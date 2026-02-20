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
                StockActionType.Adjustment => quantityChange, // can be +/- for adjustments
                _ => 0
            };

            if (delta == 0)
                return (false, "Invalid transaction: quantity change cannot be 0.");

            int newQty = item.QuantityOnHand + delta;

            if (newQty < 0)
                return (false, $"Insufficient stock. Current QOH = {item.QuantityOnHand}.");

            using var dbTx = await _context.Database.BeginTransactionAsync();

            try
            {
                item.QuantityOnHand = newQty;

                var tx = new StockTransaction
                {
                    InventoryItemID = inventoryItemId,
                    ActionType = actionType,
                    QuantityChange = delta,                 // store signed delta
                    Notes = notes,
                    PerformedBy = string.IsNullOrWhiteSpace(performedBy) ? "System" : performedBy,
                    DateCreated = DateTime.UtcNow
                };

                _context.StockTransactions.Add(tx);

                await _context.SaveChangesAsync();
                await dbTx.CommitAsync();

                return (true, null);
            }
            catch (Exception ex)
            {
                await dbTx.RollbackAsync();
                return (false, ex.Message);
            }
        }
    }
}
