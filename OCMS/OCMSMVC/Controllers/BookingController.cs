using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Repositories;
using Services;
using System.Security.Cryptography.Xml;

namespace OCMSMVC.Controllers
{
    public class BookingController : Controller
    {
        private readonly IBookingService bookingService;
        private readonly ICapacityEngine capacityEngine;
        private readonly ITimeSlotRepository timeSlotRepository;

        public BookingController(
          IBookingService bookingService,
          ICapacityEngine capacityEngine,
          ITimeSlotRepository timeSlotRepository)
        {
            this.bookingService = bookingService;
            this.capacityEngine = capacityEngine;
            this.timeSlotRepository = timeSlotRepository;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Search(int? departmentId, string? term, DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            if (targetDate < DateTime.Today)
            {
                targetDate = DateTime.Today;
            }

            var deparments = await bookingService.GetSpecialtiesAsync();
            var doctors = await bookingService.SearchDoctorAsync(departmentId, term);

            var doctorCards = new List<DoctorCardItem>();

            foreach (var doc in doctors)
            {
                var slots = await timeSlotRepository.GetSlotsByDoctorAndDateAsync(doc.Id, targetDate);

                var morningSlots = slots.Where(s => s.StartTime < new TimeSpan(12, 0, 0));
                var afternoonSlots = slots.Where(s => s.StartTime >= new TimeSpan(12, 0, 0));

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

            var model = new DoctorSearchViewModel
            {
                SelectedDepartmentId = departmentId,
                SearchTerm = term,
                TargetDate = targetDate,
                Departments = deparments,
                DoctorCards = doctorCards
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> SelectSlot(int doctorId, DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            var doctor = await bookingService.GetDoctorWithDaySlotsAsync(doctorId, targetDate);

            if (doctor == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin bác sĩ.";
                return RedirectToAction("Search");
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
                        HttpContext.Session.Remove("HeldSlotId");
                        HttpContext.Session.Remove("HeldExpiresAt");
                        heldSlotId = null;
                    }
                }
            }

            var slotGridItems = slots.Select(s => new SlotGridItem
            {
                SlotId = s.Id,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status,
                RowVersion = Convert.ToBase64String(s.RowVersion),
                IsHeldByCurrentUser = (heldSlotId.HasValue && s.Id == heldSlotId.Value)
            }).ToList();

            var currentHeldSlot = slotGridItems.FirstOrDefault(s => s.IsHeldByCurrentUser);

            var model = new SelectSlotViewModel
            {
                DoctorId = doctor.Id,
                DoctorName = doctor.User.FullName,
                AcademicTitle = doctor.AcademicTitle,
                DepartmentName = doctor.Department.Name,
                PricePerSlot = doctor.PricePerSlot,
                SelectedDate = targetDate,
                MorningSlots = slotGridItems.Where(s => s.StartTime < new TimeSpan(12, 0, 0)).ToList(),
                AfternoonSlots = slotGridItems.Where(s => s.StartTime >= new TimeSpan(12, 0, 0)).ToList(),
                DayCapacity = capacityEngine.CalculateShiftMetrics(slots),
                CurrentHeldSlot = currentHeldSlot,
                RemainingSeconds = remainingSeconds
            };

            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> HoldSlot([FromBody] HoldSlotPostModel request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
            }

            int? currentPatientId = HttpContext.Session.GetInt32("UserId");
            int? previousHeldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            string? releasedRowVersion = null;

            if (previousHeldSlotId.HasValue && previousHeldSlotId.Value != request.SlotId)
            {
                releasedRowVersion = await bookingService.ReleaseSlotAsync(previousHeldSlotId.Value);
            }


            var result = await bookingService.HoldTimeSlotAsync(request.SlotId, request.RowVersion, currentPatientId);

            if (result.IsSuccess)
            {
                HttpContext.Session.SetInt32("HeldSlotId", result.SlotId);
                HttpContext.Session.SetString("HeldExpiresAt", DateTime.UtcNow.AddSeconds(result.ExpiresInSeconds).ToString("o"));

                return Ok(
                new
                {
                    success = true,
                    slotId = result.SlotId,
                    newRowVersion = result.NewRowVersion,       //token mới của khung giờ vừa giữ
                    releasedSlotId = previousHeldSlotId,
                    releasedNewRowVersion = releasedRowVersion, //token mới của khung giờ vừa release
                    expiresIn = result.ExpiresInSeconds,
                    message = result.Message
                }
                );

            }

            //conflict
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
            int? heldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            if (!heldSlotId.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn chưa chọn khung giờ khám hoặc phiên giữ chỗ đã hết hạn.";
                return RedirectToAction("Search");
            }

            var slot = await timeSlotRepository.GetSlotByIdAsync(heldSlotId.Value);
            if (slot == null || slot.Status != "Hold")
            {
                TempData["ErrorMessage"] = "Khung giờ không còn hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction("Search");
            }

            var model = new BookingConfirmViewModel
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
            };
            return View(model);
        }


        [HttpPost]
        public async Task<IActionResult> ConfirmBooking(ConfirmBookingPostModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng nhập. Vui lòng đăng nhập để xác nhận lịch hẹn.";
                return RedirectToAction("Login", "Auth");
            }

            int? heldSlotId = HttpContext.Session.GetInt32("HeldSlotId");
            if (!heldSlotId.HasValue || heldSlotId.Value != model.SlotId)
            {
                TempData["ErrorMessage"] = "Phiên giữ chỗ của bạn đã hết hạn hoặc không hợp lệ. Vui lòng chọn lại.";
                return RedirectToAction("Search");
            }

            try
            {
                var appointment = await bookingService.ConfirmBookingAsync(userId.Value, model.SlotId, model.Reason);
                HttpContext.Session.Remove("HeldSlotId");
                HttpContext.Session.Remove("HeldExpiresAt");

                TempData["SuccessMessage"] = "Đặt lịch khám thành công!";
                return RedirectToAction("Success", new { appointmentId = appointment.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xác nhận đặt lịch: " + ex.Message;
                return RedirectToAction("Confirm");
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

            var model = new BookingSuccessViewModel
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
            };

            return View(model);
        }
    }
}
