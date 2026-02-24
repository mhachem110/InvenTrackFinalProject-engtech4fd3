using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace InvenTrack.Services
{
    public class StockService
    {
        private readonly InvenTrackContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<StockService> _logger;

        public StockService(
            InvenTrackContext context,
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ILogger<StockService> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
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

                    var beforeTotal = item.QuantityOnHand;
                    var reorderLevel = item.ReorderLevel;

                    performedBy = string.IsNullOrWhiteSpace(performedBy) ? "System" : performedBy.Trim();
                    notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

                    if (actionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(notes))
                        return (false, "Notes are required for an Adjustment.");

                    int qtyAbs = actionType == StockActionType.Adjustment ? quantity : Math.Abs(quantity);

                    if (actionType == StockActionType.CheckIn)
                    {
                        if (!toLocationId.HasValue || toLocationId.Value < 1)
                            return (false, "Please select a location for Check In.");
                        if (qtyAbs < 0)
                            return (false, "Quantity must be positive for Check In.");
                    }

                    if (actionType == StockActionType.CheckOut)
                    {
                        if (!fromLocationId.HasValue || fromLocationId.Value < 1)
                            return (false, "Please select a location.");
                        if (qtyAbs < 0)
                            return (false, "Quantity must be positive for Check Out.");
                    }

                    if (actionType == StockActionType.Adjustment)
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
                        if (qtyAbs < 0)
                            return (false, "Quantity must be positive for Transfer.");
                    }

                    bool shouldAlertAfterCommit = false;
                    int afterTotal = beforeTotal;
                    List<InventoryItemStock> stocksForEmail = new();

                    await using var dbTx = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var stocks = await _context.InventoryItemStocks
                            .Where(s => s.InventoryItemID == inventoryItemId)
                            .ToListAsync();

                        if (stocks.Count == 0)
                        {
                            var seed = new InventoryItemStock
                            {
                                InventoryItemID = inventoryItemId,
                                StorageLocationID = item.StorageLocationID,
                                QuantityOnHand = item.QuantityOnHand
                            };
                            _context.InventoryItemStocks.Add(seed);
                            stocks.Add(seed);
                        }

                        InventoryItemStock? fromStock = null;
                        InventoryItemStock? toStock = null;

                        if (fromLocationId.HasValue)
                            fromStock = stocks.FirstOrDefault(s => s.StorageLocationID == fromLocationId.Value);

                        if (toLocationId.HasValue)
                            toStock = stocks.FirstOrDefault(s => s.StorageLocationID == toLocationId.Value);

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
                                stocks.Add(toStock);
                            }

                            var q = Math.Abs(quantity);
                            toStock.QuantityOnHand += q;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = q,
                                ToStorageLocationID = toLocationId
                            });
                        }
                        else if (actionType == StockActionType.CheckOut)
                        {
                            if (fromStock == null)
                                return (false, "No stock exists in the selected location.");

                            var q = Math.Abs(quantity);
                            if (fromStock.QuantityOnHand < q)
                                return (false, $"Insufficient stock in that location. Available = {fromStock.QuantityOnHand}.");

                            fromStock.QuantityOnHand -= q;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = -q,
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
                                stocks.Add(fromStock);
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

                            var q = Math.Abs(quantity);
                            if (fromStock.QuantityOnHand < q)
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
                                stocks.Add(toStock);
                            }

                            fromStock.QuantityOnHand -= q;
                            toStock.QuantityOnHand += q;

                            _context.StockTransactions.Add(new StockTransaction
                            {
                                InventoryItemID = inventoryItemId,
                                ActionType = actionType,
                                DateCreated = DateTime.UtcNow,
                                PerformedBy = performedBy,
                                Notes = notes,
                                QuantityChange = q,
                                FromStorageLocationID = fromLocationId,
                                ToStorageLocationID = toLocationId
                            });
                        }

                        afterTotal = stocks.Sum(s => s.QuantityOnHand);
                        item.QuantityOnHand = afterTotal;

                        var primary = stocks
                            .Where(s => s.QuantityOnHand > 0)
                            .OrderByDescending(s => s.QuantityOnHand)
                            .Select(s => s.StorageLocationID)
                            .FirstOrDefault();

                        if (primary > 0)
                            item.StorageLocationID = primary;

                        await _context.SaveChangesAsync();
                        await dbTx.CommitAsync();

                        var crossedAboveToLow = beforeTotal > reorderLevel && afterTotal <= reorderLevel;
                        var droppedFromEqualToBelow = beforeTotal == reorderLevel && afterTotal < reorderLevel;

                        if (item.IsActive && reorderLevel >= 0 && (crossedAboveToLow || droppedFromEqualToBelow))
                        {
                            shouldAlertAfterCommit = true;
                            stocksForEmail = stocks;
                        }

                        if (shouldAlertAfterCommit)
                        {
                            _logger.LogInformation(
                                "Reorder alert trigger: ItemId={ItemId} SKU={SKU} Before={Before} After={After} Reorder={Reorder}",
                                item.ID, item.SKU, beforeTotal, afterTotal, reorderLevel);

                            try
                            {
                                await SendReorderAlertAsync(item, afterTotal, reorderLevel, stocksForEmail);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Reorder alert email failed for ItemId={ItemId}", item.ID);
                            }
                        }

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

        private async Task SendReorderAlertAsync(InventoryItem item, int qtyNow, int reorderLevel, List<InventoryItemStock> stocks)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in new[] { "Admin", "Manager", "Viewer" })
            {
                var users = await _userManager.GetUsersInRoleAsync(role);
                foreach (var u in users)
                {
                    if (string.IsNullOrWhiteSpace(u.Email)) continue;
                    if (!u.EmailConfirmed) continue;

                    recipients.Add(u.Email);
                }
            }

            if (recipients.Count == 0)
            {
                _logger.LogWarning("Reorder alert: no recipients found (Admin/Manager with confirmed emails). ItemId={ItemId}", item.ID);
                return;
            }

            var locationIds = stocks?
                .Select(s => s.StorageLocationID)
                .Distinct()
                .ToList() ?? new List<int>();

            var locNames = await _context.StorageLocations
                .AsNoTracking()
                .Where(l => locationIds.Contains(l.ID))
                .ToDictionaryAsync(l => l.ID, l => l.Name);

            string LocName(int id) => locNames.TryGetValue(id, out var n) ? n : $"Location #{id}";

            var subject = $"[InvenTrack] Reorder Alert: {item.ItemName} ({item.SKU})";

            var sb = new StringBuilder();
            sb.Append("<div style='font-family:Segoe UI,Arial,sans-serif;line-height:1.5'>");
            sb.Append("<h2 style='margin:0 0 8px 0'>Reorder Alert</h2>");
            sb.Append($"<div><strong>Item:</strong> {System.Net.WebUtility.HtmlEncode(item.ItemName)}</div>");
            sb.Append($"<div><strong>SKU:</strong> {System.Net.WebUtility.HtmlEncode(item.SKU)}</div>");
            sb.Append($"<div><strong>Total Quantity On Hand:</strong> {qtyNow}</div>");
            sb.Append($"<div><strong>Reorder Level:</strong> {reorderLevel}</div>");

            if (stocks != null && stocks.Count > 0)
            {
                sb.Append("<hr style='margin:12px 0'/>");
                sb.Append("<div style='font-weight:600;margin-bottom:6px'>Per-location stock</div>");
                sb.Append("<ul style='margin:0;padding-left:18px'>");

                foreach (var s in stocks.OrderByDescending(x => x.QuantityOnHand))
                {
                    var name = System.Net.WebUtility.HtmlEncode(LocName(s.StorageLocationID));
                    sb.Append($"<li>{name}: {s.QuantityOnHand}</li>");
                }

                sb.Append("</ul>");
            }

            sb.Append("<hr style='margin:12px 0'/>");
            sb.Append("<div style='color:#6c757d;font-size:12px'>This message was generated automatically by InvenTrack.</div>");
            sb.Append("</div>");

            var body = sb.ToString();

            foreach (var email in recipients)
            {
                await _emailSender.SendEmailAsync(email, subject, body);
                _logger.LogInformation("Reorder alert email sent to {Email} for ItemId={ItemId}", email, item.ID);
            }
        }
    }
}