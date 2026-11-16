using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public class TimeSlotRepository : ITimeSlotRepository
    {
        private readonly OcmsDbContext context;
        public TimeSlotRepository(OcmsDbContext context)
        {
            this.context = context;
        }

        public async Task<List<TimeSlot>> GetSlotsByDoctorAndDateAsync(int doctorId, DateTime targetDate)
        {
            var dateOnly = targetDate.Date;
            var slots = await context.TimeSlots.Include(ts => ts.DoctorSchedule)
                .Where(ts => ts.DoctorSchedule.DoctorId == doctorId && ts.DoctorSchedule.WorkDate == dateOnly && ts.DoctorSchedule.Status == "Active")
                .OrderBy(ts => ts.StartTime)
                .ToListAsync();

            // Tự động kiểm tra: nếu có slot đang "Hold" mà không có hạn hoặc đã quá hạn -> tự giải phóng về "Open"
            var now = DateTime.UtcNow;
            bool hasExpired = false;
            foreach (var slot in slots)
            {
                if (slot.Status == "Hold" && (!slot.HoldExpiresAt.HasValue || slot.HoldExpiresAt.Value <= now))
                {
                    slot.Status = "Open";
                    slot.HoldExpiresAt = null;
                    hasExpired = true;
                }
            }
            if (hasExpired)
            {
                await context.SaveChangesAsync();
            }

            return slots;
        }

        public async Task<TimeSlot?> GetSlotByIdAsync(int slotId)
        {
            return await context.TimeSlots.Include(ts => ts.DoctorSchedule).ThenInclude(s => s.Room)
        .Include(ts => ts.DoctorSchedule).ThenInclude(s => s.Doctor).ThenInclude(d => d.User)
        .Include(ts => ts.DoctorSchedule).ThenInclude(s => s.Doctor).ThenInclude(d => d.Department)
        .FirstOrDefaultAsync(ts => ts.Id == slotId);
        }

        public async Task<(bool success, string? newRowVersion)> HoldSlotAsync(int slotId, byte[] expectedRowVersion)
        {
            var slot = await context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == slotId);
            if (slot == null)
            {
                return (false, null);
            }

            // Nếu slot đang Hold nhưng không có hạn hoặc đã quá hạn -> reset về Open cho phép giữ tiếp
            if (slot.Status == "Hold" && (!slot.HoldExpiresAt.HasValue || slot.HoldExpiresAt.Value <= DateTime.UtcNow))
            {
                slot.Status = "Open";
                slot.HoldExpiresAt = null;
            }

            if (slot.Status != "Open")
            {
                return (false, null);
            }

            context.Entry(slot).Property(s => s.RowVersion).OriginalValue = expectedRowVersion;
            slot.Status = "Hold";
            slot.HoldExpiresAt = DateTime.UtcNow.AddMinutes(10);
            try
            {
                await context.SaveChangesAsync();
                return (true, Convert.ToBase64String(slot.RowVersion));
            }
            catch (DbUpdateConcurrencyException)
            {
                // Concurrency token cũ (ví dụ do người khác vừa đổi slot và nhả slot này ra).
                // Kiểm tra lại: nếu slot hiện tại trong DB vẫn đang là "Open" thì nạp lại RowVersion mới và cho phép giữ chỗ!
                await context.Entry(slot).ReloadAsync();
                if (slot != null && (slot.Status == "Open" || (slot.Status == "Hold" && (!slot.HoldExpiresAt.HasValue || slot.HoldExpiresAt.Value <= DateTime.UtcNow))))
                {
                    slot.Status = "Hold";
                    slot.HoldExpiresAt = DateTime.UtcNow.AddMinutes(10);
                    try
                    {
                        await context.SaveChangesAsync();
                        return (true, Convert.ToBase64String(slot.RowVersion));
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return (false, null);
                    }
                }
                return (false, null);
            }
        }
        public async Task<string?> ReleaseSlotAsync(int slotId)
        {
            var slot = await context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == slotId);
            if (slot == null)
            {
                return null;
            }

            if (slot.Status == "Hold")
            {
                slot.Status = "Open";
                slot.HoldExpiresAt = null;
                await context.SaveChangesAsync();
                return Convert.ToBase64String(slot.RowVersion);
            }
            return null;
        }
    }
}
