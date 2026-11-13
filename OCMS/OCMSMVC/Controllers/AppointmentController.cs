using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Filters;
using OCMSMVC.Models;
using Services;

namespace OCMSMVC.Controllers
{
    [RoleAuthorize("Patient", "Admin")]
    public class AppointmentController : Controller
    {
        private readonly IAppointmentService appointmentService;
        private readonly ICapacityEngine capacityEngine;
        private readonly IEmailService emailService;
        private readonly ILogger<AppointmentController> logger;

        public AppointmentController(
            IAppointmentService appointmentService,
            ICapacityEngine capacityEngine,
            IEmailService emailService,
            ILogger<AppointmentController> logger)
        {
            this.appointmentService = appointmentService;
            this.capacityEngine = capacityEngine;
            this.emailService = emailService;
            this.logger = logger;
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

            var apptBefore = await appointmentService.GetAppointmentDetailsAsync(model.AppointmentId, userId.Value);
            var (success, message, isRefunded) = await appointmentService.CancelAppointmentAsync(model.AppointmentId, userId.Value, model.CancellationReason);

            if (success)
            {
                TempData["SuccessMessage"] = message;

                // Gửi email xác nhận hủy lịch khám
                if (apptBefore != null)
                {
                    var recipientEmail = apptBefore.Patient?.User?.Email ?? HttpContext.Session.GetString("Email");
                    if (!string.IsNullOrWhiteSpace(recipientEmail) &&
                        !recipientEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) &&
                        !recipientEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string subject = $"[OCMS Clinic] Xác nhận hủy lịch khám - Mã: #{apptBefore.Id}";
                            string patientName = apptBefore.Patient?.User?.FullName ?? "Quý khách";
                            string doctorTitle = apptBefore.Doctor?.AcademicTitle ?? "BS.";
                            string doctorName = apptBefore.Doctor?.User?.FullName ?? "Bác sĩ";
                            string deptName = apptBefore.Doctor?.Department?.Name ?? "Đa khoa";
                            string dateStr = apptBefore.AppointmentDate.ToString("dd/MM/yyyy");
                            string timeStr = apptBefore.TimeSlot != null ? $"{apptBefore.TimeSlot.StartTime.ToString(@"hh\:mm")} - {apptBefore.TimeSlot.EndTime.ToString(@"hh\:mm")}" : "";
                            string refundNotice = isRefunded
                                ? "Lịch khám được hủy trước 24 giờ. Quý khách được hoàn lại 100% tiền đặt cọc theo quy định."
                                : "Lịch khám được hủy dưới 24 giờ trước giờ hẹn. Tiền đặt cọc không được hoàn lại theo quy định.";

                            string reasonNotice = !string.IsNullOrWhiteSpace(model.CancellationReason)
                                ? $"<p style='margin: 5px 0;'><strong>Lý do hủy:</strong> {model.CancellationReason}</p>"
                                : "";

                            string body = $@"
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 10px; overflow: hidden;'>
                                <div style='background-color: #dc3545; color: white; padding: 20px; text-align: center;'>
                                    <h2 style='margin: 0;'>PHÒNG KHÁM NGOẠI TRÚ OCMS</h2>
                                    <p style='margin: 5px 0 0; opacity: 0.9;'>Xác nhận hủy lịch hẹn khám bệnh</p>
                                </div>
                                <div style='padding: 25px;'>
                                    <p>Kính gửi <strong>{patientName}</strong>,</p>
                                    <p>Hệ thống OCMS Clinic đã tiếp nhận và xác nhận việc hủy lịch hẹn khám của bạn như sau:</p>
                                    
                                    <div style='background-color: #f8f9fa; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0;'>
                                        <p style='margin: 5px 0;'><strong>Mã lịch hẹn:</strong> #{apptBefore.Id}</p>
                                        <p style='margin: 5px 0;'><strong>Bác sĩ:</strong> {doctorTitle} {doctorName}</p>
                                        <p style='margin: 5px 0;'><strong>Chuyên khoa:</strong> {deptName}</p>
                                        <p style='margin: 5px 0;'><strong>Ngày khám:</strong> {dateStr}</p>
                                        <p style='margin: 5px 0;'><strong>Khung giờ:</strong> {timeStr}</p>
                                        {reasonNotice}
                                    </div>

                                    <div style='background-color: {(isRefunded ? "#d1e7dd" : "#fff3cd")}; border: 1px solid {(isRefunded ? "#badbcc" : "#ffeeba")}; color: {(isRefunded ? "#0f5132" : "#856404")}; padding: 12px; border-radius: 6px; margin-bottom: 20px;'>
                                        <strong>Chính sách hoàn tiền:</strong> {refundNotice}
                                    </div>

                                    <p style='color: #6c757d; font-size: 13px; text-align: center; margin-top: 30px;'>
                                        Cảm ơn quý khách đã tin tưởng OCMS Clinic &bull; Hotline: 1900 8888 &bull; Email hỗ trợ: hams.ocms.noreply@gmail.com
                                    </p>
                                </div>
                            </div>";

                            await emailService.SendEmailAsync(recipientEmail.Trim(), subject, body);
                            logger.LogInformation($"[CANCEL EMAIL SUCCESS] Đã gửi email xác nhận hủy lịch #{apptBefore.Id} tới {recipientEmail}");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"[CANCEL EMAIL ERROR] Lỗi khi gửi email hủy lịch #{apptBefore.Id}: {ex.Message}");
                        }
                    }
                }
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

                // Gửi email xác nhận đổi lịch hẹn (kèm vé khám và mã QR mới)
                var updatedAppt = await appointmentService.GetAppointmentDetailsAsync(model.AppointmentId, userId.Value);
                if (updatedAppt != null)
                {
                    var recipientEmail = updatedAppt.Patient?.User?.Email ?? HttpContext.Session.GetString("Email");
                    if (!string.IsNullOrWhiteSpace(recipientEmail) &&
                        !recipientEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) &&
                        !recipientEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            string qrToken = updatedAppt.QrCodeToken ?? $"APT-{updatedAppt.Id}";
                            string qrImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=180x180&data={qrToken}";
                            string subject = $"[OCMS Clinic] Cập nhật lịch hẹn khám (Đổi lịch mới) - Mã: {qrToken}";
                            string patientName = updatedAppt.Patient?.User?.FullName ?? "Quý khách";
                            string doctorTitle = updatedAppt.Doctor?.AcademicTitle ?? "BS.";
                            string doctorName = updatedAppt.Doctor?.User?.FullName ?? "Bác sĩ";
                            string deptName = updatedAppt.Doctor?.Department?.Name ?? "Đa khoa";
                            string roomNumber = updatedAppt.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? "101";
                            string dateStr = updatedAppt.AppointmentDate.ToString("dd/MM/yyyy");
                            string startTimeStr = updatedAppt.TimeSlot != null ? updatedAppt.TimeSlot.StartTime.ToString(@"hh\:mm") : "08:00";
                            string endTimeStr = updatedAppt.TimeSlot != null ? updatedAppt.TimeSlot.EndTime.ToString(@"hh\:mm") : "08:15";

                            string body = $@"
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 10px; overflow: hidden;'>
                                <div style='background-color: #0d6efd; color: white; padding: 20px; text-align: center;'>
                                    <h2 style='margin: 0;'>PHÒNG KHÁM NGOẠI TRÚ OCMS</h2>
                                    <p style='margin: 5px 0 0; opacity: 0.9;'>Vé khám bệnh điện tử (E-Ticket) - ĐÃ CẬP NHẬT ĐỔI LỊCH</p>
                                </div>
                                <div style='padding: 25px;'>
                                    <p>Kính gửi <strong>{patientName}</strong>,</p>
                                    <p>Lịch hẹn khám của bạn đã được <strong>đổi sang khung giờ mới thành công</strong> trên hệ thống OCMS Clinic.</p>
                                    
                                    <div style='text-align: center; margin: 20px 0; padding: 15px; background-color: #f8f9fa; border: 1px dashed #0d6efd; border-radius: 8px;'>
                                        <p style='margin: 0 0 10px; font-weight: bold; color: #0d6efd; font-size: 14px;'>MÃ VÉ KHÁM MỚI & QUÉT QR CHECK-IN</p>
                                        <img src='{qrImageUrl}' alt='Mã QR Vé Khám' width='160' height='160' style='display: block; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 6px; padding: 5px; background: #fff;' />
                                        <p style='margin: 8px 0 0; font-family: monospace; font-size: 16px; font-weight: bold; color: #0d6efd;'>{qrToken}</p>
                                        <small style='color: #6c757d; font-size: 12px;'>Quý khách xuất trình mã QR này tại Quầy tiếp đón để làm thủ tục vào phòng khám</small>
                                    </div>

                                    <div style='background-color: #f8f9fa; border-left: 4px solid #0d6efd; padding: 15px; margin: 20px 0;'>
                                        <p style='margin: 5px 0;'><strong>Mã vé khám:</strong> <span style='font-size: 16px; color: #0d6efd; font-weight: bold;'>{qrToken}</span></p>
                                        <p style='margin: 5px 0;'><strong>Bác sĩ khám:</strong> {doctorTitle} {doctorName}</p>
                                        <p style='margin: 5px 0;'><strong>Chuyên khoa:</strong> {deptName}</p>
                                        <p style='margin: 5px 0;'><strong>Phòng khám:</strong> Phòng {roomNumber}</p>
                                        <p style='margin: 5px 0;'><strong>Ngày khám mới:</strong> <strong style='color: #0d6efd;'>{dateStr}</strong></p>
                                        <p style='margin: 5px 0;'><strong>Khung giờ hẹn mới:</strong> <strong style='color: #198754;'>{startTimeStr} - {endTimeStr}</strong></p>
                                    </div>

                                    <div style='background-color: #fff3cd; border: 1px solid #ffeeba; color: #856404; padding: 12px; border-radius: 6px; margin-bottom: 20px;'>
                                        <strong>Lưu ý:</strong> Quý khách đã sử dụng quyền đổi lịch 1 lần duy nhất cho lịch hẹn này. Vui lòng có mặt trước giờ hẹn 15 phút tại Quầy tiếp đón.
                                    </div>

                                    <p style='color: #6c757d; font-size: 13px; text-align: center; margin-top: 30px;'>
                                        Hệ thống Phòng Khám Đa Khoa OCMS &bull; Hotline: 1900 8888 &bull; Email hỗ trợ: hams.ocms.noreply@gmail.com
                                    </p>
                                </div>
                            </div>";

                            await emailService.SendEmailAsync(recipientEmail.Trim(), subject, body);
                            logger.LogInformation($"[RESCHEDULE EMAIL SUCCESS] Đã gửi email đổi lịch #{updatedAppt.Id} tới {recipientEmail}");
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, $"[RESCHEDULE EMAIL ERROR] Lỗi khi gửi email đổi lịch #{updatedAppt.Id}: {ex.Message}");
                        }
                    }
                }

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
