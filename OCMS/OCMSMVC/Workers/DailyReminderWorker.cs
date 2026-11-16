using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OCMSMVC.Workers
{
    public class DailyReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyReminderWorker> _logger;

        // Lưu danh sách ID các hồ sơ bệnh án đã gửi email nhắc tái khám trong phiên chạy để chống gửi lặp lại (spam)
        private static readonly HashSet<int> _sentFollowUpRecordIds = new();

        // =========================================================================================
        // [CẤU HÌNH BẬT / TẮT CHẾ ĐỘ DEMO & TEST NGAY LẬP TỨC]
        //
        // - ĐẶT true:  Worker chạy quét tự động mỗi 1 phút/lần (tiện lợi để demo nhận email ngay lập tức).
        // - ĐẶT false: Worker chạy đúng chuẩn thực tế (chỉ quét vào đúng 08:00 sáng hàng ngày).
        // =========================================================================================
        private static bool IsDemoTestingMode = true;

        public DailyReminderWorker(IServiceProvider serviceProvider, ILogger<DailyReminderWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation($"[DAILY REMINDER WORKER] Đã khởi động. Chế độ Demo/Test: {(IsDemoTestingMode ? "BẬT (Quét mỗi phút)" : "TẮT (Chạy 8h00 sáng)")}.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;

                if (IsDemoTestingMode)
                {
                    // Chế độ DEMO: Quét kiểm tra gửi thông báo ngay lập tức, lặp lại sau mỗi 1 phút
                    await RunAllRemindersAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
                else
                {
                    // Chế độ THỰC TẾ: Chỉ chạy vào đúng khung giờ 08:00 sáng hàng ngày
                    if (now.Hour == 8 && now.Minute == 0)
                    {
                        await RunAllRemindersAsync(stoppingToken);
                        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                }
            }
        }

        private async Task RunAllRemindersAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<OcmsDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                // 1. Gửi email nhắc lịch hẹn khám ngày mai (Upcoming Appointments)
                await ProcessUpcomingAppointmentRemindersAsync(dbContext, emailService, stoppingToken);

                // 2. Gửi email nhắc lịch hẹn tái khám (Follow-up Reminders)
                await ProcessFollowUpRemindersAsync(dbContext, emailService, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DAILY REMINDER WORKER] Có lỗi xảy ra trong quá trình quét gửi email nhắc nhở.");
            }
        }

        // =========================================================================================
        // 1. TỰ ĐỘNG GỬI EMAIL NHẮC LỊCH HẸN KHÁM ĐÃ ĐẶT (TRƯỚC 1 NGÀY)
        // =========================================================================================
        private async Task ProcessUpcomingAppointmentRemindersAsync(OcmsDbContext dbContext, IEmailService emailService, CancellationToken stoppingToken)
        {
            var tomorrow = DateTime.Today.AddDays(1);

            var upcomingAppointments = await dbContext.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot)
                .Where(a => a.Status == "Confirmed" && a.AppointmentDate.Date == tomorrow)
                .ToListAsync(stoppingToken);

            if (upcomingAppointments.Any())
            {
                foreach (var appt in upcomingAppointments)
                {
                    var recipientEmail = appt.Patient?.User?.Email;
                    if (string.IsNullOrWhiteSpace(recipientEmail) ||
                        recipientEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) ||
                        recipientEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var subject = "[OCMS Clinic] Nhắc nhở lịch hẹn khám ngày mai";
                    var timeSlotStr = appt.TimeSlot != null
                        ? $"{appt.TimeSlot.StartTime:hh\\:mm} - {appt.TimeSlot.EndTime:hh\\:mm}"
                        : appt.AppointmentDate.ToString("HH:mm");

                    var body = $@"
                        <div style='font-family: Arial, sans-serif; border: 1px solid #e0e0e0; padding: 25px; border-radius: 12px; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                            <div style='text-align: center; border-bottom: 2px solid #0D6EFD; padding-bottom: 15px; margin-bottom: 20px;'>
                                <h2 style='color: #0D6EFD; margin: 0;'>PHÒNG KHÁM ĐA KHOA OCMS</h2>
                                <p style='color: #6c757d; margin: 5px 0 0 0; font-size: 14px;'>Thông báo nhắc nhở lịch hẹn khám bệnh</p>
                            </div>
                            <p style='font-size: 15px;'>Kính gửi <strong>{appt.Patient?.User?.FullName}</strong>,</p>
                            <p style='font-size: 14px; line-height: 1.6;'>Hệ thống xin trân trọng nhắc nhở bạn có một lịch hẹn khám bệnh tại phòng khám vào ngày mai:</p>
                            <div style='background-color: #f8f9fa; border-left: 4px solid #0D6EFD; padding: 15px; margin: 15px 0; border-radius: 4px;'>
                                <p style='margin: 5px 0;'><strong>Mã vé khám:</strong> <span style='color: #0D6EFD; font-family: monospace; font-size: 15px;'>{appt.QrCodeToken ?? $"APT-{appt.Id}"}</span></p>
                                <p style='margin: 5px 0;'><strong>Ngày khám:</strong> {appt.AppointmentDate:dd/MM/yyyy}</p>
                                <p style='margin: 5px 0;'><strong>Khung giờ:</strong> {timeSlotStr}</p>
                                <p style='margin: 5px 0;'><strong>Bác sĩ khám:</strong> {appt.Doctor?.AcademicTitle} {appt.Doctor?.User?.FullName}</p>
                                <p style='margin: 5px 0;'><strong>Chuyên khoa:</strong> {appt.Doctor?.Department?.Name}</p>
                            </div>
                            <p style='font-size: 13px; color: #856404; background-color: #fff3cd; padding: 10px; border-radius: 6px;'>
                                ⚠️ <strong>Lưu ý:</strong> Quý khách vui lòng có mặt trước giờ hẹn 15 phút tại Quầy tiếp đón và xuất trình mã vé khám để làm thủ tục.
                            </p>
                            <hr style='border: none; border-top: 1px solid #eeeeee; margin: 20px 0;' />
                            <p style='font-size: 13px; color: #6c757d; margin: 0;'>Phòng Khám Đa Khoa OCMS • Hotline: 1900 8888 • Email: hams.ocms.noreply@gmail.com</p>
                        </div>";

                    await emailService.SendEmailAsync(recipientEmail, subject, body);
                    _logger.LogInformation($"[APPOINTMENT REMINDER] Đã gửi email nhắc hẹn ngày mai tới: {recipientEmail}");
                }
            }
        }

        // =========================================================================================
        // 2. TỰ ĐỘNG GỬI EMAIL THÔNG BÁO / NHẮC LỊCH HẸN TÁI KHÁM (FOLLOW-UP)
        // =========================================================================================
        private async Task ProcessFollowUpRemindersAsync(OcmsDbContext dbContext, IEmailService emailService, CancellationToken stoppingToken)
        {
            // =========================================================================================
            // [ĐOẠN COMMENT BẬT / TẮT RÀNG BUỘC NGÀY TÁI KHÁM ĐỂ DEMO]
            //
            // --- CÁCH 1: CHẾ ĐỘ THỰC TẾ (PRODUCTION - NHẮC TRƯỚC 1 NGÀY): ---
            // Bệnh nhân có lịch hẹn tái khám vào ngày mai (FollowUpDate == Today + 1 ngày).
            // Bỏ comment dòng dưới nếu muốn áp dụng ràng buộc nhắc trước 1 ngày:
            //
            // var followUpRecords = await dbContext.MedicalRecords
            //     .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            //     .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            //     .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Department)
            //     .Where(m => m.FollowUpDate.HasValue && m.FollowUpDate.Value.Date == DateTime.Today.AddDays(1))
            //     .ToListAsync(stoppingToken);
            //
            // --- CÁCH 2: CHẾ ĐỘ TEST DEMO NGAY LẬP TỨC (MẶC ĐỊNH HIỆN TẠI): ---
            // Bác sĩ vừa hoàn tất ca khám có ngày hẹn tái khám (hôm nay hoặc tương lai)
            // hệ thống sẽ gửi email ngay sau 1 phút, không bắt buộc phải chờ đến đúng ngày mai.
            // =========================================================================================

            var followUpRecords = await dbContext.MedicalRecords
                .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
                .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
                .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Department)
                .Where(m => m.FollowUpDate.HasValue && m.FollowUpDate.Value.Date >= DateTime.Today)
                .ToListAsync(stoppingToken);

            if (followUpRecords.Any())
            {
                foreach (var record in followUpRecords)
                {
                    // Chống gửi trùng lặp nhiều lần cho cùng một bệnh án trong phiên chạy
                    if (_sentFollowUpRecordIds.Contains(record.Id))
                    {
                        continue;
                    }

                    var recipientEmail = record.Appointment?.Patient?.User?.Email;
                    if (string.IsNullOrWhiteSpace(recipientEmail) ||
                        recipientEmail.EndsWith("@ocms.clinic", StringComparison.OrdinalIgnoreCase) ||
                        recipientEmail.EndsWith("@ocms.local", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var patientName = record.Appointment?.Patient?.User?.FullName ?? "Quý khách";
                    var doctorName = record.Appointment?.Doctor?.User?.FullName ?? "Bác sĩ điều trị";
                    var academicTitle = record.Appointment?.Doctor?.AcademicTitle ?? "BS";
                    var departmentName = record.Appointment?.Doctor?.Department?.Name ?? "Chuyên khoa";
                    var followUpDateStr = record.FollowUpDate!.Value.ToString("dd/MM/yyyy");

                    var subject = $"[OCMS Clinic] Nhắc lịch hẹn tái khám ngày {followUpDateStr}";

                    var body = $@"
                        <div style='font-family: Arial, sans-serif; border: 1px solid #e0e0e0; padding: 25px; border-radius: 12px; max-width: 600px; margin: 0 auto; background-color: #ffffff;'>
                            <div style='text-align: center; border-bottom: 2px solid #198754; padding-bottom: 15px; margin-bottom: 20px;'>
                                <h2 style='color: #198754; margin: 0;'>PHÒNG KHÁM ĐA KHOA OCMS</h2>
                                <p style='color: #6c757d; margin: 5px 0 0 0; font-size: 14px;'>Thông báo nhắc lịch hẹn tái khám định kỳ</p>
                            </div>
                            <p style='font-size: 15px;'>Kính gửi <strong>{patientName}</strong>,</p>
                            <p style='font-size: 14px; line-height: 1.6;'>
                                Theo kết luận của đợt khám bệnh trước, Bác sĩ điều trị có chỉ định tái khám để kiểm tra tiến triển bệnh và đánh giá hiệu quả dùng thuốc.
                            </p>
                            <div style='background-color: #f0fdf4; border: 1px solid #bbf7d0; border-left: 4px solid #198754; padding: 15px; margin: 15px 0; border-radius: 6px;'>
                                <p style='margin: 6px 0;'><strong>Ngày hẹn tái khám:</strong> <span style='color: #198754; font-size: 16px; font-weight: bold;'>{followUpDateStr}</span></p>
                                <p style='margin: 6px 0;'><strong>Bác sĩ phụ trách:</strong> {academicTitle}. {doctorName}</p>
                                <p style='margin: 6px 0;'><strong>Chuyên khoa:</strong> {departmentName}</p>
                                <p style='margin: 6px 0;'><strong>Chẩn đoán trước đó:</strong> {record.Diagnosis}</p>
                                {(string.IsNullOrWhiteSpace(record.DoctorNotes) ? "" : $"<p style='margin: 6px 0;'><strong>Lời dặn của Bác sĩ:</strong> <em>{record.DoctorNotes}</em></p>")}
                            </div>
                            <div style='background-color: #e7f1ff; border: 1px solid #cce5ff; padding: 12px; border-radius: 6px; margin-bottom: 15px;'>
                                <p style='margin: 0; font-size: 13px; color: #004085;'>
                                    💡 <strong>Hướng dẫn:</strong> Quý khách có thể truy cập website để chọn khung giờ khám phù hợp, hoặc đến trực tiếp Quầy lễ tân vào ngày hẹn để được ưu tiên sắp xếp vào phòng khám.
                                </p>
                            </div>
                            <hr style='border: none; border-top: 1px solid #eeeeee; margin: 20px 0;' />
                            <p style='font-size: 13px; color: #6c757d; margin: 0;'>Hệ thống Phòng Khám Đa Khoa OCMS • Hotline: 1900 8888 • Email hỗ trợ: hams.ocms.noreply@gmail.com</p>
                        </div>";

                    await emailService.SendEmailAsync(recipientEmail, subject, body);
                    _sentFollowUpRecordIds.Add(record.Id);
                    _logger.LogInformation($"[FOLLOW-UP REMINDER SUCCESS] Đã tự động gửi email nhắc lịch tái khám tới bệnh nhân: {recipientEmail} (Hồ sơ #{record.Id}, Ngày hẹn: {followUpDateStr})");
                }
            }
        }
    }
}
