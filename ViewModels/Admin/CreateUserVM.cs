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
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string SelectedRole { get; set; } = "Viewer";

        public bool EmailConfirmed { get; set; } = true;

        public List<string> AvailableRoles { get; set; } = new();
    }
}