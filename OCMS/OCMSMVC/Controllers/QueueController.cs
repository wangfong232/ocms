using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCMSMVC.Filters;
using OCMSMVC.Hubs;
using Services;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class QueueController : Controller
    {
        private readonly IReceptionService _receptionService;
        private readonly IHubContext<QueueHub> _hubContext;

        public QueueController(IReceptionService receptionService, IHubContext<QueueHub> hubContext)
        {
            _receptionService = receptionService;
            _hubContext = hubContext;
        }

        private bool RequireClinicPersonnel()
        {
            var role = HttpContext.Session.GetString("Role");
            return role is "Staff" or "Doctor" or "Admin";
        }

        // GET: /Queue/Display (Màn hình TV sảnh chờ - Công khai tại sảnh phòng khám)
        [HttpGet]
        public async Task<IActionResult> Display()
        {
            var boardData = await _receptionService.GetLobbyQueueBoardDataAsync();
            return View(boardData);
        }

        // GET: /Queue/GetLobbyData (API trả JSON để trang TV tự động cập nhật)
        [HttpGet]
        public async Task<IActionResult> GetLobbyData()
        {
            var boardData = await _receptionService.GetLobbyQueueBoardDataAsync();
            return Json(boardData);
        }

        // GET: /Queue/Dispatch (Bảng điều phối gọi số dành cho nhân viên lễ tân / bác sĩ)
        [HttpGet]
        [RoleAuthorize("Staff", "Doctor", "Admin")]
        public async Task<IActionResult> Dispatch()
        {
            var boardData = await _receptionService.GetLobbyQueueBoardDataAsync();
            return View(boardData);
        }

        // POST: /Queue/CallPatient (Bác sĩ/Nhân viên bấm gọi bệnh nhân vào khám)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Staff", "Doctor", "Admin")]
        public async Task<IActionResult> CallPatient(int appointmentId)
        {
            var result = await _receptionService.CallNextPatientAsync(appointmentId);

            if (!result.Success || result.Appointment == null)
            {
                return Json(new { success = false, message = result.Message });
            }

            var apt = result.Appointment;
            var roomNumber = apt.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? "101";
            var patientName = apt.Patient?.User?.FullName ?? "Bệnh nhân";
            var departmentName = apt.Doctor?.Department?.Name ?? "Phòng khám";

            // PRN222 - Chapter 7: Phát thông điệp qua SignalR Hub tới toàn bộ Client (TV Sảnh + Bàn gọi số)
            await _hubContext.Clients.All.SendAsync("PatientCalled", new
            {
                queueNumber = apt.QueueNumber,
                patientName = patientName,
                roomNumber = roomNumber,
                departmentName = departmentName
            });
            await _hubContext.Clients.All.SendAsync("QueueUpdated");

            return Json(new
            {
                success = true,
                message = result.Message,
                queueNumber = apt.QueueNumber,
                patientName = patientName,
                roomNumber = roomNumber
            });
        }

        // POST: /Queue/CompleteConsultation (Hoàn tất ca khám)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RoleAuthorize("Staff", "Doctor", "Admin")]
        public async Task<IActionResult> CompleteConsultation(int appointmentId)
        {
            var result = await _receptionService.CompleteConsultationAsync(appointmentId);

            if (result.Success)
            {
                // PRN222 - Chapter 7: Cập nhật thời gian thực khi hoàn tất ca khám
                await _hubContext.Clients.All.SendAsync("QueueUpdated");
            }

            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
