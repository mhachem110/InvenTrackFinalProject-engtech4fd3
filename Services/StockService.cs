using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace InvenTrackFinalProject.Services
{
    public class StockService
    {
        private readonly InvenTrackContext _context;

        public StockService(InvenTrackContext context)
        {
            _context = context;
        }

        public async Task<(bool ok, string? error)> ApplyTransactionAsync(
            int inventoryItemId,
            StockActionType actionType,
            int quantityChange,
            string? notes = null,
            string? performedBy = null)
        {
            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.ID == inventoryItemId);

            if (item == null)
                return (false, "Inventory item not found.");

            // Convert to signed delta:
            int delta = actionType switch
            {
                StockActionType.CheckIn => Math.Abs(quantityChange),
                StockActionType.CheckOut => -Math.Abs(quantityChange),
                StockActionType.Adjustment => quantityChange,
                _ => 0
            };

            if (delta == 0)
                return (false, "Invalid transaction: quantity change cannot be 0.");

            int newQty = item.QuantityOnHand + delta;

            if (newQty < 0)
                return (false, $"Insufficient stock. Current QOH = {item.QuantityOnHand}.");

            try
            {
                // EF Core will automatically use a transaction for SaveChanges when multiple commands are executed.
                item.QuantityOnHand = newQty;

                var tx = new StockTransaction
                {
                    InventoryItemID = inventoryItemId,
                    ActionType = actionType,
                    QuantityChange = delta,
                    Notes = notes,
                    PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? "System" : performedBy,
                    DateCreated = DateTime.UtcNow
                };

                _context.StockTransactions.Add(tx);

                await _context.SaveChangesAsync();
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}