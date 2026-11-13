using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DataAccessObjects; 
using Microsoft.EntityFrameworkCore;

namespace OCMSMVC.Workers
{
    public class SlotExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SlotExpiryWorker> _logger;

        public SlotExpiryWorker(IServiceProvider serviceProvider, ILogger<SlotExpiryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Slot Expiry Worker is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<OcmsDbContext>();

                        var expiredTime = DateTime.UtcNow.AddMinutes(-10);

                        var expiredAppointments = await dbContext.Appointments
                            .Include(a => a.TimeSlot)
                            .Where(a => a.Status == "Pending" && a.CreatedAt <= expiredTime)
                            .ToListAsync(stoppingToken);

                        if (expiredAppointments.Any())
                        {
                            foreach (var appt in expiredAppointments)
                            {
                                appt.Status = "Cancelled";
                                appt.CancellationReason = "Payment Timeout (10 minutes exceeded)";

                                if (appt.TimeSlot != null)
                                {
                                    appt.TimeSlot.Status = "Open";
                                }

                                _logger.LogInformation($"Released Slot ID: {appt.TimeSlotId} from Appointment ID: {appt.Id}");
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while cleaning up expired slots.");
                }

                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}