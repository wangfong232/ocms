using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OCMSMVC.Workers
{
    public class DailyReminderWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DailyReminderWorker> _logger;

        public DailyReminderWorker(IServiceProvider serviceProvider, ILogger<DailyReminderWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Daily Reminder Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now; 

                if (now.Hour == 8 && now.Minute == 0)
                {
                    await ProcessDailyRemindersAsync(stoppingToken);
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task ProcessDailyRemindersAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<OcmsDbContext>();

                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var tomorrow = DateTime.Today.AddDays(1);

                var upcomingAppointments = await dbContext.Appointments
                    .Include(a => a.Patient)
                        .ThenInclude(p => p.User)
                    .Include(a => a.TimeSlot)
                    .Where(a => a.Status == "Confirmed" && a.AppointmentDate.Date == tomorrow)
                    .ToListAsync(stoppingToken);

                if (upcomingAppointments.Any())
                {
                    foreach (var appt in upcomingAppointments)
                    {
                        if (!string.IsNullOrEmpty(appt.Patient.User?.Email))
                        {
                            var subject = "Nhắc nhở lịch hẹn khám ngày mai tại OCMS Clinic";

                            var body = $@"
                                <div style='font-family: Arial, sans-serif; border: 1px solid #ddd; padding: 20px; border-radius: 10px; max-width: 600px;'>
                                    <h2 style='color: #0D8ABC;'>OCMS Clinic - Nhắc Lịch Khám</h2>
                                    <p>Xin chào <strong>{appt.Patient.User.FullName}</strong>,</p>
                                    <p>Đây là tin nhắn tự động nhắc nhở bạn có một lịch hẹn khám vào ngày mai.</p>
                                    <ul>
                                        <li><strong>Ngày khám:</strong> {appt.AppointmentDate:dd/MM/yyyy}</li>
                                        <li><strong>Giờ khám:</strong> {appt.TimeSlot.StartTime:hh\\:mm} - {appt.TimeSlot.EndTime:hh\\:mm}</li>
                                        <li><strong>Mã đặt chỗ:</strong> {appt.QrCodeToken ?? "N/A"}</li>
                                    </ul>
                                    <p>Vui lòng đến trước 15 phút và đưa mã này cho lễ tân khi check-in.</p>
                                    <br/>
                                    <p>Trân trọng,<br/>Đội ngũ OCMS</p>
                                </div>";

                            await emailService.SendEmailAsync(appt.Patient.User.Email, subject, body);

                            _logger.LogInformation($"Successfully sent email to {appt.Patient.User.Email}");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No upcoming appointments for tomorrow. Skipping reminders.");
                }
            }
        }
    }
}