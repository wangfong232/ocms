using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OCMSMVC.Hubs
{
    public class BookingHub : Hub
    {
        public async Task NotifySlotStatus(int slotId, string status)
        {
            await Clients.Others.SendAsync("SlotStatusChanged", new { slotId, status });
        }
    }
}
