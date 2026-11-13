using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace OCMSMVC.Hubs
{
    /// <summary>
    /// PRN222 - Chapter 7: Real-Time Communication with SignalR
    /// Hub quản lý kết nối và điều phối broadcast thời gian thực giữa Bàn gọi số (Staff/Doctor) và Màn hình TV sảnh (Display)
    /// </summary>
    public class QueueHub : Hub
    {
        // Thông báo toàn bộ client cập nhật lại bảng hàng đợi
        public async Task BroadcastQueueUpdate(object? payload = null)
        {
            await Clients.All.SendAsync("QueueUpdated", payload);
        }

        // Thông báo chuông gọi bệnh nhân vào phòng khám cụ thể
        public async Task BroadcastPatientCalled(int queueNumber, string patientName, string roomNumber, string departmentName)
        {
            await Clients.All.SendAsync("PatientCalled", new
            {
                queueNumber,
                patientName,
                roomNumber,
                departmentName
            });
        }
    }
}
