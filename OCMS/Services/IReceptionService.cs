using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class RoomQueueDto
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public QueuePatientDto? CurrentPatient { get; set; }
        public List<QueuePatientDto> WaitingQueue { get; set; } = new List<QueuePatientDto>();
    }

    public class QueuePatientDto
    {
        public int AppointmentId { get; set; }
        public int? QueueNumber { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string MaskedPhone { get; set; } = string.Empty;
        public string AppointmentTime { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public interface IReceptionService
    {
        Task<Appointment?> FindAppointmentAsync(string query, DateTime? date = null);
        Task<Appointment?> GetAppointmentByIdAsync(int id);
        Task<List<Appointment>> GetTodayAppointmentsAsync(DateTime? date = null);
        Task<(bool Success, string Message, int? QueueNumber, Appointment? Appointment)> CheckInAsync(
            int appointmentId,
            int systolicBP,
            int diastolicBP,
            int pulse,
            int spO2,
            decimal temperature,
            decimal? weight,
            decimal? height);
        Task<(bool Success, string Message, Appointment? Appointment)> RegisterWalkInAsync(
            string fullName,
            string phone,
            string gender,
            DateTime dob,
            string? address,
            int doctorId,
            int? timeSlotId,
            bool isEmergency,
            string? reason);
        Task<List<Doctor>> GetAvailableDoctorsTodayAsync();
        Task<List<TimeSlot>> GetOpenSlotsForDoctorTodayAsync(int doctorId);
        Task<List<RoomQueueDto>> GetLobbyQueueBoardDataAsync();
        Task<(bool Success, string Message, Appointment? Appointment)> CallNextPatientAsync(int appointmentId);
        Task<(bool Success, string Message)> CompleteConsultationAsync(int appointmentId);
    }
}
