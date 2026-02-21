using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class Category
    {
        public int ID { get; set; }

        [Display(Name = "Category Name")]
        [Required(ErrorMessage = "Category name is required.")]
        [StringLength(80, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 80 characters.")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
        public string? Description { get; set; }

        public ICollection<InventoryItem> InventoryItems { get; set; } = new HashSet<InventoryItem>();
    }
}