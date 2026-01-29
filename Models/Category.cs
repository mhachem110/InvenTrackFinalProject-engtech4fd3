using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class Category
    {
        public int ID { get; set; }

        [Required]
        [StringLength(80)]
        public string Name { get; set; }

        [StringLength(250)]
        public string Description { get; set; }

        public ICollection<InventoryItem> InventoryItems { get; set; } = new HashSet<InventoryItem>();
    }
}
