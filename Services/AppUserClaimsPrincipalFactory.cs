using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace InvenTrack.Services
{
    public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly InvenTrackContext _context;

        public AppUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            InvenTrackContext context)
            : base(userManager, roleManager, optionsAccessor)
        {
            _context = context;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            identity.AddClaim(new Claim("DisplayName", UserDisplayHelper.GetDisplayName(user)));
            identity.AddClaim(new Claim("FullName", user.FullName ?? string.Empty));
            identity.AddClaim(new Claim("JobTitle", user.JobTitle ?? string.Empty));
            identity.AddClaim(new Claim("Department", user.Department ?? string.Empty));

            if (user.AssignedStorageLocationId.HasValue)
            {
                var locationName = _context.StorageLocations
                    .Where(x => x.ID == user.AssignedStorageLocationId.Value)
                    .Select(x => x.Name)
                    .FirstOrDefault() ?? string.Empty;

                identity.AddClaim(new Claim("AssignedLocationId", user.AssignedStorageLocationId.Value.ToString()));
                identity.AddClaim(new Claim("AssignedLocationName", locationName));
            }
            else
            {
                identity.AddClaim(new Claim("AssignedLocationName", "All Locations"));
            }

            return identity;
        }
    }
}
