namespace InvenTrack.ViewModels.Admin
{
    public class UserRoleRowVM
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";
        public string FullName { get; set; } = "-";
        public string JobTitle { get; set; } = "-";
        public string Department { get; set; } = "-";
        public string Role { get; set; } = "None";
        public string AssignedLocationName { get; set; } = "-";
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
    }
}
