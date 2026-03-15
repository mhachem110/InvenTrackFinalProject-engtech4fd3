using InvenTrack.Hubs;
using InvenTrack.Models;
using Microsoft.AspNetCore.SignalR;

namespace InvenTrack.Services
{
    public class TransferRequestNotificationService
    {
        private readonly IHubContext<TransferRequestHub> _hubContext;

        public TransferRequestNotificationService(IHubContext<TransferRequestHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyTransferRequestChangedAsync(StockTransferRequest request, string message)
        {
            var payload = new
            {
                requestId = request.ID,
                status = request.Status.ToString(),
                fromLocationId = request.FromStorageLocationID,
                toLocationId = request.ToStorageLocationID,
                message = message
            };

            var tasks = new List<Task>
            {
                _hubContext.Clients.Group(TransferRequestHub.GlobalGroup)
                    .SendAsync("TransferRequestsChanged", payload),

                _hubContext.Clients.Group(TransferRequestHub.GetLocationGroup(request.FromStorageLocationID))
                    .SendAsync("TransferRequestsChanged", payload)
            };

            if (request.ToStorageLocationID != request.FromStorageLocationID)
            {
                tasks.Add(
                    _hubContext.Clients.Group(TransferRequestHub.GetLocationGroup(request.ToStorageLocationID))
                        .SendAsync("TransferRequestsChanged", payload));
            }

            await Task.WhenAll(tasks);
        }
    }
}