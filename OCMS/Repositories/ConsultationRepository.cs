using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories
{
    public class ConsultationRepository : IConsultationRepository
    {
        private readonly OcmsDbContext _context;

        public ConsultationRepository(OcmsDbContext context)
        {
            _context = context;
        }

        public async Task<List<Appointment>> GetTodayAppointmentsByDoctorIdAsync(int doctorId, DateTime date)
        {
            return await _context.Appointments
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.TimeSlot)
                .Include(a => a.VitalSigns)
                .Include(a => a.MedicalRecord)
                .Where(a => a.DoctorId == doctorId 
                         && a.AppointmentDate.Date == date.Date 
                         && a.Status != "Cancelled")
                .OrderBy(a => a.TimeSlot.StartTime)
                .ToListAsync();
        }

        public async Task<Appointment?> GetAppointmentDetailAsync(int appointmentId)
        {
            return await _context.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.User)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Department)
                .Include(a => a.Patient)
                    .ThenInclude(p => p.User)
                .Include(a => a.TimeSlot)
                .Include(a => a.VitalSigns)
                .Include(a => a.MedicalRecord)
                .Include(a => a.ServiceOrders)
                    .ThenInclude(so => so.MedicalService)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);
        }

        public async Task<List<MedicalRecord>> GetPatientPastMedicalHistoryAsync(int patientId, int currentAppointmentId)
        {
            return await _context.MedicalRecords
                .Include(m => m.Appointment)
                    .ThenInclude(a => a.Doctor)
                        .ThenInclude(d => d.User)
                .Include(m => m.Appointment)
                    .ThenInclude(a => a.TimeSlot)
                .Where(m => m.Appointment.PatientId == patientId && m.AppointmentId != currentAppointmentId)
                .OrderByDescending(m => m.Appointment.AppointmentDate)
                .ToListAsync();
        }

        public async Task<List<MedicalService>> GetActiveServicesAsync()
        {
            return await _context.MedicalServices
                .Include(s => s.Department)
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        }

        public async Task<MedicalService?> GetServiceByIdAsync(int serviceId)
        {
            return await _context.MedicalServices.FindAsync(serviceId);
        }

        public async Task<bool> SaveMedicalRecordAsync(MedicalRecord medicalRecord)
        {
            var existing = await _context.MedicalRecords.FirstOrDefaultAsync(m => m.AppointmentId == medicalRecord.AppointmentId);
            if (existing == null)
            {
                _context.MedicalRecords.Add(medicalRecord);
            }
            else
            {
                existing.Diagnosis = medicalRecord.Diagnosis;
                existing.ICD10Code = medicalRecord.ICD10Code;
                existing.DoctorNotes = medicalRecord.DoctorNotes;
                existing.FollowUpDate = medicalRecord.FollowUpDate;
            }
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> AddServiceOrderAsync(ServiceOrder serviceOrder)
        {
            _context.ServiceOrders.Add(serviceOrder);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateServiceOrderResultAsync(int serviceOrderId, string? resultSummary, string status)
        {
            var order = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (order == null) return false;

            order.ResultSummary = resultSummary;
            order.Status = status;
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteServiceOrderAsync(int serviceOrderId)
        {
            var order = await _context.ServiceOrders.FindAsync(serviceOrderId);
            if (order == null) return false;

            _context.ServiceOrders.Remove(order);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateAppointmentStatusAsync(int appointmentId, string status)
        {
            var appt = await _context.Appointments.FindAsync(appointmentId);
            if (appt == null) return false;

            appt.Status = status;
            appt.UpdatedAt = DateTime.UtcNow;
            return await _context.SaveChangesAsync() > 0;
        }
    }
}
