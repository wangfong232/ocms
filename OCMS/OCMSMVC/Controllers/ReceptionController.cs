using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class ReceptionController : Controller
    {
        private readonly IReceptionService _receptionService;
        private readonly OcmsDbContext _context;

        public ReceptionController(IReceptionService receptionService, OcmsDbContext context)
        {
            _receptionService = receptionService;
            _context = context;
        }

        // GET: /Reception/CheckIn
        [HttpGet]
        public async Task<IActionResult> CheckIn(int? appointmentId, string? message, string? messageType)
        {
            Appointment? appointment = null;
            if (appointmentId.HasValue)
            {
                appointment = await _receptionService.GetAppointmentByIdAsync(appointmentId.Value);
            }

            // Lấy danh sách cuộc hẹn hôm nay
            var todayAppointments = await _receptionService.GetTodayAppointmentsAsync();

            ViewBag.Message = message;
            ViewBag.MessageType = messageType;
            ViewBag.TodayAppointments = todayAppointments;

            return View(appointment);
        }

        // GET: /Reception/ResetDemo
        [HttpGet]
        public IActionResult ResetDemo()
        {
            DbInitializer.ResetDemoData(_context);
            return RedirectToAction(nameof(CheckIn), new
            {
                message = "Đã làm mới dữ liệu về trạng thái ban đầu để demo lại!",
                messageType = "success"
            });
        }

        // POST: /Reception/Search
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return RedirectToAction(nameof(CheckIn), new
                {
                    message = "Vui lòng nhập mã QR vé hoặc số điện thoại.",
                    messageType = "warning"
                });
            }

            var appointment = await _receptionService.FindAppointmentAsync(query);

            if (appointment == null)
            {
                return RedirectToAction(nameof(CheckIn), new
                {
                    message = $"Không tìm thấy lịch hẹn nào cho mã/SĐT: {query}",
                    messageType = "danger"
                });
            }

            return RedirectToAction(nameof(CheckIn), new { appointmentId = appointment.Id });
        }

        // POST: /Reception/ConfirmCheckIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmCheckIn(
            int appointmentId,
            int systolicBP,
            int diastolicBP,
            int pulseRate,
            int spO2,
            decimal temperature,
            decimal? weight,
            decimal? height)
        {
            var result = await _receptionService.CheckInAsync(
                appointmentId,
                systolicBP,
                diastolicBP,
                pulseRate,
                spO2,
                temperature,
                weight,
                height
            );

            return RedirectToAction(nameof(CheckIn), new
            {
                appointmentId = result.Appointment?.Id ?? appointmentId,
                message = result.Message,
                messageType = result.Success ? "success" : "danger"
            });
        }

        // GET: /Reception/GetTodayAppointments (API cập nhật danh sách hôm nay)
        [HttpGet]
        public async Task<IActionResult> GetTodayAppointments()
        {
            var list = await _receptionService.GetTodayAppointmentsAsync();

            var data = list.Select(a => new
            {
                id = a.Id,
                qrCodeToken = a.QrCodeToken ?? $"OCMS-APT-{a.Id}",
                patientName = a.Patient?.User?.FullName ?? "N/A",
                phoneNumber = a.Patient?.User?.PhoneNumber ?? "N/A",
                doctorName = a.Doctor?.User?.FullName ?? "N/A",
                roomNumber = a.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? "101",
                appointmentTime = a.AppointmentDate.ToString("HH:mm"),
                status = a.Status,
                queueNumber = a.QueueNumber
            });

            return Json(data);
        }

        // GET: /Reception/WalkIn (Giao diện đăng ký khám vãng lai)
        [HttpGet]
        public async Task<IActionResult> WalkIn()
        {
            ViewBag.Doctors = await _receptionService.GetAvailableDoctorsTodayAsync();
            return View(new WalkInViewModel());
        }

        // GET: /Reception/GetDoctorSlots?doctorId=X (Lấy danh sách khung giờ trống của bác sĩ)
        [HttpGet]
        public async Task<IActionResult> GetDoctorSlots(int doctorId)
        {
            var slots = await _receptionService.GetOpenSlotsForDoctorTodayAsync(doctorId);
            var data = slots.Select(s => new
            {
                id = s.Id,
                time = $"{s.StartTime:hh\\:mm} - {s.EndTime:hh\\:mm}",
                roomNumber = s.DoctorSchedule?.Room?.RoomNumber ?? "101"
            });
            return Json(data);
        }

        // POST: /Reception/WalkIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WalkIn(WalkInViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Doctors = await _receptionService.GetAvailableDoctorsTodayAsync();
                return View(model);
            }

            var result = await _receptionService.RegisterWalkInAsync(
                model.FullName,
                model.PhoneNumber,
                model.Gender,
                model.DateOfBirth,
                model.Address,
                model.DoctorId,
                model.TimeSlotId,
                model.IsEmergency,
                model.Reason
            );

            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                ViewBag.Doctors = await _receptionService.GetAvailableDoctorsTodayAsync();
                return View(model);
            }

            return RedirectToAction(nameof(CheckIn), new
            {
                appointmentId = result.Appointment?.Id,
                message = result.Message,
                messageType = "success"
            });
        }
    }
}
