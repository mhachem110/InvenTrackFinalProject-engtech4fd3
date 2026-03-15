using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace InvenTrack.ViewModels.TransferRequests
{
    public class TransferRequestCreateVM
    {
        [Required]
        public int InventoryItemID { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; } = 1;

        [Required]
        [Display(Name = "From Location")]
        public int FromStorageLocationID { get; set; }

        [Required]
        [Display(Name = "To Location")]
        public int ToStorageLocationID { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<SelectListItem> AvailableLocations { get; set; } = new();
    }
}