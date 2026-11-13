using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCMSMVC.Hubs;
using OCMSMVC.Models;
using Repositories;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService bookingService;
        private readonly ICapacityEngine capacityEngine;
        private readonly ITimeSlotRepository timeSlotRepository;
        private readonly IHubContext<BookingHub> hubContext;

        public BookingController(
          IBookingService bookingService,
          ICapacityEngine capacityEngine,
          ITimeSlotRepository timeSlotRepository,
          IHubContext<BookingHub> hubContext)
        {
            this.bookingService = bookingService;
            this.capacityEngine = capacityEngine;
            this.timeSlotRepository = timeSlotRepository;
            this.hubContext = hubContext;
        }

        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> DoctorDirectory(int? specialtyId, int? departmentId, string? term, DateTime? targetDate, DateTime? date, int? doctorId)
        {
            if (doctorId.HasValue && doctorId.Value > 0)
            {
                return RedirectToAction(nameof(SelectSlot), new { doctorId = doctorId.Value, date = targetDate ?? date ?? DateTime.Today });
            }
            return await Search(departmentId ?? specialtyId, term, targetDate ?? date);
        }

        public async Task<IActionResult> Search(int? departmentId, string? term, DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            // Chặn tìm kiếm ngày trong quá khứ
            if (targetDate < DateTime.Today)
            {
                targetDate = DateTime.Today;
                TempData["ErrorMessage"] = "Không thể tìm kiếm lịch khám trong quá khứ. Đã chuyển về ngày hôm nay.";
            }

            var deparments = await bookingService.GetSpecialtiesAsync();
            var doctors = await bookingService.SearchDoctorAsync(departmentId, term);
            var doctorCards = new List<DoctorCardItem>();
            var nowTime = DateTime.Now.TimeOfDay;
            var isToday = targetDate == DateTime.Today;

            foreach (var doc in doctors)
            {
                var slots = await timeSlotRepository.GetSlotsByDoctorAndDateAsync(doc.Id, targetDate);
                var validSlots = isToday ? slots.Where(s => s.StartTime > nowTime).ToList() : slots;

                var morningSlots = validSlots.Where(s => s.StartTime < new TimeSpan(12, 0, 0));
                var afternoonSlots = validSlots.Where(s => s.StartTime >= new TimeSpan(12, 0, 0));

                doctorCards.Add(new DoctorCardItem
                {
                    DoctorId = doc.Id,
                    FullName = doc.User.FullName,
                    AcademicTitle = doc.AcademicTitle,
                    DepartmentName = doc.Department.Name,
                    RoomNumber = doc.DefaultRoom?.RoomNumber ?? "N/A",
                    PricePerSlot = doc.PricePerSlot,
                    MorningCapacity = capacityEngine.CalculateShiftMetrics(morningSlots),
                    AfternoonCapacity = capacityEngine.CalculateShiftMetrics(afternoonSlots)
                });
            }

            return View(new DoctorSearchViewModel
            {
                SelectedDepartmentId = departmentId,
                SearchTerm = term,
                TargetDate = targetDate,
                Departments = deparments,
                DoctorCards = doctorCards
            });
        }

        [HttpGet]
        public async Task<IActionResult> SelectSlot(int doctorId, DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;

            // Chặn URL ngày quá khứ
            if (targetDate < DateTime.Today)
            {
                TempData["ErrorMessage"] = "Không thể đặt lịch khám trong quá khứ. Đã tự động chuyển về ngày hôm nay.";
                return RedirectToAction(nameof(SelectSlot), new { doctorId, date = DateTime.Today.ToString("yyyy-MM-dd") });
            }

            var doctor = await bookingService.GetDoctorWithDaySlotsAsync(doctorId, targetDate);
            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin bác sĩ.";
                return RedirectToAction(nameof(Search));
            }

            var slots = await timeSlotRepository.GetSlotsByDoctorAndDateAsync(doctorId, targetDate);
            int? heldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            string? heldExpiresAtStr = HttpContext.Session.GetString("HeldExpiresAt");
            int remainingSeconds = 0;

            if (heldSlotId.HasValue && !string.IsNullOrEmpty(heldExpiresAtStr))
            {
                if (DateTime.TryParse(heldExpiresAtStr, out var heldExpiresAt))
                {
                    var diff = (heldExpiresAt - DateTime.UtcNow).TotalSeconds;
                    if (diff > 0)
                    {
                        remainingSeconds = (int)diff;
                    }
                    else
                    {
                        await bookingService.ReleaseSlotAsync(heldSlotId.Value);
                        await hubContext.Clients.All.SendAsync("SlotStatusChanged", new { slotId = heldSlotId.Value, status = "Open" });
                        HttpContext.Session.Remove("HeldSlotId");
                        HttpContext.Session.Remove("HeldExpiresAt");
                        heldSlotId = null;
                    }
                }
            }

            var nowTime = DateTime.Now.TimeOfDay;
            var isToday = targetDate == DateTime.Today;

            var slotGridItems = slots.Select(s =>
            {
                bool isPast = isToday && s.StartTime <= nowTime;
                string effectiveStatus = isPast ? "Past" : s.Status;

                return new SlotGridItem
                {
                    SlotId = s.Id,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = effectiveStatus,
                    RowVersion = Convert.ToBase64String(s.RowVersion),
                    IsHeldByCurrentUser = !isPast && (heldSlotId.HasValue && s.Id == heldSlotId.Value)
                };
            }).ToList();

            var currentHeldSlot = slotGridItems.FirstOrDefault(s => s.IsHeldByCurrentUser);

            return View(new SelectSlotViewModel
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.User.FullName,
                AcademicTitle = doctor.AcademicTitle,
                DepartmentName = doctor.Department.Name,
                PricePerSlot = doctor.PricePerSlot,
                SelectedDate = targetDate,
                MorningSlots = slotGridItems.Where(s => s.StartTime < new TimeSpan(12, 0, 0)).ToList(),
                AfternoonSlots = slotGridItems.Where(s => s.StartTime >= new TimeSpan(12, 0, 0)).ToList(),
                DayCapacity = capacityEngine.CalculateShiftMetrics(slots.Where(s => !(isToday && s.StartTime <= nowTime))),
                CurrentHeldSlot = currentHeldSlot,
                RemainingSeconds = remainingSeconds
            });
        }

        [HttpPost]
        public async Task<IActionResult> HoldSlot([FromBody] HoldSlotPostModel request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            // Kiểm tra slot không thuộc ngày hoặc giờ trong quá khứ
            var targetSlot = await timeSlotRepository.GetSlotByIdAsync(request.SlotId);
            if (targetSlot == null)
            {
                return NotFound(new { success = false, message = "Khung giờ không tồn tại." });
            }

            if (targetSlot.DoctorSchedule.WorkDate.Date < DateTime.Today ||
               (targetSlot.DoctorSchedule.WorkDate.Date == DateTime.Today && targetSlot.StartTime <= DateTime.Now.TimeOfDay))
            {
                return BadRequest(new { success = false, message = "Không thể giữ chỗ cho khung giờ đã trôi qua trong quá khứ." });
            }

            int? currentPatientId = HttpContext.Session.GetInt32("UserId");
            int? previousHeldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            string? releasedRowVersion = null;

            if (previousHeldSlotId.HasValue && previousHeldSlotId.Value != request.SlotId)
            {
                releasedRowVersion = await bookingService.ReleaseSlotAsync(previousHeldSlotId.Value);
                // Broadcast SignalR: slot cũ được mở lại kèm RowVersion mới
                await hubContext.Clients.All.SendAsync("SlotStatusChanged", new
                {
                    slotId = previousHeldSlotId.Value,
                    status = "Open",
                    rowVersion = releasedRowVersion
                });
            }

            var result = await bookingService.HoldTimeSlotAsync(request.SlotId, request.RowVersion, currentPatientId);

            if (result.IsSuccess)
            {
                HttpContext.Session.SetInt32("HeldSlotId", result.SlotId);
                HttpContext.Session.SetString("HeldExpiresAt", DateTime.UtcNow.AddSeconds(result.ExpiresInSeconds).ToString("o"));

                // Broadcast SignalR: slot vừa được giữ kèm RowVersion mới
                await hubContext.Clients.All.SendAsync("SlotStatusChanged", new
                {
                    slotId = result.SlotId,
                    status = "Hold",
                    rowVersion = result.NewRowVersion
                });

                return Ok(new
                {
                    success = true,
                    slotId = result.SlotId,
                    newRowVersion = result.NewRowVersion,
                    releasedSlotId = previousHeldSlotId,
                    releasedNewRowVersion = releasedRowVersion,
                    expiresIn = result.ExpiresInSeconds,
                    message = result.Message
                });
            }

            return StatusCode(409, new
            {
                success = false,
                slotId = result.SlotId,
                message = result.Message,
                alternatives = result.AlternativeSlots
            });
        }

        [HttpGet]
        public async Task<IActionResult> Confirm()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để tiếp tục xác nhận thông tin lịch khám.";
                return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action("Confirm", "Booking") });
            }

            int? heldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            if (!heldSlotId.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn chưa chọn khung giờ khám hoặc phiên giữ chỗ đã hết hạn.";
                return RedirectToAction(nameof(Search));
            }

            var slot = await timeSlotRepository.GetSlotByIdAsync(heldSlotId.Value);
            if (slot == null || slot.Status != "Hold")
            {
                TempData["ErrorMessage"] = "Khung giờ không còn hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Search));
            }

            if (slot.DoctorSchedule.WorkDate.Date < DateTime.Today ||
               (slot.DoctorSchedule.WorkDate.Date == DateTime.Today && slot.StartTime <= DateTime.Now.TimeOfDay))
            {
                TempData["ErrorMessage"] = "Khung giờ khám đã trôi qua trong quá khứ. Vui lòng chọn khung giờ khác.";
                return RedirectToAction(nameof(Search));
            }

            return View(new BookingConfirmViewModel
            {
                SlotId = slot.Id,
                DoctorId = slot.DoctorSchedule.DoctorId,
                DoctorName = slot.DoctorSchedule.Doctor.User.FullName,
                AcademicTitle = slot.DoctorSchedule.Doctor.AcademicTitle,
                DepartmentName = slot.DoctorSchedule.Doctor.Department.Name,
                RoomNumber = slot.DoctorSchedule.Room.RoomNumber,
                AppointmentDate = slot.DoctorSchedule.WorkDate,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime,
                ConsultationFee = slot.DoctorSchedule.Doctor.PricePerSlot,
                PatientName = HttpContext.Session.GetString("FullName") ?? "Bệnh nhân",
                PatientPhone = HttpContext.Session.GetString("PhoneNumber") ?? ""
            });
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(ConfirmBookingPostModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng nhập. Vui lòng đăng nhập để xác nhận lịch hẹn.";
                return RedirectToAction("Login", "Auth", new { returnUrl = Url.Action("Confirm", "Booking") });
            }

            int? heldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            if (!heldSlotId.HasValue || heldSlotId.Value != model.SlotId)
            {
                TempData["ErrorMessage"] = "Phiên giữ chỗ của bạn đã hết hạn hoặc không hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction(nameof(Search));
            }

            var slot = await timeSlotRepository.GetSlotByIdAsync(model.SlotId);
            if (slot == null || slot.DoctorSchedule.WorkDate.Date < DateTime.Today ||
               (slot.DoctorSchedule.WorkDate.Date == DateTime.Today && slot.StartTime <= DateTime.Now.TimeOfDay))
            {
                TempData["ErrorMessage"] = "Khung giờ khám đã trôi qua trong quá khứ. Vui lòng chọn khung giờ khác.";
                return RedirectToAction(nameof(Search));
            }

            try
            {
                var appointment = await bookingService.ConfirmBookingAsync(userId.Value, model.SlotId, model.Reason);
                HttpContext.Session.Remove("HeldSlotId");
                HttpContext.Session.Remove("HeldExpiresAt");

                // Broadcast SignalR: slot đã được đặt
                await hubContext.Clients.All.SendAsync("SlotStatusChanged", new { slotId = model.SlotId, status = "Booked" });

                TempData["SuccessMessage"] = "Đặt lịch khám thành công!";
                return RedirectToAction(nameof(Success), new { appointmentId = appointment.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xác nhận đặt lịch: " + ex.Message;
                return RedirectToAction(nameof(Confirm));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Success(int appointmentId)
        {
            var appt = await bookingService.GetAppointmentDetailsAsync(appointmentId);
            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lịch hẹn.";
                return RedirectToAction("Index", "Home");
            }

            return View(new BookingSuccessViewModel
            {
                AppointmentId = appt.Id,
                QrCodeToken = appt.QrCodeToken ?? $"APP-{appt.Id}",
                DoctorName = appt.Doctor.User.FullName,
                AcademicTitle = appt.Doctor.AcademicTitle,
                DepartmentName = appt.Doctor.Department.Name,
                RoomNumber = appt.TimeSlot.DoctorSchedule.Room.RoomNumber,
                AppointmentDate = appt.AppointmentDate,
                StartTime = appt.TimeSlot.StartTime,
                EndTime = appt.TimeSlot.EndTime,
                ConsultationFee = appt.Doctor.PricePerSlot,
                PatientName = appt.Patient.User.FullName,
                PatientPhone = appt.Patient.User.PhoneNumber ?? "",
                Reason = appt.Reason,
                Status = appt.Status
            });
        }
    }
}