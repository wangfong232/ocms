using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OCMSMVC.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OCMSMVC.Workers
{
    public class SlotExpiryWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<BookingHub> _hubContext;
        private readonly ILogger<SlotExpiryWorker> _logger;

        public SlotExpiryWorker(
            IServiceProvider serviceProvider,
            IHubContext<BookingHub> hubContext,
            ILogger<SlotExpiryWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
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
                        var now = DateTime.UtcNow;

                        // 1. Tự động giải phóng các khung giờ đang "Hold" mà đã hết hạn HOẶC các slot cũ từ trước (HoldExpiresAt == null)
                        var expiredHeldSlots = await dbContext.TimeSlots
                            .Where(s => s.Status == "Hold" && (s.HoldExpiresAt == null || s.HoldExpiresAt <= now))
                            .ToListAsync(stoppingToken);

                        if (expiredHeldSlots.Any())
                        {
                            foreach (var slot in expiredHeldSlots)
                            {
                                slot.Status = "Open";
                                slot.HoldExpiresAt = null;
                                _logger.LogInformation($"[AUTO RELEASE HOLD] Khung giờ ID: {slot.Id} đã hết hạn 10 phút -> tự giải phóng về 'Open'.");
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);

                            // Bắn SignalR realtime báo cho tất cả client
                            foreach (var slot in expiredHeldSlots)
                            {
                                await _hubContext.Clients.All.SendAsync("SlotStatusChanged", new
                                {
                                    slotId = slot.Id,
                                    status = "Open",
                                    rowVersion = Convert.ToBase64String(slot.RowVersion)
                                }, cancellationToken: stoppingToken);
                            }
                        }

                        // 2. Hủy các đơn đặt hẹn "Pending" quá 10 phút chưa thanh toán
                        var expiredApptTime = now.AddMinutes(-10);
                        var expiredAppointments = await dbContext.Appointments
                            .Include(a => a.TimeSlot)
                            .Where(a => a.Status == "Pending" && a.CreatedAt <= expiredApptTime)
                            .ToListAsync(stoppingToken);

                        if (expiredAppointments.Any())
                        {
                            var releasedSlots = new List<TimeSlot>();
                            foreach (var appt in expiredAppointments)
                            {
                                appt.Status = "Cancelled";
                                appt.CancellationReason = "Quá hạn thanh toán 10 phút";

                                if (appt.TimeSlot != null)
                                {
                                    appt.TimeSlot.Status = "Open";
                                    appt.TimeSlot.HoldExpiresAt = null;
                                    releasedSlots.Add(appt.TimeSlot);
                                }

                                _logger.LogInformation($"[AUTO CANCEL PENDING] Hủy lịch hẹn ID: {appt.Id}, mở lại Slot ID: {appt.TimeSlotId}");
                            }

                            await dbContext.SaveChangesAsync(stoppingToken);

                            foreach (var slot in releasedSlots)
                            {
                                await _hubContext.Clients.All.SendAsync("SlotStatusChanged", new
                                {
                                    slotId = slot.Id,
                                    status = "Open",
                                    rowVersion = Convert.ToBase64String(slot.RowVersion)
                                }, cancellationToken: stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra trong quá trình quét giải phóng khung giờ hết hạn.");
                }

                // Quét mỗi 20 giây để phản hồi nhanh chóng
                await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
            }
        }
    }
}