using InvenTrack.Models;
using InvenTrack.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminUsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private static readonly string[] AllowedRoles = new[] { "Admin", "Manager", "Viewer" };

        public AdminUsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(string? q)
        {
            await EnsureRolesExistAsync();

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var s = q.Trim().ToLowerInvariant();
                query = query.Where(u =>
                    (u.Email ?? "").ToLower().Contains(s) ||
                    (u.UserName ?? "").ToLower().Contains(s));
            }

            var users = await query
                .OrderBy(u => u.Email)
                .Take(500)
                .ToListAsync();

            var vm = new UserRolesIndexVM { Query = q };

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
                SelectedRole = "Viewer",
                EmailConfirmed = true
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

            var user = new ApplicationUser
            {
                Email = email,
                UserName = userName,
                EmailConfirmed = vm.EmailConfirmed
            };

            var createRes = await _userManager.CreateAsync(user, vm.Password);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            var roleRes = await _userManager.AddToRoleAsync(user, vm.SelectedRole);
            if (!roleRes.Succeeded)
            {
                foreach (var e in roleRes.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);

                return View(vm);
            }

            TempData["RoleMsg"] = $"Created user {email} with role {vm.SelectedRole}.";
            return RedirectToAction(nameof(Index));
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
    }
}