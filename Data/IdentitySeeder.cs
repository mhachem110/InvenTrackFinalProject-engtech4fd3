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

            string[] roles = { "Admin", "Manager", "Viewer" };

            foreach (var r in roles)
            {
                if (!await roleManager.RoleExistsAsync(r))
                {
                    var roleRes = await roleManager.CreateAsync(new IdentityRole(r));
                    if (!roleRes.Succeeded)
                        throw new InvalidOperationException("Failed to create role: " + r);
                }
            }

            var adminEmail = "admin@inventrack.local";
            var adminPass = "Admin@2026";

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createRes = await userManager.CreateAsync(admin, adminPass);
                if (!createRes.Succeeded)
                {
                    var msg = string.Join("; ", createRes.Errors.Select(e => e.Description));
                    throw new InvalidOperationException("Failed to create admin user: " + msg);
                }
            }

            if (!await userManager.IsInRoleAsync(admin, "Admin"))
            {
                var addRoleRes = await userManager.AddToRoleAsync(admin, "Admin");
                if (!addRoleRes.Succeeded)
                {
                    var msg = string.Join("; ", addRoleRes.Errors.Select(e => e.Description));
                    throw new InvalidOperationException("Failed to assign Admin role: " + msg);
                }
            }
        }
    }
}