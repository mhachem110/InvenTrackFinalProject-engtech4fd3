using InvenTrack.Data;
using InvenTrack.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace InvenTrack.Services
{
    public class AppAccessService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly InvenTrackContext _context;

        public AppAccessService(UserManager<ApplicationUser> userManager, InvenTrackContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<AccessScope> GetScopeAsync(ClaimsPrincipal principal)
        {
            var userId = _userManager.GetUserId(principal);
            if (string.IsNullOrWhiteSpace(userId))
                throw new InvalidOperationException("User is not authenticated.");

            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new InvalidOperationException("Authenticated user record was not found.");

            var roles = await _userManager.GetRolesAsync(user);

            string? assignedLocationName = null;

            if (user.AssignedStorageLocationId.HasValue)
            {
                assignedLocationName = await _context.StorageLocations
                    .AsNoTracking()
                    .Where(l => l.ID == user.AssignedStorageLocationId.Value)
                    .Select(l => l.Name)
                    .FirstOrDefaultAsync();
            }

            var scope = new AccessScope
            {
                UserId = user.Id,
                IsAdmin = roles.Contains(AppRoles.Admin),
                IsRegionalManager = roles.Contains(AppRoles.RegionalManager),
                IsManager = roles.Contains(AppRoles.Manager),
                IsSupervisor = roles.Contains(AppRoles.Supervisor),
                IsEmployee = roles.Contains(AppRoles.Employee),
                AssignedLocationId = user.AssignedStorageLocationId,
                AssignedLocationName = assignedLocationName
            };

            if (scope.IsScopedUser && !scope.AssignedLocationId.HasValue)
            {
                throw new InvalidOperationException("Scoped users must have an assigned storage location.");
            }

            return scope;
        }

        public IQueryable<InventoryItem> ApplyInventoryScope(IQueryable<InventoryItem> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;

            return query.Where(i =>
                i.StorageLocationID == locationId ||
                i.InventoryItemStocks.Any(s => s.StorageLocationID == locationId));
        }

        public IQueryable<StockTransaction> ApplyTransactionScope(IQueryable<StockTransaction> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;

            return query.Where(t =>
                t.FromStorageLocationID == locationId ||
                t.ToStorageLocationID == locationId);
        }

        public IQueryable<StorageLocation> ApplyLocationScope(IQueryable<StorageLocation> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;
            return query.Where(l => l.ID == locationId);
        }

        public IQueryable<Category> ApplyCategoryScope(IQueryable<Category> query, AccessScope scope)
        {
            return query;
        }

        public IQueryable<StockTransferRequest> ApplyTransferRequestScope(IQueryable<StockTransferRequest> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;

            return query.Where(r =>
                r.FromStorageLocationID == locationId ||
                r.ToStorageLocationID == locationId);
        }

        public bool CanTouchLocation(AccessScope scope, int locationId)
        {
            return scope.HasGlobalLocationAccess || scope.AssignedLocationId == locationId;
        }

        public bool CanTouchEitherLocation(AccessScope scope, int fromLocationId, int toLocationId)
        {
            return scope.HasGlobalLocationAccess ||
                   scope.AssignedLocationId == fromLocationId ||
                   scope.AssignedLocationId == toLocationId;
        }

        public bool CanTouchItemPrimaryLocation(AccessScope scope, InventoryItem item)
        {
            return scope.HasGlobalLocationAccess || scope.AssignedLocationId == item.StorageLocationID;
        }
    }
}