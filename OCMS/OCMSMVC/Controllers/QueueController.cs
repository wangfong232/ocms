using Microsoft.AspNetCore.Mvc;
using Services;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class QueueController : Controller
    {
        private readonly IReceptionService _receptionService;

        public QueueController(IReceptionService receptionService)
        {
            _receptionService = receptionService;
        }

        // GET: /Queue/Display (Màn hình TV sảnh chờ)
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

        // GET: /Queue/Dispatch (Bảng điều phối gọi số dành cho nhân viên / bác sĩ)
        [HttpGet]
        public async Task<IActionResult> Dispatch()
        {
            var boardData = await _receptionService.GetLobbyQueueBoardDataAsync();
            return View(boardData);
        }

        // POST: /Queue/CallPatient (Bác sĩ/Nhân viên bấm gọi bệnh nhân vào khám)
        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public async Task<IActionResult> CompleteConsultation(int appointmentId)
        {
            var result = await _receptionService.CompleteConsultationAsync(appointmentId);
            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
