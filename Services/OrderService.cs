using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.ViewModels.Orders;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InvenTrack.Services
{
    public class OrderService
    {
        private readonly InvenTrackContext _context;
        private readonly StockService _stockService;
        private readonly AppAccessService _accessService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly OrderRequestNotificationService _notificationService;
        private readonly InventoryAiService _inventoryAiService;

        public OrderService(
            InvenTrackContext context,
            StockService stockService,
            AppAccessService accessService,
            UserManager<ApplicationUser> userManager,
            OrderRequestNotificationService notificationService,
            InventoryAiService inventoryAiService)
        {
            _context = context;
            _stockService = stockService;
            _accessService = accessService;
            _userManager = userManager;
            _notificationService = notificationService;
            _inventoryAiService = inventoryAiService;
        }

        public async Task<ReorderRequestCreateVM?> BuildCreateVmAsync(int inventoryItemId, ClaimsPrincipal principal)
        {
            var scope = await _accessService.GetScopeAsync(principal);
            var itemQuery = _accessService.ApplyInventoryScope(
                _context.InventoryItems.AsNoTracking().Include(i => i.StorageLocation), scope);

            var item = await itemQuery.FirstOrDefaultAsync(i => i.ID == inventoryItemId);
            if (item == null) return null;

            var stocksQuery = _context.InventoryItemStocks
                .AsNoTracking()
                .Include(x => x.StorageLocation)
                .Where(x => x.InventoryItemID == inventoryItemId);

            if (scope.IsScopedUser)
                stocksQuery = stocksQuery.Where(x => x.StorageLocationID == scope.AssignedLocationId);

            var stocks = await stocksQuery.OrderByDescending(x => x.QuantityOnHand).ToListAsync();
            var visibleQty = stocks.Sum(x => x.QuantityOnHand);
            if (visibleQty == 0 && scope.IsScopedUser && item.StorageLocationID == scope.AssignedLocationId)
                visibleQty = item.QuantityOnHand;

            var requireApproval = scope.IsEmployee || scope.IsSupervisor;
            var locations = await GetAvailableLocationsAsync(scope);
            var destination = scope.IsScopedUser ? scope.AssignedLocationId ?? item.StorageLocationID : item.StorageLocationID;

            var prediction = await _inventoryAiService.GetPredictionAsync(item.ID, scope);
            var aiSuggested = prediction?.SuggestedReorderQuantity ?? 0;
            var riskLocation = prediction?.HighestRiskLocation;
            var riskLocationSuggested = riskLocation?.SuggestedReorderQuantity ?? 0;
            var suggested = Math.Max(Math.Max(aiSuggested, riskLocationSuggested), Math.Max(item.ReorderLevel > 0 ? item.ReorderLevel * 2 - visibleQty : 10, 1));

            var relatedLocationIds = new List<int>();
            if (riskLocation != null)
                relatedLocationIds.Add(riskLocation.StorageLocationId);
            relatedLocationIds.AddRange(stocks.Where(s => s.QuantityOnHand <= item.ReorderLevel).Select(s => s.StorageLocationID));
            relatedLocationIds = relatedLocationIds.Distinct().ToList();

            return new ReorderRequestCreateVM
            {
                InventoryItemID = item.ID,
                ItemName = item.ItemName,
                SKU = item.SKU,
                CurrentVisibleQuantity = visibleQty,
                SuggestedQuantity = suggested,
                RequestedQuantity = suggested,
                ReorderLevel = item.ReorderLevel,
                DestinationStorageLocationID = destination,
                RequiresApproval = requireApproval,
                AiConfidenceLabel = prediction?.ConfidenceLabel ?? "Low",
                AiInsightSummary = riskLocation?.InsightSummary ?? prediction?.InsightSummary ?? "Suggestion is based on visible stock, reorder level, and recent usage.",
                SuggestedByLabel = prediction?.IsPredictionAvailable == true ? "AI-assisted suggestion" : "Rule-based suggestion",
                DestinationHint = scope.IsScopedUser
                    ? $"Stock will be added to your assigned location: {scope.AssignedLocationName ?? item.StorageLocation?.Name ?? "Assigned location"}."
                    : "Choose the destination location that should receive the new stock.",
                AvailableLocations = locations.Select(l => new LocationOptionVM
                {
                    ID = l.ID,
                    Name = l.Name,
                    QuantityOnHand = stocks.FirstOrDefault(s => s.StorageLocationID == l.ID)?.QuantityOnHand ?? 0
                }).ToList(),
                RelatedLocationIds = relatedLocationIds
            };
        }

        public async Task<(bool ok, string? error, int? orderId)> SubmitAsync(ReorderRequestCreateVM vm, ClaimsPrincipal principal)
        {
            var scope = await _accessService.GetScopeAsync(principal);
            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return (false, "User not found.", null);

            var itemQuery = _accessService.ApplyInventoryScope(
                _context.InventoryItems.AsNoTracking().Include(i => i.StorageLocation), scope);
            var item = await itemQuery.FirstOrDefaultAsync(i => i.ID == vm.InventoryItemID);
            if (item == null) return (false, "Inventory item not found.", null);
            if (vm.RequestedQuantity < 1) return (false, "Quantity must be at least 1.", null);

            var allowedLocationIds = (await GetAvailableLocationsAsync(scope)).Select(x => x.ID).ToHashSet();
            if (!allowedLocationIds.Contains(vm.DestinationStorageLocationID))
                return (false, "Destination location is not allowed for your access scope.", null);

            var destinationName = await _context.StorageLocations
                .AsNoTracking()
                .Where(l => l.ID == vm.DestinationStorageLocationID)
                .Select(l => l.Name)
                .FirstOrDefaultAsync() ?? "destination";

            var locationNames = await _context.StorageLocations
                .AsNoTracking()
                .Where(l => vm.RelatedLocationIds.Contains(l.ID))
                .OrderBy(l => l.Name)
                .Select(l => l.Name)
                .ToListAsync();

            var request = new InventoryOrderRequest
            {
                InventoryItemID = item.ID,
                DestinationStorageLocationID = vm.DestinationStorageLocationID,
                RelatedLocationIdsCsv = string.Join(',', vm.RelatedLocationIds.Distinct()),
                RelatedLocationNames = string.Join(", ", locationNames),
                CurrentVisibleQuantity = vm.CurrentVisibleQuantity,
                SuggestedQuantity = vm.SuggestedQuantity,
                RequestedQuantity = vm.RequestedQuantity,
                Notes = string.IsNullOrWhiteSpace(vm.Notes) ? null : vm.Notes.Trim(),
                RequestedByUserId = user.Id,
                RequestedByName = UserDisplayHelper.GetDisplayName(user),
                DateRequested = DateTime.UtcNow,
                RequiresApproval = scope.IsEmployee || scope.IsSupervisor,
                Status = (scope.IsAdmin || scope.IsRegionalManager || scope.IsManager) ? OrderRequestStatus.Completed : OrderRequestStatus.Pending
            };

            _context.InventoryOrderRequests.Add(request);
            await _context.SaveChangesAsync();

            if (!request.RequiresApproval)
            {
                var actor = UserDisplayHelper.GetDisplayName(user);
                var (ok, error) = await _stockService.ApplyTransactionAsync(
                    inventoryItemId: request.InventoryItemID,
                    actionType: StockActionType.CheckIn,
                    quantity: request.RequestedQuantity,
                    toLocationId: request.DestinationStorageLocationID,
                    notes: $"Restock order #{request.ID} for {destinationName}. {request.Notes}".Trim(),
                    performedBy: actor);

                if (!ok) return (false, error ?? "Could not complete reorder.", null);

                var txId = await _context.StockTransactions
                    .AsNoTracking()
                    .Where(t => t.InventoryItemID == request.InventoryItemID && t.ActionType == StockActionType.CheckIn && t.PerformedBy == actor)
                    .OrderByDescending(t => t.ID)
                    .Select(t => t.ID)
                    .FirstOrDefaultAsync();

                request.ReviewedByUserId = user.Id;
                request.ReviewedByName = actor;
                request.DateReviewed = DateTime.UtcNow;
                request.ReviewDecision = "Completed directly";
                request.FulfilledBy = actor;
                request.DateFulfilled = DateTime.UtcNow;
                request.StockTransactionID = txId > 0 ? txId : null;
                await _context.SaveChangesAsync();

                await _notificationService.NotifyChangedAsync($"Restock order #{request.ID} completed for {item.ItemName} to {destinationName}.");
            }
            else
            {
                await _notificationService.NotifyChangedAsync($"Restock request #{request.ID} submitted for {item.ItemName} to {destinationName}.");
            }

            return (true, null, request.ID);
        }

        public async Task<OrderRequestIndexVM> BuildIndexVmAsync(ClaimsPrincipal principal, string? search, string? status)
        {
            var scope = await _accessService.GetScopeAsync(principal);
            var canApprove = scope.IsAdmin || scope.IsRegionalManager || scope.IsManager;

            var query = _context.InventoryOrderRequests
                .AsNoTracking()
                .Include(x => x.InventoryItem)
                .Include(x => x.DestinationStorageLocation)
                .AsQueryable();

            if (scope.IsScopedUser)
                query = query.Where(x => x.DestinationStorageLocationID == scope.AssignedLocationId || x.RequestedByUserId == scope.UserId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(x =>
                    x.InventoryItem.ItemName.Contains(search) ||
                    x.InventoryItem.SKU.Contains(search) ||
                    x.RequestedByName.Contains(search) ||
                    (x.ReviewedByName != null && x.ReviewedByName.Contains(search)) ||
                    x.DestinationStorageLocation.Name.Contains(search) ||
                    (x.RelatedLocationNames != null && x.RelatedLocationNames.Contains(search)));
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderRequestStatus>(status, true, out var parsed))
                query = query.Where(x => x.Status == parsed);

            var rows = await query
                .OrderByDescending(x => x.DateRequested)
                .Select(x => new OrderRequestRowVM
                {
                    ID = x.ID,
                    ItemName = x.InventoryItem.ItemName,
                    SKU = x.InventoryItem.SKU,
                    DestinationLocation = x.DestinationStorageLocation.Name,
                    RelatedLocations = x.RelatedLocationNames ?? string.Empty,
                    CurrentVisibleQuantity = x.CurrentVisibleQuantity,
                    SuggestedQuantity = x.SuggestedQuantity,
                    RequestedQuantity = x.RequestedQuantity,
                    RequestedByName = x.RequestedByName,
                    DateRequested = x.DateRequested,
                    Status = x.Status,
                    ReviewedByName = x.ReviewedByName,
                    DateReviewed = x.DateReviewed,
                    ReviewDecision = x.ReviewDecision,
                    FulfilledBy = x.FulfilledBy,
                    DateFulfilled = x.DateFulfilled,
                    Notes = x.Notes,
                    CanApprove = canApprove && x.Status == OrderRequestStatus.Pending,
                    CanReject = canApprove && x.Status == OrderRequestStatus.Pending
                })
                .ToListAsync();

            return new OrderRequestIndexVM
            {
                Rows = rows,
                Search = search ?? string.Empty,
                StatusFilter = status ?? string.Empty,
                CanApprove = canApprove,
                PendingCount = await PendingCountAsync(scope)
            };
        }

        public async Task<int> PendingCountAsync(AccessScope scope)
        {
            var query = _context.InventoryOrderRequests.AsNoTracking().Where(x => x.Status == OrderRequestStatus.Pending);
            if (scope.IsScopedUser)
                query = query.Where(x => x.DestinationStorageLocationID == scope.AssignedLocationId || x.RequestedByUserId == scope.UserId);
            return await query.CountAsync();
        }

        public async Task<(bool ok, string? error)> ApproveAsync(int id, ClaimsPrincipal principal)
        {
            var scope = await _accessService.GetScopeAsync(principal);
            if (!(scope.IsAdmin || scope.IsRegionalManager || scope.IsManager))
                return (false, "Not authorized.");

            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return (false, "User not found.");

            var request = await _context.InventoryOrderRequests.Include(x => x.InventoryItem).Include(x => x.DestinationStorageLocation).FirstOrDefaultAsync(x => x.ID == id);
            if (request == null) return (false, "Order request not found.");
            if (request.Status != OrderRequestStatus.Pending) return (false, "Only pending requests can be approved.");
            if (scope.IsScopedUser && request.DestinationStorageLocationID != scope.AssignedLocationId) return (false, "Not allowed for this location.");

            var actor = UserDisplayHelper.GetDisplayName(user);
            var (ok, error) = await _stockService.ApplyTransactionAsync(
                inventoryItemId: request.InventoryItemID,
                actionType: StockActionType.CheckIn,
                quantity: request.RequestedQuantity,
                toLocationId: request.DestinationStorageLocationID,
                notes: $"Approved restock request #{request.ID} for {request.DestinationStorageLocation.Name}. {request.Notes}".Trim(),
                performedBy: actor);

            if (!ok) return (false, error ?? "Could not approve restock request.");

            var txId = await _context.StockTransactions
                .AsNoTracking()
                .Where(t => t.InventoryItemID == request.InventoryItemID && t.ActionType == StockActionType.CheckIn && t.PerformedBy == actor)
                .OrderByDescending(t => t.ID)
                .Select(t => t.ID)
                .FirstOrDefaultAsync();

            request.Status = OrderRequestStatus.Completed;
            request.ReviewedByUserId = user.Id;
            request.ReviewedByName = actor;
            request.DateReviewed = DateTime.UtcNow;
            request.ReviewDecision = "Approved";
            request.FulfilledBy = actor;
            request.DateFulfilled = DateTime.UtcNow;
            request.StockTransactionID = txId > 0 ? txId : null;
            await _context.SaveChangesAsync();

            await _notificationService.NotifyChangedAsync($"Restock request #{request.ID} approved for {request.InventoryItem.ItemName}.");
            return (true, null);
        }

        public async Task<(bool ok, string? error)> RejectAsync(int id, ClaimsPrincipal principal)
        {
            var scope = await _accessService.GetScopeAsync(principal);
            if (!(scope.IsAdmin || scope.IsRegionalManager || scope.IsManager)) return (false, "Not authorized.");

            var user = await _userManager.GetUserAsync(principal);
            if (user == null) return (false, "User not found.");

            var request = await _context.InventoryOrderRequests.Include(x => x.InventoryItem).FirstOrDefaultAsync(x => x.ID == id);
            if (request == null) return (false, "Order request not found.");
            if (request.Status != OrderRequestStatus.Pending) return (false, "Only pending requests can be rejected.");
            if (scope.IsScopedUser && request.DestinationStorageLocationID != scope.AssignedLocationId) return (false, "Not allowed for this location.");

            request.Status = OrderRequestStatus.Rejected;
            request.ReviewedByUserId = user.Id;
            request.ReviewedByName = UserDisplayHelper.GetDisplayName(user);
            request.DateReviewed = DateTime.UtcNow;
            request.ReviewDecision = "Rejected";
            await _context.SaveChangesAsync();

            await _notificationService.NotifyChangedAsync($"Restock request #{request.ID} rejected for {request.InventoryItem.ItemName}.");
            return (true, null);
        }

        private async Task<List<StorageLocation>> GetAvailableLocationsAsync(AccessScope scope)
        {
            var query = _context.StorageLocations.AsNoTracking().AsQueryable();
            if (scope.IsScopedUser)
                query = query.Where(x => x.ID == scope.AssignedLocationId);
            return await query.OrderBy(x => x.Name).ToListAsync();
        }
    }
}
