using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using OCMSMVC.Filters;
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
        private readonly IAppointmentRepository appointmentRepository;
        private readonly IHubContext<BookingHub> hubContext;
        private readonly IEmailService emailService;
        private readonly ILogger<BookingController> logger;

        public BookingController(
          IBookingService bookingService,
          ICapacityEngine capacityEngine,
          ITimeSlotRepository timeSlotRepository,
          IAppointmentRepository appointmentRepository,
          IHubContext<BookingHub> hubContext,
          IEmailService emailService,
          ILogger<BookingController> logger)
        {
            this.bookingService = bookingService;
            this.capacityEngine = capacityEngine;
            this.timeSlotRepository = timeSlotRepository;
            this.appointmentRepository = appointmentRepository;
            this.hubContext = hubContext;
            this.emailService = emailService;
            this.logger = logger;
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
                if (DateTime.TryParse(heldExpiresAtStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var heldExpiresAt))
                {
                    // Chuyển về UTC trước khi trừ DateTime.UtcNow để tránh lệch múi giờ UTC+7 (tránh bug 429 phút)
                    var diff = (heldExpiresAt.ToUniversalTime() - DateTime.UtcNow).TotalSeconds;
                    if (diff > 600) diff = 600; // Khống chế tối đa không vượt quá 10 phút

                    if (diff > 0)
                    {
                        remainingSeconds = (int)diff;
                    }
                    else
                    {
                        var releasedVer = await bookingService.ReleaseSlotAsync(heldSlotId.Value);
                        await hubContext.Clients.All.SendAsync("SlotStatusChanged", new
                        {
                            slotId = heldSlotId.Value,
                            status = "Open",
                            rowVersion = releasedVer
                        });
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
            if (currentPatientId.HasValue)
            {
                var patient = await appointmentRepository.GetPatientByUserIdAsync(currentPatientId.Value);
                if (patient != null)
                {
                    var existingAppts = await appointmentRepository.GetActiveAppointmentsByPatientAndDateAsync(patient.Id, targetSlot.DoctorSchedule.WorkDate);
                    if (existingAppts.Count >= 3)
                    {
                        return BadRequest(new { success = false, message = "Bạn đã đạt giới hạn tối đa 3 lịch hẹn khám trong ngày này. Vui lòng liên hệ quầy tiếp đón nếu cần khám thêm chuyên khoa." });
                    }

                    if (existingAppts.Any(a => a.Doctor?.DepartmentId == targetSlot.DoctorSchedule.Doctor.DepartmentId))
                    {
                        return BadRequest(new { success = false, message = "Bạn đã có 1 lịch hẹn khám tại chuyên khoa này trong ngày. Mỗi chuyên khoa chỉ được đặt tối đa 1 lần/ngày." });
                    }

                    if (existingAppts.Any(a => a.TimeSlot != null && targetSlot.StartTime < a.TimeSlot.EndTime && targetSlot.EndTime > a.TimeSlot.StartTime))
                    {
                        return BadRequest(new { success = false, message = "Khung giờ này bị trùng thời gian với một lịch hẹn khám khác của bạn trong ngày." });
                    }
                }
            }

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
        [RoleAuthorize("Patient", "Admin")]
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

            var sessionEmail = HttpContext.Session.GetString("Email") ?? "";
            if (sessionEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) ||
                sessionEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
            {
                sessionEmail = ""; // Không điền sẵn email ảo của tài khoản test
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
                PatientPhone = HttpContext.Session.GetString("PhoneNumber") ?? "",
                PatientEmail = sessionEmail
            });
        }

        [HttpPost]
        [RoleAuthorize("Patient", "Admin")]
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

            // Kiểm tra giới hạn số lần đặt lịch trong ngày
            var patient = await appointmentRepository.GetPatientByUserIdAsync(userId.Value);
            if (patient != null)
            {
                var existingAppts = await appointmentRepository.GetActiveAppointmentsByPatientAndDateAsync(patient.Id, slot.DoctorSchedule.WorkDate);
                if (existingAppts.Count >= 3)
                {
                    TempData["ErrorMessage"] = "Bạn đã đạt giới hạn tối đa 3 lịch hẹn khám trong ngày này. Vui lòng liên hệ quầy tiếp đón nếu cần khám thêm chuyên khoa.";
                    return RedirectToAction(nameof(Search));
                }

                if (existingAppts.Any(a => a.Doctor?.DepartmentId == slot.DoctorSchedule.Doctor.DepartmentId))
                {
                    TempData["ErrorMessage"] = "Bạn đã có 1 lịch hẹn khám tại chuyên khoa này trong ngày. Mỗi chuyên khoa chỉ được đặt tối đa 1 lần/ngày.";
                    return RedirectToAction(nameof(Search));
                }

                if (existingAppts.Any(a => a.TimeSlot != null && slot.StartTime < a.TimeSlot.EndTime && slot.EndTime > a.TimeSlot.StartTime))
                {
                    TempData["ErrorMessage"] = "Khung giờ này bị trùng thời gian với một lịch hẹn khám khác của bạn trong ngày.";
                    return RedirectToAction(nameof(Search));
                }
            }

            try
            {
                var appointment = await bookingService.ConfirmBookingAsync(userId.Value, model.SlotId, model.Reason);
                HttpContext.Session.Remove("HeldSlotId");
                HttpContext.Session.Remove("HeldExpiresAt");

                // Broadcast SignalR: slot đã được đặt
                await hubContext.Clients.All.SendAsync("SlotStatusChanged", new { slotId = model.SlotId, status = "Booked" });

                // Gửi vé khám điện tử (E-Ticket) qua Email cho bệnh nhân
                var apptDetails = await bookingService.GetAppointmentDetailsAsync(appointment.Id);
                var recipientEmail = !string.IsNullOrWhiteSpace(model.Email)
                    ? model.Email.Trim()
                    : (!string.IsNullOrWhiteSpace(apptDetails?.Patient?.User?.Email) &&
                       !apptDetails.Patient.User.Email.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) &&
                       !apptDetails.Patient.User.Email.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase)
                        ? apptDetails.Patient.User.Email.Trim()
                        : "");

                bool emailSent = false;
                if (!string.IsNullOrWhiteSpace(recipientEmail) &&
                    !recipientEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase) &&
                    !recipientEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (apptDetails != null)
                        {
                            logger.LogInformation($"[TICKET EMAIL] Đang gửi vé khám tới: {recipientEmail} cho lịch hẹn #{apptDetails.Id}...");
                            string qrToken = apptDetails.QrCodeToken ?? $"APT-{apptDetails.Id}";
                            string subject = $"[OCMS Clinic] Vé khám điện tử (E-Ticket) - Mã: {qrToken}";
                            string body = BuildTicketEmailHtml(apptDetails);

                            await emailService.SendEmailAsync(recipientEmail, subject, body);
                            logger.LogInformation($"[TICKET EMAIL SUCCESS] Đã gửi vé khám thành công tới {recipientEmail}!");
                            emailSent = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, $"[TICKET EMAIL ERROR] Không thể gửi vé khám tới {recipientEmail}: {ex.Message}");
                        TempData["ErrorMessage"] = $"Lỗi khi gửi email vé khám: {ex.Message}";
                    }
                }

                if (emailSent)
                {
                    TempData["SuccessMessage"] = $"Đặt lịch khám thành công! Vé khám điện tử đã được gửi về email: {recipientEmail}.";
                    TempData["TicketEmail"] = recipientEmail;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(recipientEmail))
                    {
                        TempData["SuccessMessage"] = "Đặt lịch khám thành công!";
                        TempData["WarningMessage"] = "Chưa có email nhận vé khám. Quý khách có thể nhập email ở khung bên dưới để nhận vé.";
                    }
                }

                return RedirectToAction(nameof(Success), new { appointmentId = appointment.Id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xác nhận đặt lịch: " + ex.Message;
                return RedirectToAction(nameof(Confirm));
            }
        }

        [HttpPost]
        [RoleAuthorize("Patient", "Admin")]
        public async Task<IActionResult> ResendTicket(int appointmentId, string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập địa chỉ email hợp lệ.";
                return RedirectToAction(nameof(Success), new { appointmentId });
            }

            email = email.Trim();
            if (email.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) || email.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập địa chỉ email thật (Gmail, Outlook...), không dùng email mẫu hệ thống.";
                return RedirectToAction(nameof(Success), new { appointmentId });
            }

            // Chống spam: giới hạn tối đa 3 lần và khoảng cách tối thiểu 60 giây giữa các lần gửi
            var lastSentKey = $"LastResendTicket_{appointmentId}";
            var countKey = $"ResendCount_{appointmentId}";

            var lastSentStr = HttpContext.Session.GetString(lastSentKey);
            if (DateTime.TryParse(lastSentStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastSent))
            {
                var secondsSinceLast = (DateTime.UtcNow - lastSent).TotalSeconds;
                if (secondsSinceLast < 60)
                {
                    int waitSeconds = 60 - (int)secondsSinceLast;
                    TempData["WarningMessage"] = $"Thao tác gửi email quá nhanh. Quý khách vui lòng đợi {waitSeconds} giây nữa trước khi gửi lại.";
                    return RedirectToAction(nameof(Success), new { appointmentId });
                }
            }

            var resendCount = HttpContext.Session.GetInt32(countKey) ?? 0;
            if (resendCount >= 3)
            {
                TempData["ErrorMessage"] = "Bạn đã đạt giới hạn tối đa 3 lần gửi lại vé khám cho lịch hẹn này.";
                return RedirectToAction(nameof(Success), new { appointmentId });
            }

            var apptDetails = await bookingService.GetAppointmentDetailsAsync(appointmentId);
            if (apptDetails == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin lịch hẹn.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                string qrToken = apptDetails.QrCodeToken ?? $"APT-{apptDetails.Id}";
                string subject = $"[OCMS Clinic] Vé khám điện tử (E-Ticket) - Mã: {qrToken}";
                string body = BuildTicketEmailHtml(apptDetails);

                await emailService.SendEmailAsync(email, subject, body);

                // Cập nhật session giới hạn tốc độ
                HttpContext.Session.SetString(lastSentKey, DateTime.UtcNow.ToString("o"));
                HttpContext.Session.SetInt32(countKey, resendCount + 1);

                logger.LogInformation($"[RESEND TICKET SUCCESS] Đã gửi lại vé #{appointmentId} tới {email} (lần {resendCount + 1}/3)");
                TempData["SuccessMessage"] = $"Đã gửi lại vé khám thành công tới email: {email}! Vui lòng kiểm tra hộp thư (kể cả mục Spam/Thư rác).";
                TempData["TicketEmail"] = email;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"[RESEND TICKET ERROR] Lỗi gửi vé tới {email}: {ex.Message}");
                TempData["ErrorMessage"] = $"Lỗi khi gửi email: {ex.Message}";
            }

            return RedirectToAction(nameof(Success), new { appointmentId });
        }

        private string BuildTicketEmailHtml(BusinessObjects.Entities.Appointment apptDetails)
        {
            string qrToken = apptDetails.QrCodeToken ?? $"APT-{apptDetails.Id}";
            string qrImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=180x180&data={qrToken}";
            string patientName = apptDetails.Patient?.User?.FullName ?? "Quý khách";
            string doctorTitle = apptDetails.Doctor?.AcademicTitle ?? "BS.";
            string doctorName = apptDetails.Doctor?.User?.FullName ?? "Bác sĩ phụ trách";
            string deptName = apptDetails.Doctor?.Department?.Name ?? "Đa khoa";
            string roomNumber = apptDetails.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? "101";
            string dateStr = apptDetails.AppointmentDate.ToString("dd/MM/yyyy");

            string startTimeStr = apptDetails.TimeSlot != null ? apptDetails.TimeSlot.StartTime.ToString(@"hh\:mm") : "08:00";
            string endTimeStr = apptDetails.TimeSlot != null ? apptDetails.TimeSlot.EndTime.ToString(@"hh\:mm") : "08:15";
            string priceStr = apptDetails.Doctor != null ? apptDetails.Doctor.PricePerSlot.ToString("N0") : "150,000";

            return $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 10px; overflow: hidden;'>
                <div style='background-color: #0d6efd; color: white; padding: 20px; text-align: center;'>
                    <h2 style='margin: 0;'>PHÒNG KHÁM NGOẠI TRÚ OCMS</h2>
                    <p style='margin: 5px 0 0; opacity: 0.9;'>Vé khám bệnh điện tử (E-Ticket)</p>
                </div>
                <div style='padding: 25px;'>
                    <p>Kính gửi <strong>{patientName}</strong>,</p>
                    <p>Lịch hẹn khám của bạn đã được xác nhận thành công trên hệ thống OCMS Clinic.</p>
                    
                    <div style='text-align: center; margin: 20px 0; padding: 15px; background-color: #f8f9fa; border: 1px dashed #0d6efd; border-radius: 8px;'>
                        <p style='margin: 0 0 10px; font-weight: bold; color: #0d6efd; font-size: 14px;'>MÃ VÉ KHÁM & QUÉT QR CHECK-IN</p>
                        <img src='{qrImageUrl}' alt='Mã QR Vé Khám' width='160' height='160' style='display: block; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; padding: 5px; background: #fff;' />
                        <p style='margin: 8px 0 0; font-family: monospace; font-size: 16px; font-weight: bold; color: #0d6efd;'>{qrToken}</p>
                        <small style='color: #6c757d; font-size: 12px;'>Quý khách xuất trình mã QR này tại Quầy tiếp đón để được quét vào phòng khám</small>
                    </div>

                    <div style='background-color: #f8f9fa; border-left: 4px solid #0d6efd; padding: 15px; margin: 20px 0;'>
                        <p style='margin: 5px 0;'><strong>Mã vé khám (QR Token):</strong> <span style='font-size: 16px; color: #0d6efd; font-weight: bold;'>{qrToken}</span></p>
                        <p style='margin: 5px 0;'><strong>Bác sĩ khám:</strong> {doctorTitle} {doctorName}</p>
                        <p style='margin: 5px 0;'><strong>Chuyên khoa:</strong> {deptName}</p>
                        <p style='margin: 5px 0;'><strong>Phòng khám:</strong> Phòng {roomNumber}</p>
                        <p style='margin: 5px 0;'><strong>Ngày khám:</strong> {dateStr}</p>
                        <p style='margin: 5px 0;'><strong>Khung giờ hẹn:</strong> {startTimeStr} - {endTimeStr}</p>
                        <p style='margin: 5px 0;'><strong>Phí khám niêm yết:</strong> {priceStr} VNĐ</p>
                    </div>

                    <div style='background-color: #fff3cd; border: 1px solid #ffeeba; color: #856404; padding: 12px; border-radius: 6px; margin-bottom: 20px;'>
                        <strong>Lưu ý tiếp đón:</strong> Quý khách vui lòng có mặt trước giờ hẹn 15 phút tại Quầy tiếp đón và xuất trình mã QR này hoặc số điện thoại để làm thủ tục vào phòng khám.
                    </div>

                    <p style='color: #6c757d; font-size: 13px; text-align: center; margin-top: 30px;'>
                        Hệ thống Phòng Khám Đa Khoa OCMS &bull; Hotline: 1900 8888 &bull; Email hỗ trợ: hams.ocms.noreply@gmail.com
                    </p>
                </div>
            </div>";
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