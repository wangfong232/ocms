using BusinessObjects.Entities;
using Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class AppointmentService : IAppointmentService
    {
        private readonly IAppointmentRepository appointmentRepository;
        private readonly ITimeSlotRepository timeSlotRepository;
        public AppointmentService(IAppointmentRepository appointmentRepository, ITimeSlotRepository timeSlotRepository)
        {
            this.appointmentRepository = appointmentRepository;
            this.timeSlotRepository = timeSlotRepository;
        }

        public async Task<Patient?> GetPatientByUserIdAsync(int userId)
        {
            return await appointmentRepository.GetPatientByUserIdAsync(userId);
        }

        public async Task<List<Appointment>> GetPatientAppointmentsAsync(int userId)
        {
            var patient = await appointmentRepository.GetPatientByUserIdAsync(userId);
            if (patient == null)
            {
                return new List<Appointment>();
            }
            return await appointmentRepository.GetAppointmentsByPatientIdAsync(patient.Id);
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId, int userId)
        {
            var patient = await appointmentRepository.GetPatientByUserIdAsync(userId);
            if (patient == null)
            {
                return null;
            }
            return await appointmentRepository.GetAppointmentByIdAndPatientAsync(appointmentId, patient.Id);
        }

        public async Task<(bool Success, string Message, bool IsRefunded)> CancelAppointmentAsync(int appointmentId, int userId, string? cancellationReason)
        {
            var patient = await appointmentRepository.GetPatientByUserIdAsync(userId);
            if (patient == null)
            {
                return (false, "Không tìm thấy thông tin bệnh nhân tương ứng với tài khoản của bạn.", false);
            }
            return await appointmentRepository.CancelAppointmentAsync(appointmentId, patient.Id, cancellationReason);
        }

        public async Task<(bool Success, string Message)> RescheduleAppointmentAsync(int appointmentId, int userId, int newSlotId)
        {
            var patient = await appointmentRepository.GetPatientByUserIdAsync(userId);
            if (patient == null)
            {
                return (false, "Không tìm thấy thông tin bệnh nhân tương ứng với tài khoản của bạn.");
            }
            return await appointmentRepository.RescheduleAppointmentAsync(appointmentId, patient.Id, newSlotId);
        }
        public async Task<List<TimeSlot>> GetAvailableSlotsForDoctorAndDateAsync(int doctorId, DateTime targetDate)
        {
            return await timeSlotRepository.GetSlotsByDoctorAndDateAsync(doctorId, targetDate);
        }
    }
}
