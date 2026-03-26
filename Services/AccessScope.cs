namespace InvenTrack.Services
{
    public class AccessScope
    {
        public string UserId { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
        public bool IsRegionalManager { get; set; }
        public bool IsManager { get; set; }
        public bool IsSupervisor { get; set; }
        public bool IsEmployee { get; set; }

        public int? AssignedLocationId { get; set; }
        public string? AssignedLocationName { get; set; }

        public bool HasGlobalLocationAccess => IsAdmin || IsRegionalManager;
        public bool CanManageUsers => IsAdmin;

        public bool CanManageReferenceData => IsAdmin || IsRegionalManager;

        public bool CanCreateEditInventory => IsAdmin || IsRegionalManager || IsManager || IsSupervisor;
        public bool CanDeleteInventory => IsAdmin || IsRegionalManager || IsManager || IsSupervisor;

        public bool CanCreateDirectTransactions => IsAdmin || IsRegionalManager || IsManager || IsSupervisor;
        public bool CanConfirmTransfers => IsAdmin || IsRegionalManager || IsManager;
        public bool CanRequestTransfers => IsAdmin || IsRegionalManager || IsManager || IsSupervisor || IsEmployee;
        public bool CanCreateDirectOrders => IsAdmin || IsRegionalManager || IsManager;
        public bool CanRequestOrders => IsAdmin || IsRegionalManager || IsManager || IsSupervisor || IsEmployee;
        public bool CanApproveOrders => IsAdmin || IsRegionalManager || IsManager;

        public bool IsScopedUser => !HasGlobalLocationAccess;
    }
}