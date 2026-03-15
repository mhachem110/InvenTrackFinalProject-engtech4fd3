using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminUsersController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        public AdminUsersController(
            InvenTrackContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _emailSender = emailSender;
        }

        public async Task<IActionResult> Index(string? q, int page = 1, int pageSize = 10)
        {
            await EnsureRolesExistAsync();

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            var query = _userManager.Users
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();

                var matchingLocationIds = await _context.StorageLocations
                    .AsNoTracking()
                    .Where(l => l.Name.Contains(q))
                    .Select(l => l.ID)
                    .ToListAsync();

                query = query.Where(u =>
                    (u.Email ?? "").Contains(q) ||
                    (u.UserName ?? "").Contains(q) ||
                    (u.AssignedStorageLocationId.HasValue && matchingLocationIds.Contains(u.AssignedStorageLocationId.Value)));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var users = await query
                .OrderBy(u => u.Email)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var locationIds = users
                .Where(u => u.AssignedStorageLocationId.HasValue)
                .Select(u => u.AssignedStorageLocationId!.Value)
                .Distinct()
                .ToList();

            var locationMap = locationIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.StorageLocations
                    .AsNoTracking()
                    .Where(l => locationIds.Contains(l.ID))
                    .ToDictionaryAsync(l => l.ID, l => l.Name);

            var vm = new UserRolesIndexVM
            {
                Query = q,
                PageIndex = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var role = roles.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";
                var isLockedOut = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;

                string locationName = "All Locations";
                if (u.AssignedStorageLocationId.HasValue &&
                    locationMap.TryGetValue(u.AssignedStorageLocationId.Value, out var resolvedName))
                {
                    locationName = resolvedName;
                }

                vm.Users.Add(new UserRoleRowVM
                {
                    UserId = u.Id,
                    Email = u.Email ?? "-",
                    UserName = u.UserName ?? "-",
                    Role = role,
                    AssignedLocationName = locationName,
                    IsLockedOut = isLockedOut,
                    LockoutEnd = u.LockoutEnd
                });
            }

            return View(vm);
        }

        public async Task<IActionResult> Create()
        {
            await EnsureRolesExistAsync();

            var vm = new CreateUserVM
            {
                AvailableRoles = AppRoles.All.ToList(),
                SelectedRole = AppRoles.Employee,
                AvailableLocations = await GetLocationSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserVM vm)
        {
            await EnsureRolesExistAsync();

            vm.AvailableRoles = AppRoles.All.ToList();
            vm.AvailableLocations = await GetLocationSelectListAsync();

            vm.Email = (vm.Email ?? string.Empty).Trim();
            vm.UserName = (vm.UserName ?? string.Empty).Trim();
            vm.SelectedRole = NormalizeRole(vm.SelectedRole);

            await ValidateRoleAndLocationAsync(vm.SelectedRole, vm.AssignedStorageLocationId);

            if (AppRoles.GlobalRoles.Contains(vm.SelectedRole))
                vm.AssignedStorageLocationId = null;

            if (!ModelState.IsValid)
                return View(vm);

            var existingEmail = await _userManager.FindByEmailAsync(vm.Email);
            if (existingEmail != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email already exists.");
                return View(vm);
            }

            var existingUser = await _userManager.FindByNameAsync(vm.UserName);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(vm.UserName), "Username already exists.");
                return View(vm);
            }

            var temporaryPassword = GenerateTemporaryPassword();

            var user = new ApplicationUser
            {
                Email = vm.Email,
                UserName = vm.UserName,
                EmailConfirmed = true,
                AssignedStorageLocationId = vm.AssignedStorageLocationId
            };

            var createRes = await _userManager.CreateAsync(user, temporaryPassword);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            var roleRes = await _userManager.AddToRoleAsync(user, vm.SelectedRole);
            if (!roleRes.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                foreach (var e in roleRes.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            try
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken));

                var setPasswordUrl = Url.Action(
                    "SetInitialPassword",
                    "AdminUsers",
                    new { email = user.Email, code = encodedToken },
                    protocol: Request.Scheme);

                if (string.IsNullOrWhiteSpace(setPasswordUrl))
                {
                    await _userManager.DeleteAsync(user);
                    ModelState.AddModelError(string.Empty, "Unable to generate the password setup link.");
                    return View(vm);
                }

                var locationName = "All Locations";
                if (vm.AssignedStorageLocationId.HasValue)
                {
                    locationName = await _context.StorageLocations
                        .Where(x => x.ID == vm.AssignedStorageLocationId.Value)
                        .Select(x => x.Name)
                        .FirstOrDefaultAsync() ?? "-";
                }

                var subject = "Your InvenTrack account";
                var body = $@"
<p>Hello {HtmlEncoder.Default.Encode(user.UserName ?? user.Email ?? "")},</p>
<p>An administrator created your InvenTrack account.</p>
<p>
<strong>Username:</strong> {HtmlEncoder.Default.Encode(user.UserName ?? "-")}<br />
<strong>Role:</strong> {HtmlEncoder.Default.Encode(GetRoleDisplayName(vm.SelectedRole))}<br />
<strong>Assigned location:</strong> {HtmlEncoder.Default.Encode(locationName)}<br />
<strong>Temporary password:</strong> {HtmlEncoder.Default.Encode(temporaryPassword)}
</p>
<p>Please use the link below to set your own password:</p>
<p><a href=""{HtmlEncoder.Default.Encode(setPasswordUrl)}"">Set your password</a></p>";

                await _emailSender.SendEmailAsync(vm.Email, subject, body);
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "User was not created because the onboarding email could not be sent.");
                return View(vm);
            }

            TempData["RoleMsg"] = $"Created user {vm.Email} with role {GetRoleDisplayName(vm.SelectedRole)}.";
            return RedirectToAction(nameof(Index));
        }

        [AllowAnonymous]
        public IActionResult SetInitialPassword(string? email, string? code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return BadRequest();

            var vm = new SetInitialPasswordVM
            {
                Email = email,
                Code = code
            };

            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetInitialPassword(SetInitialPasswordVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email.Trim());
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired setup link.");
                return View(vm);
            }

            var tempPasswordValid = await _userManager.CheckPasswordAsync(user, vm.TemporaryPassword);
            if (!tempPasswordValid)
            {
                ModelState.AddModelError(nameof(vm.TemporaryPassword), "Temporary password is invalid.");
                return View(vm);
            }

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(vm.Code));
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Invalid or expired setup link.");
                return View(vm);
            }

            var resetRes = await _userManager.ResetPasswordAsync(user, decodedToken, vm.NewPassword);
            if (!resetRes.Succeeded)
            {
                foreach (var e in resetRes.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            return Redirect("~/Identity/Account/Login?onboarded=1");
        }

        public async Task<IActionResult> Edit(string id)
        {
            await EnsureRolesExistAsync();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            var current = roles.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";

            var currentLocationName = "All Locations";
            if (user.AssignedStorageLocationId.HasValue)
            {
                currentLocationName = await _context.StorageLocations
                    .AsNoTracking()
                    .Where(x => x.ID == user.AssignedStorageLocationId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync() ?? "-";
            }

            var vm = new EditUserRoleVM
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                UserName = user.UserName ?? "-",
                CurrentRole = current,
                SelectedRole = current == "None" ? AppRoles.Employee : current,
                AssignedStorageLocationId = user.AssignedStorageLocationId,
                CurrentLocationName = currentLocationName,
                AvailableRoles = AppRoles.All.ToList(),
                AvailableLocations = await GetLocationSelectListAsync()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserRoleVM vm)
        {
            await EnsureRolesExistAsync();

            vm.AvailableRoles = AppRoles.All.ToList();
            vm.AvailableLocations = await GetLocationSelectListAsync();

            if (string.IsNullOrWhiteSpace(vm.UserId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            vm.Email = user.Email ?? "-";
            vm.UserName = user.UserName ?? "-";

            if (user.AssignedStorageLocationId.HasValue)
            {
                vm.CurrentLocationName = await _context.StorageLocations
                    .AsNoTracking()
                    .Where(x => x.ID == user.AssignedStorageLocationId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefaultAsync() ?? "-";
            }
            else
            {
                vm.CurrentLocationName = "All Locations";
            }

            vm.SelectedRole = NormalizeRole(vm.SelectedRole);

            await ValidateRoleAndLocationAsync(vm.SelectedRole, vm.AssignedStorageLocationId);

            if (AppRoles.GlobalRoles.Contains(vm.SelectedRole))
                vm.AssignedStorageLocationId = null;

            if (!ModelState.IsValid)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);
                vm.CurrentRole = currentRoles.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";
                return View(vm);
            }

            user.AssignedStorageLocationId = vm.AssignedStorageLocationId;

            var existing = await _userManager.GetRolesAsync(user);
            var toRemove = existing.Where(r => AppRoles.All.Contains(r)).ToList();

            if (toRemove.Count > 0)
            {
                var removeRes = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removeRes.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "Failed to remove old role(s).");
                    vm.CurrentRole = existing.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";
                    return View(vm);
                }
            }

            var addRes = await _userManager.AddToRoleAsync(user, vm.SelectedRole);
            if (!addRes.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Failed to assign the new role.");
                return View(vm);
            }

            var updateRes = await _userManager.UpdateAsync(user);
            if (!updateRes.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Failed to update the assigned location.");
                return View(vm);
            }

            TempData["RoleMsg"] = $"Updated access for {user.Email ?? user.UserName}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var vm = new ResetPasswordVM
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                UserName = user.UserName ?? "-"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            vm.Email = user.Email ?? "-";
            vm.UserName = user.UserName ?? "-";

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var res = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);

            if (!res.Succeeded)
            {
                foreach (var e in res.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            TempData["RoleMsg"] = $"Password reset for {user.Email ?? user.UserName}.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Lockout(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            var vm = new LockoutVM
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                UserName = user.UserName ?? "-",
                IsLockedOut = isLockedOut,
                LockoutEnd = user.LockoutEnd,
                Minutes = 60
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lockout(LockoutVM vm)
        {
            if (vm.Minutes < 1)
                ModelState.AddModelError(nameof(vm.Minutes), "Minutes must be 1 or more.");

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            vm.Email = user.Email ?? "-";
            vm.UserName = user.UserName ?? "-";

            if (!ModelState.IsValid)
            {
                vm.IsLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                vm.LockoutEnd = user.LockoutEnd;
                return View(vm);
            }

            var enable = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!enable.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Unable to enable lockout.");
                return View(vm);
            }

            var until = DateTimeOffset.UtcNow.AddMinutes(vm.Minutes);
            var res = await _userManager.SetLockoutEndDateAsync(user, until);

            if (!res.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Unable to set lockout end date.");
                return View(vm);
            }

            TempData["RoleMsg"] = $"Locked {user.Email ?? user.UserName} until {until:yyyy-MM-dd HH:mm} UTC.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var res = await _userManager.SetLockoutEndDateAsync(user, null);

            TempData["RoleMsg"] = res.Succeeded
                ? $"Unlocked {user.Email ?? user.UserName}."
                : "Unable to unlock user.";

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isSelf = string.Equals(currentUserId, user.Id, StringComparison.Ordinal);

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";

            var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            var isLastAdmin = role == AppRoles.Admin && await IsLastAdminAsync(user.Id);

            var vm = new DeleteUserVM
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                UserName = user.UserName ?? "-",
                Role = role,
                IsLockedOut = isLockedOut,
                LockoutEnd = user.LockoutEnd,
                IsSelf = isSelf,
                IsLastAdmin = isLastAdmin
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(DeleteUserVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.UserId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null)
            {
                TempData["RoleMsg"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            var currentUserId = _userManager.GetUserId(User);
            if (string.Equals(currentUserId, user.Id, StringComparison.Ordinal))
            {
                TempData["RoleMsg"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(r => AppRoles.All.Contains(r)) ?? "None";

            if (role == AppRoles.Admin && await IsLastAdminAsync(user.Id))
            {
                TempData["RoleMsg"] = "Cannot delete the last Admin account.";
                return RedirectToAction(nameof(Index));
            }

            var res = await _userManager.DeleteAsync(user);

            if (!res.Succeeded)
            {
                TempData["RoleMsg"] = "Unable to delete user.";
                return RedirectToAction(nameof(Index));
            }

            TempData["RoleMsg"] = $"Deleted user {user.Email ?? user.UserName}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetLocationSelectListAsync()
        {
            return await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.Name
                })
                .ToListAsync();
        }

        private async Task<bool> IsLastAdminAsync(string userId)
        {
            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            return admins.Count == 1 && admins[0].Id == userId;
        }

        private async Task EnsureRolesExistAsync()
        {
            foreach (var role in AppRoles.All)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                    await _roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        private async Task ValidateRoleAndLocationAsync(string selectedRole, int? assignedStorageLocationId)
        {
            if (string.IsNullOrWhiteSpace(selectedRole) || !AppRoles.All.Contains(selectedRole))
            {
                ModelState.AddModelError("SelectedRole", "Please select a valid role.");
                return;
            }

            if (AppRoles.ScopedRoles.Contains(selectedRole) && !assignedStorageLocationId.HasValue)
            {
                ModelState.AddModelError("AssignedStorageLocationId", "This role requires an assigned location.");
                return;
            }

            if (assignedStorageLocationId.HasValue)
            {
                var exists = await _context.StorageLocations
                    .AsNoTracking()
                    .AnyAsync(x => x.ID == assignedStorageLocationId.Value);

                if (!exists)
                {
                    ModelState.AddModelError("AssignedStorageLocationId", "Selected location was not found.");
                }
            }
        }

        private static string NormalizeRole(string? role)
            => (role ?? string.Empty).Trim();

        private static string GetRoleDisplayName(string role)
        {
            return role switch
            {
                AppRoles.Admin => "Admin",
                AppRoles.RegionalManager => "Regional Manager",
                AppRoles.Manager => "Manager",
                AppRoles.Supervisor => "Supervisor",
                AppRoles.Employee => "Employee",
                _ => role
            };
        }

        private static string GenerateTemporaryPassword(int length = 12)
        {
            const string lower = "abcdefghijkmnopqrstuvwxyz";
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digits = "23456789";
            const string special = "!@$?_-.";

            var all = lower + upper + digits + special;

            var chars = new List<char>
            {
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                special[RandomNumberGenerator.GetInt32(special.Length)]
            };

            while (chars.Count < length)
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }
    }
}