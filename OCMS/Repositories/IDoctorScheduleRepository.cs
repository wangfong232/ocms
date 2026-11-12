using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories
{
    public interface IDoctorScheduleRepository
    {
        Task<DoctorSchedule?> GetByIdAsync(int id);
        Task<List<DoctorSchedule>> GetSchedulesByDoctorIdAsync(int doctorId);
        Task<List<DoctorSchedule>> GetSchedulesByDoctorAndDateRangeAsync(int doctorId, DateTime startDate, DateTime endDate);
        Task<bool> ExistsShiftAsync(int doctorId, DateTime workDate, string shiftType);
        Task<bool> IsRoomOccupiedAsync(int roomId, DateTime workDate, string shiftType, int? excludeScheduleId = null);
        Task<DoctorSchedule> AddAsync(DoctorSchedule schedule);
        Task UpdateAsync(DoctorSchedule schedule);
        Task<bool> HasBookedAppointmentsAsync(int scheduleId);
    }
}

