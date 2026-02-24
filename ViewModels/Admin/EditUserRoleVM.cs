using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class EditUserRoleVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";

        public string CurrentRole { get; set; } = "None";

        [Required]
        [Display(Name = "Role")]
        public string SelectedRole { get; set; } = "Viewer";

        public List<string> AvailableRoles { get; set; } = new();
    }
}