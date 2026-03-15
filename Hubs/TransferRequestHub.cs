using InvenTrack.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace InvenTrack.Hubs
{
    [Authorize]
    public class TransferRequestHub : Hub
    {
        public const string GlobalGroup = "transfer-global";

        private readonly UserManager<ApplicationUser> _userManager;

        public TransferRequestHub(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public static string GetLocationGroup(int locationId) => $"transfer-location-{locationId}";

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated != true)
            {
                await base.OnConnectedAsync();
                return;
            }

            if (Context.User.IsInRole(AppRoles.Admin) || Context.User.IsInRole(AppRoles.RegionalManager))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GlobalGroup);
            }

            var user = await _userManager.GetUserAsync(Context.User);
            if (user?.AssignedStorageLocationId != null)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetLocationGroup(user.AssignedStorageLocationId.Value));
            }

            await base.OnConnectedAsync();
        }
    }
}