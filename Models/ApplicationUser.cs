using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(120)]
        public string? FullName { get; set; } = string.Empty;

        [StringLength(120)]
        public string? JobTitle { get; set; }

        [StringLength(120)]
        public string? Department { get; set; }

        public int? AssignedStorageLocationId { get; set; }

        public string DisplayName =>
            !string.IsNullOrWhiteSpace(FullName)
                ? FullName
                : (!string.IsNullOrWhiteSpace(UserName) ? UserName! : (Email ?? "User"));
    }
}
