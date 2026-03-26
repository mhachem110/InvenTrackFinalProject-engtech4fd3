using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class ResetPasswordVM
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

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}