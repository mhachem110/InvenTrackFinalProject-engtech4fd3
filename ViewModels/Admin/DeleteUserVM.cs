using System;

namespace InvenTrack.ViewModels.Admin
{
    public class DeleteUserVM
    {
        public string UserId { get; set; } = "";
        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";
        public string Role { get; set; } = "None";
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        public bool IsSelf { get; set; }
        public bool IsLastAdmin { get; set; }
    }
}