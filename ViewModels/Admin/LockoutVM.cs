using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class LockoutVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";

        [Display(Name = "Full Name")]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Job Title")]
        [StringLength(120)]
        public string JobTitle { get; set; } = string.Empty;

        [StringLength(120)]
        public string Department { get; set; } = string.Empty;

        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        [Display(Name = "Lockout (minutes)")]
        [Range(1, 525600)]
        public int Minutes { get; set; } = 60;
    }
}