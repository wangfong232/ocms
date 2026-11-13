using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public interface IAppointmentService
    {
        Task<Patient?> GetPatientByUserIdAsync(int userId);
        Task<List<Appointment>> GetPatientAppointmentsAsync(int userId);
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId, int userId);
        Task<(bool Success, string Message, bool IsRefunded)> CancelAppointmentAsync(int appointmentId, int userId, string? cancellationReason);
        Task<(bool Success, string Message)> RescheduleAppointmentAsync(int appointmentId, int userId, int newSlotId);
        Task<List<TimeSlot>> GetAvailableSlotsForDoctorAndDateAsync(int doctorId, DateTime targetDate);
    }
}
