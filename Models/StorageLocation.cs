using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StorageLocation
    {
        public int ID { get; set; }

        [Display(Name = "Location Name")]
        [Required(ErrorMessage = "Location name is required.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Location name must be between 2 and 80 characters.")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Building")]
        [StringLength(80, ErrorMessage = "Building cannot exceed 80 characters.")]
        public string? Building { get; set; }

        [Display(Name = "Room")]
        [StringLength(40, ErrorMessage = "Room cannot exceed 40 characters.")]
        public string? Room { get; set; }

        public ICollection<InventoryItem> InventoryItems { get; set; } = new HashSet<InventoryItem>();
    }
}