using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Admin
{
    public class ResetPasswordVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = "-";
        public string UserName { get; set; } = "-";

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}