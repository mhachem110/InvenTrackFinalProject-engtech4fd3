using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InvenTrack.Hubs
{
    [Authorize]
    public class OrderRequestHub : Hub
    {
    }
}
