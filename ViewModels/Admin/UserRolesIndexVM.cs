using System.Collections.Generic;

namespace InvenTrack.ViewModels.Admin
{
    public class UserRolesIndexVM
    {
        public string? Query { get; set; }
        public List<UserRoleRowVM> Users { get; set; } = new();
    }
}