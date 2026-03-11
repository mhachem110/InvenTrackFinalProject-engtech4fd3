using System.Security.Claims;

namespace InvenTrack.Utilities
{
    public static class UserRoleExtensions
    {
        public static bool CanManage(this ClaimsPrincipal user)
            => user?.IsInRole("Admin") == true || user?.IsInRole("Manager") == true;

        public static bool IsViewer(this ClaimsPrincipal user)
            => user?.IsInRole("Viewer") == true;
    }
}