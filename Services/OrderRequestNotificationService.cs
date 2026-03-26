using InvenTrack.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace InvenTrack.Services
{
    public class OrderRequestNotificationService
    {
        private readonly IHubContext<OrderRequestHub> _hubContext;

        public OrderRequestNotificationService(IHubContext<OrderRequestHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public Task NotifyChangedAsync(string message)
        {
            return _hubContext.Clients.All.SendAsync("OrderRequestsChanged", new { message });
        }
    }
}
