using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StorageLocation
    {
        public int ID { get; set; }

        [Required]
        [StringLength(80)]
        [Display(Name = "Location Name")]
        public string Name { get; set; }

        [StringLength(80)]
        public string Building { get; set; }

        [StringLength(40)]
        public string Room { get; set; }

        public ICollection<InventoryItem> InventoryItems { get; set; } = new HashSet<InventoryItem>();
    }
}
