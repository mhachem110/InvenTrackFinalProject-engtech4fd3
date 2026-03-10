using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class CreateUserVM
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string UserName { get; set; } = string.Empty;

        [Required]
        public string SelectedRole { get; set; } = "Viewer";

        public List<string> AvailableRoles { get; set; } = new();
    }
}