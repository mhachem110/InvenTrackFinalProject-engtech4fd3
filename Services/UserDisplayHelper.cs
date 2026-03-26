using InvenTrack.Models;

namespace InvenTrack.Services
{
    public static class UserDisplayHelper
    {
        public static string GetDisplayName(ApplicationUser? user)
        {
            if (user == null) return "Unknown";
            if (!string.IsNullOrWhiteSpace(user.FullName)) return user.FullName;
            if (!string.IsNullOrWhiteSpace(user.UserName)) return user.UserName!;
            if (!string.IsNullOrWhiteSpace(user.Email)) return user.Email!;
            return "Unknown";
        }
    }
}
