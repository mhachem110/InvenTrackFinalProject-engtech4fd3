using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StockTransaction
    {
        public int ID { get; set; }

        [Display(Name = "Reference #")]
        public int? ReferenceNumber { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime DateCreated { get; set; } = DateTime.Today;

        [Display(Name = "Action")]
        public StockActionType ActionType { get; set; } = StockActionType.Adjustment;

        [Display(Name = "Quantity Change")]
        public int QuantityChange { get; set; }

        [StringLength(250)]
        public string Notes { get; set; }

        [Display(Name = "Performed By")]
        [StringLength(120)]
        public string PerformedBy { get; set; }

        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; }
    }

    public enum StockActionType
    {
        CheckIn = 1,     // Stock added
        CheckOut = 2,    // Stock removed
        Adjustment = 3   // Manual correction
    }
}
