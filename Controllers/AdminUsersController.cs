using InvenTrack.Models;
using InvenTrack.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;

        private static readonly string[] AllowedRoles = new[] { "Admin", "Manager", "Viewer" };

        public AdminUsersController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IEmailSender emailSender)
        {
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

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(u =>
                    (u.Email ?? "").Contains(q) ||
                    (u.UserName ?? "").Contains(q));
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
                var role = roles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";
                var isLockedOut = u.LockoutEnabled && u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow;

                vm.Users.Add(new UserRoleRowVM
                {
                    UserId = u.Id,
                    Email = u.Email ?? "-",
                    UserName = u.UserName ?? "-",
                    Role = role,
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
                AvailableRoles = AllowedRoles.ToList(),
                SelectedRole = "Viewer"
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateUserVM vm)
        {
            await EnsureRolesExistAsync();
            vm.AvailableRoles = AllowedRoles.ToList();

            if (!AllowedRoles.Contains(vm.SelectedRole))
                ModelState.AddModelError(nameof(vm.SelectedRole), "Please select a valid role.");

            if (!ModelState.IsValid)
                return View(vm);

            var email = vm.Email.Trim();
            var userName = vm.UserName.Trim();

            var existingEmail = await _userManager.FindByEmailAsync(email);
            if (existingEmail != null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Email already exists.");
                return View(vm);
            }

            var existingUser = await _userManager.FindByNameAsync(userName);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(vm.UserName), "Username already exists.");
                return View(vm);
            }

            var temporaryPassword = GenerateTemporaryPassword();

            var user = new ApplicationUser
            {
                Email = email,
                UserName = userName,
                EmailConfirmed = true
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
                    nameof(SetInitialPassword),
                    "AdminUsers",
                    new { email = user.Email, code = encodedToken },
                    protocol: Request.Scheme);

                if (string.IsNullOrWhiteSpace(setPasswordUrl))
                {
                    await _userManager.DeleteAsync(user);
                    ModelState.AddModelError(string.Empty, "Unable to generate the password setup link.");
                    return View(vm);
                }

                var safeUser = HtmlEncoder.Default.Encode(user.UserName ?? user.Email ?? "");
                var safeRole = HtmlEncoder.Default.Encode(vm.SelectedRole);
                var safeTempPassword = HtmlEncoder.Default.Encode(temporaryPassword);
                var safeUrl = HtmlEncoder.Default.Encode(setPasswordUrl);

                var subject = "Your InvenTrack account";
                var body = $@"
<p>Hello {safeUser},</p>
<p>An administrator created your InvenTrack account.</p>
<p><strong>Username:</strong> {HtmlEncoder.Default.Encode(user.UserName ?? "-")}<br />
<strong>Role:</strong> {safeRole}<br />
<strong>Temporary password:</strong> {safeTempPassword}</p>
<p>Please use the link below to set your own password:</p>
<p><a href=""{safeUrl}"">Set your password</a></p>
<p>You will be asked for the temporary password once, then you can choose your own password.</p>
<p>If you were not expecting this account, please contact your administrator.</p>";

                await _emailSender.SendEmailAsync(email, subject, body);
            }
            catch
            {
                await _userManager.DeleteAsync(user);
                ModelState.AddModelError(string.Empty, "User was not created because the onboarding email could not be sent.");
                return View(vm);
            }

            TempData["RoleMsg"] = $"Created user {email} with role {vm.SelectedRole}. An onboarding email was sent.";
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
            var current = roles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";

            var vm = new EditUserRoleVM
            {
                UserId = user.Id,
                Email = user.Email ?? "-",
                UserName = user.UserName ?? "-",
                CurrentRole = current,
                SelectedRole = current == "None" ? "Viewer" : current,
                AvailableRoles = AllowedRoles.ToList()
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditUserRoleVM vm)
        {
            await EnsureRolesExistAsync();
            vm.AvailableRoles = AllowedRoles.ToList();

            if (string.IsNullOrWhiteSpace(vm.UserId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            vm.Email = user.Email ?? "-";
            vm.UserName = user.UserName ?? "-";

            if (string.IsNullOrWhiteSpace(vm.SelectedRole) || !AllowedRoles.Contains(vm.SelectedRole))
            {
                ModelState.AddModelError(nameof(vm.SelectedRole), "Please select a valid role.");
                var currentRoles = await _userManager.GetRolesAsync(user);
                vm.CurrentRole = currentRoles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";
                return View(vm);
            }

            var existing = await _userManager.GetRolesAsync(user);
            var toRemove = existing.Where(r => AllowedRoles.Contains(r)).ToList();

            if (toRemove.Count > 0)
            {
                var removeRes = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!removeRes.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "Failed to remove old role(s).");
                    vm.CurrentRole = existing.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";
                    return View(vm);
                }
            }

            var addRes = await _userManager.AddToRoleAsync(user, vm.SelectedRole);
            if (!addRes.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Failed to assign the new role.");
                var currentRoles = await _userManager.GetRolesAsync(user);
                vm.CurrentRole = currentRoles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";
                return View(vm);
            }

            TempData["RoleMsg"] = $"Updated role for {user.Email ?? user.UserName} to {vm.SelectedRole}.";
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
            await EnsureRolesExistAsync();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var isSelf = string.Equals(currentUserId, user.Id, StringComparison.Ordinal);

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";

            var isLockedOut = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

            var isLastAdmin = false;
            if (role == "Admin")
                isLastAdmin = await IsLastAdminAsync(user.Id);

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
            await EnsureRolesExistAsync();

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
            var role = roles.FirstOrDefault(r => AllowedRoles.Contains(r)) ?? "None";

            if (role == "Admin" && await IsLastAdminAsync(user.Id))
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

        private async Task<bool> IsLastAdminAsync(string userId)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            return admins.Count == 1 && admins[0].Id == userId;
        }

        private async Task EnsureRolesExistAsync()
        {
            foreach (var r in AllowedRoles)
            {
                if (!await _roleManager.RoleExistsAsync(r))
                    await _roleManager.CreateAsync(new IdentityRole(r));
            }
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
            {
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            }

            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }
    }
}