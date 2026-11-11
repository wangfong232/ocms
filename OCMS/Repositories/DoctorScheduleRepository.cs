using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories
{
    public class DoctorScheduleRepository : IDoctorScheduleRepository
    {
        private readonly OcmsDbContext _context;

        public DoctorScheduleRepository(OcmsDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorSchedule?> GetByIdAsync(int id)
        {
            return await _context.DoctorSchedules
                .Include(s => s.Doctor)
                    .ThenInclude(d => d.User)
                .Include(s => s.Room)
                .Include(s => s.TimeSlots)
                    .ThenInclude(t => t.Appointments)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<List<DoctorSchedule>> GetSchedulesByDoctorIdAsync(int doctorId)
        {
            return await _context.DoctorSchedules
                .Include(s => s.Room)
                .Include(s => s.TimeSlots)
                    .ThenInclude(t => t.Appointments)
                .Where(s => s.DoctorId == doctorId)
                .OrderByDescending(s => s.WorkDate)
                .ThenBy(s => s.ShiftType)
                .ToListAsync();
        }

        public async Task<List<DoctorSchedule>> GetSchedulesByDoctorAndDateRangeAsync(int doctorId, DateTime startDate, DateTime endDate)
        {
            return await _context.DoctorSchedules
                .Include(s => s.Room)
                .Include(s => s.TimeSlots)
                    .ThenInclude(t => t.Appointments)
                .Where(s => s.DoctorId == doctorId && s.WorkDate.Date >= startDate.Date && s.WorkDate.Date <= endDate.Date)
                .OrderBy(s => s.WorkDate)
                .ThenBy(s => s.ShiftType)
                .ToListAsync();
        }

        public async Task<bool> ExistsShiftAsync(int doctorId, DateTime workDate, string shiftType)
        {
            return await _context.DoctorSchedules
                .AnyAsync(s => s.DoctorId == doctorId 
                            && s.WorkDate.Date == workDate.Date 
                            && s.ShiftType == shiftType 
                            && s.Status == "Active");
        }

        public async Task<bool> IsRoomOccupiedAsync(int roomId, DateTime workDate, string shiftType, int? excludeScheduleId = null)
        {
            return await _context.DoctorSchedules
                .AnyAsync(s => s.RoomId == roomId 
                            && s.WorkDate.Date == workDate.Date 
                            && s.ShiftType == shiftType 
                            && s.Status == "Active"
                            && (!excludeScheduleId.HasValue || s.Id != excludeScheduleId.Value));
        }

        public async Task<DoctorSchedule> AddAsync(DoctorSchedule schedule)
        {
            _context.DoctorSchedules.Add(schedule);
            await _context.SaveChangesAsync();
            return schedule;
        }

        public async Task UpdateAsync(DoctorSchedule schedule)
        {
            _context.DoctorSchedules.Update(schedule);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> HasBookedAppointmentsAsync(int scheduleId)
        {
            return await _context.Appointments
                .AnyAsync(a => a.TimeSlot.ScheduleId == scheduleId && a.Status != "Cancelled");
        }
    }
}

