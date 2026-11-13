using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;

namespace OCMSMVC.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly IAppointmentService appointmentService;
        private readonly ICapacityEngine capacityEngine;
        public AppointmentController(IAppointmentService appointmentService, ICapacityEngine capacityEngine)
        {
            this.appointmentService = appointmentService;
            this.capacityEngine = capacityEngine;
        }

        [HttpGet]
        [Route("Appointment/MyAppointments")]
        [Route("Patient/Appointments")]
        public async Task<IActionResult> MyAppointments()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem danh sách lịch hẹn của bạn.";
                return RedirectToAction("Login", "Auth");
            }

            var appointments = await appointmentService.GetPatientAppointmentsAsync(userId.Value);
            var now = DateTime.Now;

            var items = appointments.Select(a => new AppointmentItemViewModel
            {
                AppointmentId = a.Id,
                DoctorId = a.DoctorId,
                DoctorName = a.Doctor?.User?.FullName ?? "Bác sĩ",
                AcademicTitle = a.Doctor?.AcademicTitle ?? "",
                DepartmentName = a.Doctor?.Department?.Name ?? "Đa khoa",
                RoomNumber = a.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? a.Doctor?.DefaultRoom?.RoomNumber ?? "N/A",
                AppointmentDate = a.AppointmentDate,
                StartTime = a.TimeSlot?.StartTime ?? TimeSpan.Zero,
                EndTime = a.TimeSlot?.EndTime ?? TimeSpan.Zero,
                ConsultationFee = a.Doctor?.PricePerSlot ?? 0,
                Status = a.Status,
                Reason = a.Reason,
                CancellationReason = a.CancellationReason,
                QrCodeToken = a.QrCodeToken ?? $"APP-{a.Id}",
                QueueNumber = a.QueueNumber,
                RescheduledCount = a.RescheduledCount
            }).ToList();

            var upcoming = items.Where(a => (a.Status == "Confirmed" || a.Status == "Pending") && a.AppointmentDate.Date.Add(a.StartTime) >= DateTime.Now).OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToList();

            var past = items.Where(a => !((a.Status == "Confirmed" || a.Status == "Pending") && a.AppointmentDate.Date.Add(a.StartTime) >= now))
                .OrderByDescending(a => a.AppointmentDate)
                .ThenByDescending(a => a.StartTime)
                .ToList();

            var model = new MyAppointmentsViewModel
            {
                PatientFullName = HttpContext.Session.GetString("FullName") ?? "Bệnh nhân",
                UpcomingAppointments = upcoming,
                PastAppointments = past
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(CancelAppointmentPostModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thực hiện chức năng này.";
                return RedirectToAction("Login", "Auth");
            }
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu yêu cầu hủy lịch không hợp lệ.";
                return RedirectToAction("MyAppointments");
            }

            var (success, message, isRefunded) = await appointmentService.CancelAppointmentAsync(model.AppointmentId, userId.Value, model.CancellationReason);

            if (success)
            {
                TempData["SuccessMessage"] = message;
            }
            else
            {
                TempData["ErrorMessage"] = message;
            }
            return RedirectToAction("MyAppointments");
        }

        [HttpGet]
        public async Task<IActionResult> Reschedule(int id, DateTime? date)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đổi lịch khám.";
                return RedirectToAction("Login", "Auth");
            }

            var appt = await appointmentService.GetAppointmentDetailsAsync(id, userId.Value);
            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lịch hẹn hoặc bạn không có quyền truy cập.";
                return RedirectToAction("MyAppointments");
            }

            if (appt.RescheduledCount >= 1)
            {
                TempData["ErrorMessage"] = "Lịch hẹn này đã được đổi lịch 1 lần. Theo quy định, mỗi lịch hẹn chỉ được đổi lịch tối đa 1 lần.";
                return RedirectToAction("MyAppointments");
            }

            if (appt.Status != "Confirmed" && appt.Status != "Pending")
            {
                TempData["ErrorMessage"] = $"Không thể đổi lịch cho lịch hẹn có trạng thái '{appt.Status}'.";
                return RedirectToAction("MyAppointments");
            }

            var apptDateTime = appt.AppointmentDate.Date.Add(appt.TimeSlot.StartTime);
            if (apptDateTime <= DateTime.Now)
            {
                TempData["ErrorMessage"] = "Không thể đổi lịch hẹn cho khung giờ đã qua.";
                return RedirectToAction("MyAppointments");
            }

            var targetDate = date?.Date ?? appt.AppointmentDate.Date;
            if (targetDate < DateTime.Today)
            {
                targetDate = DateTime.Today;
            }

            var slots = await appointmentService.GetAvailableSlotsForDoctorAndDateAsync(appt.DoctorId, targetDate);

            var slotGridItems = slots.Select(s => new SlotGridItem
            {
                SlotId = s.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status,
                RowVersion = Convert.ToBase64String(s.RowVersion),
                IsHeldByCurrentUser = (s.Id == appt.TimeSlotId)
            }
            ).ToList();

            var model = new RescheduleViewModel
            {
                AppointmentId = appt.Id,
                DoctorId = appt.DoctorId,
                DoctorName = appt.Doctor?.User?.FullName ?? "Bác sĩ",
                AcademicTitle = appt.Doctor?.AcademicTitle ?? "",
                DepartmentName = appt.Doctor?.Department?.Name ?? "Đa khoa",
                RoomNumber = appt.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? appt.Doctor?.DefaultRoom?.RoomNumber ?? "N/A",
                PricePerSlot = appt.Doctor?.PricePerSlot ?? 0,
                CurrentAppointmentDate = appt.AppointmentDate,
                CurrentStartTime = appt.TimeSlot?.StartTime ?? TimeSpan.Zero,
                CurrentEndTime = appt.TimeSlot?.EndTime ?? TimeSpan.Zero,
                CurrentSlotId = appt.TimeSlotId,
                RescheduledCount = appt.RescheduledCount,
                SelectedDate = targetDate,
                MorningSlots = slotGridItems.Where(s => s.StartTime < new TimeSpan(12, 0, 0)).ToList(),
                AfternoonSlots = slotGridItems.Where(s => s.StartTime >= new TimeSpan(12, 0, 0)).ToList(),
                DayCapacity = capacityEngine.CalculateShiftMetrics(slots)
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmReschedule(ConfirmReschedulePostModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để đổi lịch khám.";
                return RedirectToAction("Login", "Auth");
            }
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một khung giờ mới.";
                return RedirectToAction("Reschedule", new { id = model.AppointmentId });
            }
            var (success, message) = await appointmentService.RescheduleAppointmentAsync(model.AppointmentId, userId.Value, model.NewSlotId);
            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("MyAppointments");
            }
            TempData["ErrorMessage"] = message;
            return RedirectToAction("Reschedule", new { id = model.AppointmentId });
        }

        [HttpGet]
        public async Task<IActionResult> Ticket(int id)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem vé khám.";
                return RedirectToAction("Login", "Auth");
            }
            var appt = await appointmentService.GetAppointmentDetailsAsync(id, userId.Value);
            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lịch hẹn.";
                return RedirectToAction("MyAppointments");
            }
            var model = new AppointmentDetailViewModel
            {
                AppointmentId = appt.Id,
                QrCodeToken = appt.QrCodeToken ?? $"APP-{appt.Id}",
                DoctorName = appt.Doctor?.User?.FullName ?? "Bác sĩ",
                AcademicTitle = appt.Doctor?.AcademicTitle ?? "",
                DepartmentName = appt.Doctor?.Department?.Name ?? "Đa khoa",
                RoomNumber = appt.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? appt.Doctor?.DefaultRoom?.RoomNumber ?? "N/A",
                AppointmentDate = appt.AppointmentDate,
                StartTime = appt.TimeSlot?.StartTime ?? TimeSpan.Zero,
                EndTime = appt.TimeSlot?.EndTime ?? TimeSpan.Zero,
                ConsultationFee = appt.Doctor?.PricePerSlot ?? 0,
                PatientName = appt.Patient?.User?.FullName ?? HttpContext.Session.GetString("FullName") ?? "Bệnh nhân",
                PatientPhone = appt.Patient?.User?.PhoneNumber ?? "",
                Reason = appt.Reason,
                CancellationReason = appt.CancellationReason,
                Status = appt.Status,
                QueueNumber = appt.QueueNumber,
                RescheduledCount = appt.RescheduledCount,
                CreatedAt = appt.CreatedAt
            };
            return View(model);
        }
    }
}
