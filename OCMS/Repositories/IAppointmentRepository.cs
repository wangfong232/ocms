using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public interface IAppointmentRepository
    { 
        Task<Appointment?> GetByIdAsync(int id);
        Task<Appointment?> FindByQrCodeOrIdAsync(string query);
        Task<Appointment?> FindTodayAppointmentByPhoneAsync(string phone, DateTime date);
        Task<List<Appointment>> GetTodayAppointmentsAsync(DateTime date);
        Task<int> GetNextQueueNumberAsync(int doctorId, DateTime date);
        Task<Appointment> AddAppointmentAsync(Appointment appointment);
        Task UpdateAppointmentAsync(Appointment appointment);
        Task SaveVitalSignsAsync(VitalSigns vitalSigns);
        Task<List<Doctor>> GetActiveDoctorsWithScheduleTodayAsync(DateTime date);
        Task<List<TimeSlot>> GetOpenSlotsForDoctorTodayAsync(int doctorId, DateTime date);
        Task<TimeSlot?> GetTimeSlotByIdAsync(int timeSlotId);
        Task<DoctorSchedule?> GetDoctorScheduleTodayAsync(int doctorId, DateTime date);
        Task<TimeSlot> CreateEmergencySlotAsync(int doctorScheduleId, TimeSpan startTime, TimeSpan endTime);
        Task<List<Appointment>> GetQueueBoardAppointmentsAsync(DateTime date);
        Task<Patient?> GetPatientByPhoneAsync(string phone);
        Task<Patient> CreateWalkInPatientAsync(string fullName, string phone, string gender, DateTime dob, string? address);
        Task<Appointment> CreateAppointmentAsync(int patientId, int slotId, string? reason);
        Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId);
        Task<Patient?> GetPatientByUserIdAsync(int userId);
        Task<List<Appointment>> GetAppointmentsByPatientIdAsync(int patientId);
        Task<Appointment?> GetAppointmentByIdAndPatientAsync(int appointmentId, int patientId);
        Task<(bool Success, string Message, bool IsRefunded)> CancelAppointmentAsync(int appointmentId, int patientId, string cancellationReason);
        Task<(bool Success, string Message)> RescheduleAppointmentAsync(int appointmentId, int patientId, int newSlotId);
    }
}
