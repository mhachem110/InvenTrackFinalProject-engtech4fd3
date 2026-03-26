using InvenTrack.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class EditUserRoleVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";

        [Required]
        [Display(Name = "Full Name")]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Job Title")]
        [StringLength(120)]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(120)]
        public string Department { get; set; } = string.Empty;

        public string CurrentRole { get; set; } = "None";

        [Required]
        [Display(Name = "Role")]
        public string SelectedRole { get; set; } = AppRoles.Employee;

        [Display(Name = "Assigned Location")]
        public int? AssignedStorageLocationId { get; set; }

        public string CurrentLocationName { get; set; } = "-";

        public List<string> AvailableRoles { get; set; } = new();
        public List<SelectListItem> AvailableLocations { get; set; } = new();
    }
}
