using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IDoctorScheduleService
    {
        Task<DoctorSchedule?> GetByIdAsync(int id);
        Task<List<DoctorSchedule>> GetMySchedulesAsync(int doctorId);
        Task<List<DoctorSchedule>> GetWeeklyRosterAsync(int doctorId, DateTime anyDateInWeek);
        Task<(bool Success, string Message, DoctorSchedule? Schedule)> RegisterShiftAsync(int doctorId, DateTime workDate, string shiftType, int roomId);
        Task<(bool Success, string Message)> CancelShiftAsync(int scheduleId, int doctorId);
        Task<Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<List<Room>> GetAvailableRoomsForDoctorAsync(int doctorId);
    }
}

