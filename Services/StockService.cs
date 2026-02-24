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
            int? fromLocationId = null,
            int? toLocationId = null,
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
                    var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.ID == inventoryItemId);
                    if (item == null)
                        return (false, "Inventory item not found.");

                    var hasStocks = await _context.InventoryItemStocks.AnyAsync(s => s.InventoryItemID == inventoryItemId);
                    if (!hasStocks)
                    {
                        _context.InventoryItemStocks.Add(new InventoryItemStock
                        {
                            InventoryItemID = inventoryItemId,
                            StorageLocationID = item.StorageLocationID,
                            QuantityOnHand = item.QuantityOnHand
                        });
                        await _context.SaveChangesAsync();
                    }

                    int qtyAbs = (actionType == StockActionType.Adjustment) ? quantity : Math.Abs(quantity);

                    if (actionType == StockActionType.CheckIn)
                    {
                        if (!toLocationId.HasValue || toLocationId.Value < 1)
                            return (false, "Please select a location for Check In.");
                    }

                    if (actionType == StockActionType.CheckOut || actionType == StockActionType.Adjustment)
                    {
                        if (!fromLocationId.HasValue || fromLocationId.Value < 1)
                            return (false, "Please select a location.");
                    }

                    if (actionType == StockActionType.Transfer)
                    {
                        if (!fromLocationId.HasValue || fromLocationId.Value < 1)
                            return (false, "Please select a source location.");
                        if (!toLocationId.HasValue || toLocationId.Value < 1)
                            return (false, "Please select a target location.");
                        if (fromLocationId.Value == toLocationId.Value)
                            return (false, "Target location must be different from source location.");
                    }

                    if (actionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(notes))
                        return (false, "Notes are required for an Adjustment.");

                    performedBy = string.IsNullOrWhiteSpace(performedBy) ? "System" : performedBy.Trim();
                    notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

                    await using var dbTx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        InventoryItemStock? fromStock = null;
                        InventoryItemStock? toStock = null;

                        if (fromLocationId.HasValue)
                        {
                            fromStock = await _context.InventoryItemStocks
                                .FirstOrDefaultAsync(s => s.InventoryItemID == inventoryItemId && s.StorageLocationID == fromLocationId.Value);
                        }

                        if (toLocationId.HasValue)
                        {
                            toStock = await _context.InventoryItemStocks
                                .FirstOrDefaultAsync(s => s.InventoryItemID == inventoryItemId && s.StorageLocationID == toLocationId.Value);
                        }

                        if (actionType == StockActionType.CheckIn)
                        {
                            if (toStock == null)
                            {
                                toStock = new InventoryItemStock
                                {
                                    InventoryItemID = inventoryItemId,
                                    StorageLocationID = toLocationId!.Value,
                                    QuantityOnHand = 0
                                };
                                _context.InventoryItemStocks.Add(toStock);
                            }

                            toStock.QuantityOnHand += qtyAbs;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = qtyAbs,
                                ToStorageLocationID = toLocationId
                            });
                        }
                        else if (actionType == StockActionType.CheckOut)
                        {
                            if (fromStock == null)
                                return (false, "No stock exists in the selected location.");

                            if (fromStock.QuantityOnHand < qtyAbs)
                                return (false, $"Insufficient stock in that location. Available = {fromStock.QuantityOnHand}.");

                            fromStock.QuantityOnHand -= qtyAbs;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = -qtyAbs,
                                FromStorageLocationID = fromLocationId
                            });
                        }
                        else if (actionType == StockActionType.Adjustment)
                        {
                            if (fromStock == null)
                            {
                                fromStock = new InventoryItemStock
                                {
                                    InventoryItemID = inventoryItemId,
                                    StorageLocationID = fromLocationId!.Value,
                                    QuantityOnHand = 0
                                };
                                _context.InventoryItemStocks.Add(fromStock);
                            }

                            var newLocQty = fromStock.QuantityOnHand + quantity;
                            if (newLocQty < 0)
                                return (false, $"Adjustment would make stock negative in that location. Current = {fromStock.QuantityOnHand}.");

                            fromStock.QuantityOnHand = newLocQty;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = quantity,
                                FromStorageLocationID = fromLocationId
                            });
                        }
                        else if (actionType == StockActionType.Transfer)
                        {
                            if (fromStock == null)
                                return (false, "No stock exists in the selected source location.");

                            if (fromStock.QuantityOnHand < qtyAbs)
                                return (false, $"Insufficient stock in source location. Available = {fromStock.QuantityOnHand}.");

                            if (toStock == null)
                            {
                                toStock = new InventoryItemStock
                                {
                                    InventoryItemID = inventoryItemId,
                                    StorageLocationID = toLocationId!.Value,
                                    QuantityOnHand = 0
                                };
                                _context.InventoryItemStocks.Add(toStock);
                            }

                            fromStock.QuantityOnHand -= qtyAbs;
                            toStock.QuantityOnHand += qtyAbs;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = qtyAbs,
                                FromStorageLocationID = fromLocationId,
                                ToStorageLocationID = toLocationId
                            });
                        }

                        var total = await _context.InventoryItemStocks
                            .Where(s => s.InventoryItemID == inventoryItemId)
                            .SumAsync(s => s.QuantityOnHand);

                        item.QuantityOnHand = total;

                        var primary = await _context.InventoryItemStocks
                            .Where(s => s.InventoryItemID == inventoryItemId)
                            .OrderByDescending(s => s.QuantityOnHand)
                            .Select(s => s.StorageLocationID)
                            .FirstOrDefaultAsync();

                        if (primary > 0)
                            item.StorageLocationID = primary;

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