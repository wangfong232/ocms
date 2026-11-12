using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class HoldSlotResult
    {
        public bool IsSuccess { get; set; }
        public int SlotId { get; set; }
        public string Message { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; } = 600; // Default to 10 minutes
        public string NewRowVersion { get; set; } = string.Empty;
        public List<AlternativeSlotDto> AlternativeSlots { get; set; } = new();
    }

    public class AlternativeSlotDto
    {
        public int Id { get; set; }
        public string TimeRange { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public interface IBookingService
    {
        Task<List<Department>> GetSpecialtiesAsync();
        Task<List<Doctor>> SearchDoctorAsync(int? departmentId, string? term);
        Task<Doctor?> GetDoctorWithDaySlotsAsync(int doctorId, DateTime targetDate);
        Task<HoldSlotResult> HoldTimeSlotAsync(int slotId, string rowVersion, int? patientId);
        Task<string?> ReleaseSlotAsync(int slotId);
        Task<Appointment> ConfirmBookingAsync(int userId, int slotId, string? reason);
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId);
    }
}
