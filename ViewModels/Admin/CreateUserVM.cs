using InvenTrack.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
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
        public string SelectedRole { get; set; } = AppRoles.Employee;

        [Display(Name = "Assigned Location")]
        public int? AssignedStorageLocationId { get; set; }

        public List<string> AvailableRoles { get; set; } = new();
        public List<SelectListItem> AvailableLocations { get; set; } = new();
    }
}