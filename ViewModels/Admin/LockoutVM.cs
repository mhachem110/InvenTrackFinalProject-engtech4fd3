using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class LockoutVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";

        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }

        [Display(Name = "Lockout (minutes)")]
        [Range(1, 525600)]
        public int Minutes { get; set; } = 60;
    }
}