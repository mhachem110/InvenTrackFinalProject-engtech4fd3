using InvenTrack.Models;
using Microsoft.AspNetCore.Identity;

namespace InvenTrack.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await EnsureRolesExistAsync(roleManager);
            await EnsureAdminUserAsync(userManager);
        }

        private static async Task EnsureRolesExistAsync(RoleManager<IdentityRole> roleManager)
        {
            foreach (var role in AppRoles.All)
            {
                if (await roleManager.RoleExistsAsync(role))
                    continue;

                var createRoleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (!createRoleResult.Succeeded)
                {
                    var msg = string.Join("; ", createRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Failed to create role '{role}': {msg}");
                }
            }
        }

        private static async Task EnsureAdminUserAsync(UserManager<ApplicationUser> userManager)
        {
            const string adminEmail = "admin@inventrack.local";
            const string adminPassword = "Admin@2026";

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    AssignedStorageLocationId = null
                };

                var createUserResult = await userManager.CreateAsync(admin, adminPassword);
                if (!createUserResult.Succeeded)
                {
                    var msg = string.Join("; ", createUserResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException("Failed to create admin user: " + msg);
                }
            }
            else
            {
                var needsUpdate = false;

                if (!admin.EmailConfirmed)
                {
                    admin.EmailConfirmed = true;
                    needsUpdate = true;
                }

                if (admin.AssignedStorageLocationId != null)
                {
                    admin.AssignedStorageLocationId = null;
                    needsUpdate = true;
                }

                if (needsUpdate)
                {
                    var updateUserResult = await userManager.UpdateAsync(admin);
                    if (!updateUserResult.Succeeded)
                    {
                        var msg = string.Join("; ", updateUserResult.Errors.Select(e => e.Description));
                        throw new InvalidOperationException("Failed to update admin user: " + msg);
                    }
                }
            }

            var currentRoles = await userManager.GetRolesAsync(admin);

            var rolesToRemove = currentRoles
                .Where(r => r != AppRoles.Admin)
                .ToList();

            if (rolesToRemove.Count > 0)
            {
                var removeRolesResult = await userManager.RemoveFromRolesAsync(admin, rolesToRemove);
                if (!removeRolesResult.Succeeded)
                {
                    var msg = string.Join("; ", removeRolesResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException("Failed to remove non-admin roles from admin user: " + msg);
                }
            }

            if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
            {
                var addAdminRoleResult = await userManager.AddToRoleAsync(admin, AppRoles.Admin);
                if (!addAdminRoleResult.Succeeded)
                {
                    var msg = string.Join("; ", addAdminRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException("Failed to assign Admin role: " + msg);
                }
            }
        }
    }
}