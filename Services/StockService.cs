using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Services
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
            int quantity,
            int? targetLocationId = null,
            string? notes = null,
            string? performedBy = null)
        {
            if (quantity == 0)
                return (false, "Quantity cannot be 0.");

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync(async () =>
                {
                    var item = await _context.InventoryItems
                        .FirstOrDefaultAsync(i => i.ID == inventoryItemId);

                    if (item == null)
                        return (false, "Inventory item not found.");

                    int normalizedQty = (actionType == StockActionType.Adjustment) ? quantity : Math.Abs(quantity);

                    int delta = actionType switch
                    {
                        StockActionType.CheckIn => normalizedQty,
                        StockActionType.CheckOut => -normalizedQty,
                        StockActionType.Adjustment => quantity,
                        StockActionType.Transfer => 0,
                        _ => 0
                    };

                    if (actionType == StockActionType.Transfer)
                    {
                        if (!targetLocationId.HasValue || targetLocationId.Value < 1)
                            return (false, "Target location is required for a transfer.");

                        if (targetLocationId.Value == item.StorageLocationID)
                            return (false, "Target location must be different from current location.");

                        if (item.QuantityOnHand != normalizedQty)
                            return (false, $"Transfer must move the full stock. Set quantity to {item.QuantityOnHand}.");

                        bool exists = await _context.StorageLocations.AnyAsync(l => l.ID == targetLocationId.Value);
                        if (!exists)
                            return (false, "Target location not found.");
                    }

                    int newQty = item.QuantityOnHand + delta;
                    if (newQty < 0)
                        return (false, $"Insufficient stock. Current QOH = {item.QuantityOnHand}.");

                    if (actionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(notes))
                        return (false, "Notes are required for an Adjustment.");

                    performedBy = string.IsNullOrWhiteSpace(performedBy) ? "System" : performedBy.Trim();
                    notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

                    int fromLocationId = item.StorageLocationID;
                    int? toLocationId = actionType == StockActionType.Transfer ? targetLocationId : null;

                    await using var dbTx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        item.QuantityOnHand = newQty;

                        if (actionType == StockActionType.Transfer && toLocationId.HasValue)
                            item.StorageLocationID = toLocationId.Value;

                        var tx = new StockTransaction
                        {
                            InventoryItemID = inventoryItemId,
                            ActionType = actionType,
                            DateCreated = DateTime.UtcNow,
                            PerformedBy = performedBy,
                            Notes = notes,
                            QuantityChange = actionType == StockActionType.Transfer ? normalizedQty : delta,
                            FromStorageLocationID = actionType == StockActionType.Transfer ? fromLocationId : null,
                            ToStorageLocationID = actionType == StockActionType.Transfer ? toLocationId : null
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
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}