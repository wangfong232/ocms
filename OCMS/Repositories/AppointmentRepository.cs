using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly OcmsDbContext _context;

        public AppointmentRepository(OcmsDbContext context)
        {
            _context = context;
        }

        public async Task<Appointment?> GetByIdAsync(int id)
        {
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot).ThenInclude(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Include(a => a.VitalSigns)
                .Include(a => a.Payments)
                .FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<Appointment?> FindByQrCodeOrIdAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            query = query.Trim();

            // 1. Try matching QrCodeToken directly
            var appointment = await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot).ThenInclude(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Include(a => a.VitalSigns)
                .Include(a => a.Payments)
                .FirstOrDefaultAsync(a => a.QrCodeToken != null && a.QrCodeToken.ToLower() == query.ToLower());

            if (appointment != null)
            {
                return appointment;
            }

            // 2. Parse ID prefixes: OCMS-APT-X, APP-X, HAMS-APT-X
            int aptId = 0;
            if (query.StartsWith("OCMS-APT-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(query.Replace("OCMS-APT-", "", StringComparison.OrdinalIgnoreCase), out aptId);
            }
            else if (query.StartsWith("APP-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(query.Replace("APP-", "", StringComparison.OrdinalIgnoreCase), out aptId);
            }
            else if (query.StartsWith("HAMS-APT-", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(query.Replace("HAMS-APT-", "", StringComparison.OrdinalIgnoreCase), out aptId);
            }
            else
            {
                int.TryParse(query, out aptId);
            }

            if (aptId > 0)
            {
                return await GetByIdAsync(aptId);
            }

            return null;
        }

        public async Task<Appointment?> FindTodayAppointmentByPhoneAsync(string phone, DateTime date)
        {
            phone = phone.Trim();
            var targetDate = date.Date;

            // Prioritize today's appointments that are Confirmed/Pending, then Arrived
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot).ThenInclude(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Include(a => a.VitalSigns)
                .Include(a => a.Payments)
                .Where(a => a.Patient.User.PhoneNumber == phone && a.AppointmentDate.Date == targetDate)
                .OrderByDescending(a => a.Status == "Confirmed")
                .ThenByDescending(a => a.Status == "Pending")
                .ThenByDescending(a => a.Status == "Arrived")
                .ThenBy(a => a.AppointmentDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<Appointment>> GetTodayAppointmentsAsync(DateTime date)
        {
            var targetDate = date.Date;
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot).ThenInclude(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Include(a => a.VitalSigns)
                .Where(a => a.AppointmentDate.Date == targetDate)
                .OrderBy(a => a.AppointmentDate)
                .ThenBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<int> GetNextQueueNumberAsync(int doctorId, DateTime date)
        {
            var targetDate = date.Date;
            var maxQueue = await _context.Appointments
                .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == targetDate && a.QueueNumber.HasValue)
                .MaxAsync(a => (int?)a.QueueNumber) ?? 0;

            return maxQueue + 1;
        }

        public async Task<Appointment> AddAppointmentAsync(Appointment appointment)
        {
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task UpdateAppointmentAsync(Appointment appointment)
        {
            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();
        }

        public async Task SaveVitalSignsAsync(VitalSigns vitalSigns)
        {
            if (vitalSigns.Id == 0)
            {
                _context.VitalSigns.Add(vitalSigns);
            }
            else
            {
                _context.VitalSigns.Update(vitalSigns);
            }
            await _context.SaveChangesAsync();
        }

        public async Task<List<Doctor>> GetActiveDoctorsWithScheduleTodayAsync(DateTime date)
        {
            var targetDate = date.Date;
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Include(d => d.Schedules.Where(s => s.WorkDate.Date == targetDate && s.Status == "Active"))
                    .ThenInclude(s => s.Room)
                .Include(d => d.Schedules.Where(s => s.WorkDate.Date == targetDate && s.Status == "Active"))
                    .ThenInclude(s => s.TimeSlots)
                .Where(d => d.Schedules.Any(s => s.WorkDate.Date == targetDate && s.Status == "Active"))
                .ToListAsync();
        }

        public async Task<List<TimeSlot>> GetOpenSlotsForDoctorTodayAsync(int doctorId, DateTime date)
        {
            var targetDate = date.Date;
            return await _context.TimeSlots
                .Include(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Where(t => t.DoctorSchedule.DoctorId == doctorId 
                         && t.DoctorSchedule.WorkDate.Date == targetDate
                         && t.Status == "Open")
                .OrderBy(t => t.StartTime)
                .ToListAsync();
        }

        public async Task<TimeSlot?> GetTimeSlotByIdAsync(int timeSlotId)
        {
            return await _context.TimeSlots
                .Include(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Include(t => t.DoctorSchedule).ThenInclude(s => s.Doctor).ThenInclude(d => d.User)
                .FirstOrDefaultAsync(t => t.Id == timeSlotId);
        }

        public async Task<DoctorSchedule?> GetDoctorScheduleTodayAsync(int doctorId, DateTime date)
        {
            var targetDate = date.Date;
            return await _context.DoctorSchedules
                .Include(s => s.Room)
                .Include(s => s.Doctor).ThenInclude(d => d.User)
                .Include(s => s.TimeSlots)
                .FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.WorkDate.Date == targetDate && s.Status == "Active");
        }

        public async Task<TimeSlot> CreateEmergencySlotAsync(int doctorScheduleId, TimeSpan startTime, TimeSpan endTime)
        {
            var slot = new TimeSlot
            {
                ScheduleId = doctorScheduleId,
                StartTime = startTime,
                EndTime = endTime,
                Status = "Booked"
            };
            _context.TimeSlots.Add(slot);
            await _context.SaveChangesAsync();
            return slot;
        }

        public async Task<List<Appointment>> GetQueueBoardAppointmentsAsync(DateTime date)
        {
            var targetDate = date.Date;
            return await _context.Appointments
                .Include(a => a.Patient).ThenInclude(p => p.User)
                .Include(a => a.Doctor).ThenInclude(d => d.User)
                .Include(a => a.Doctor).ThenInclude(d => d.Department)
                .Include(a => a.TimeSlot).ThenInclude(t => t.DoctorSchedule).ThenInclude(s => s.Room)
                .Where(a => a.AppointmentDate.Date == targetDate 
                         && (a.Status == "Arrived" || a.Status == "InConsultation" || a.Status == "Completed"))
                .OrderBy(a => a.QueueNumber ?? 999)
                .ThenBy(a => a.AppointmentDate)
                .ToListAsync();
        }

        public async Task<Patient?> GetPatientByPhoneAsync(string phone)
        {
            phone = phone.Trim();
            return await _context.Patients
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.User.PhoneNumber == phone);
        }

        public async Task<Patient> CreateWalkInPatientAsync(string fullName, string phone, string gender, DateTime dob, string? address)
        {
            phone = phone.Trim();
            var existingUser = await _context.Users.Include(u => u.Patient).FirstOrDefaultAsync(u => u.PhoneNumber == phone);

            if (existingUser != null)
            {
                if (existingUser.Patient != null)
                {
                    return existingUser.Patient;
                }

                var newPatientProfile = new Patient
                {
                    UserId = existingUser.Id,
                    DateOfBirth = dob,
                    Gender = string.IsNullOrWhiteSpace(gender) ? "Other" : gender,
                    Address = address
                };
                _context.Patients.Add(newPatientProfile);
                await _context.SaveChangesAsync();
                newPatientProfile.User = existingUser;
                return newPatientProfile;
            }

            // Create new User and Patient profile
            string email = $"walkin_{phone}@ocms.local";
            if (await _context.Users.AnyAsync(u => u.Email == email))
            {
                email = $"walkin_{phone}_{Guid.NewGuid().ToString().Substring(0, 5)}@ocms.local";
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var newUser = new User
            {
                FullName = fullName.Trim(),
                PhoneNumber = phone,
                Email = email,
                PasswordHash = passwordHash,
                Role = "Patient",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            var newPatient = new Patient
            {
                UserId = newUser.Id,
                DateOfBirth = dob,
                Gender = string.IsNullOrWhiteSpace(gender) ? "Other" : gender,
                Address = address
            };

            _context.Patients.Add(newPatient);
            await _context.SaveChangesAsync();

            newPatient.User = newUser;
            return newPatient;
        }

   
        public async Task<Appointment> CreateAppointmentAsync(int patientId, int slotId, string? reason)
        {
            var slot = await _context.TimeSlots.Include(ts => ts.DoctorSchedule).FirstOrDefaultAsync(ts => ts.Id == slotId);

            if (slot == null)
            {
                throw new InvalidOperationException("Khung giờ không tồn tại.");
            }

            slot.Status = "Booked";

            string qrToken = $"QR-{slot.DoctorSchedule.WorkDate:yyyyMMdd}-{Guid.NewGuid():N}".Substring(0, 20).ToUpper();//Guid.NewGuid: Tạo một chuỗi duy nhất

            var appointment = new Appointment
            {
                PatientId = patientId,
                DoctorId = slot.DoctorSchedule.DoctorId,
                TimeSlotId = slotId,
                AppointmentDate = slot.DoctorSchedule.WorkDate,
                Status = "Confirmed",
                Reason = reason,
                QrCodeToken = qrToken,
                RescheduledCount = 0,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return appointment;
        }

        public async Task<Appointment?> GetAppointmentDetailsAsync(int appointmentId)
        {
            return await _context.Appointments.Include(a => a.Patient).ThenInclude(p => p.User)
                                             .Include(a => a.Doctor).ThenInclude(d => d.User)
                                             .Include(a => a.Doctor).ThenInclude(d => d.Department)
                                             .Include(a => a.TimeSlot).ThenInclude(ts => ts.DoctorSchedule).ThenInclude(s => s.Room)
                                             .FirstOrDefaultAsync(a => a.Id == appointmentId);
        }

        public async Task<Patient?> GetPatientByUserIdAsync(int userId)
        {
            return await _context.Patients.Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == userId);
        }

        public async Task<Appointment?> GetAppointmentByIdAndPatientAsync(int appointmentId, int patientId)
        {
            return await _context.Appointments.Include(a => a.Patient).ThenInclude(p => p.User)
                                            .Include(a => a.Doctor).ThenInclude(d => d.User)
                                            .Include(a => a.Doctor).ThenInclude(d => d.Department)
                                            .Include(a => a.Doctor).ThenInclude(d => d.DefaultRoom)
                                            .Include(a => a.TimeSlot).ThenInclude(ts => ts.DoctorSchedule).ThenInclude(s => s.Room)
                                            .Include(a => a.Payments)
                                            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);
        }

        public async Task<List<Appointment>> GetAppointmentsByPatientIdAsync(int patientId)
        {
            return await _context.Appointments.Include(a => a.Doctor).ThenInclude(d => d.User)
                                            .Include(a => a.Doctor).ThenInclude(d => d.Department)
                                            .Include(a => a.Doctor).ThenInclude(d => d.DefaultRoom)
                                            .Include(a => a.TimeSlot).ThenInclude(ts => ts.DoctorSchedule).ThenInclude(s => s.Room)
                                            .Include(a => a.Payments)
                                            .Where(a => a.PatientId == patientId)
                                            .OrderByDescending(a => a.AppointmentDate)
                                            .ThenByDescending(a => a.TimeSlot.StartTime)
                                            .ToListAsync();
        }

        public async Task<(bool Success, string Message, bool IsRefunded)> CancelAppointmentAsync(int appointmentId, int patientId, string? cancellationReason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appt = await _context.Appointments.Include(a => a.TimeSlot).Include(a => a.Payments).FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);

                if (appt == null)
                {
                    return (false, "Cuộc hẹn không tồn tại.", false);
                }

                if (appt.Status == "Cancelled")
                {
                    return (false, "Cuộc hẹn đã bị hủy trước đó.", false);
                }

                if (appt.Status == "Completed" || appt.Status == "InConsultation" || appt.Status == "Arrived")
                {
                    return (false, $"Không thể hủy lịch hẹn đang ở trạng thái '{appt.Status}'.", false);
                }

                var appointmentDateTime = appt.AppointmentDate.Date.Add(appt.TimeSlot.StartTime);
                var hoursRemaining = (appointmentDateTime - DateTime.Now).TotalHours;

                bool isRefunded = false;
                string refundNotice;

                if (hoursRemaining >= 24)
                {
                    isRefunded = true;
                    refundNotice = "Đủ điều kiện hoàn 100% tiền đặt cọc (theo chính sách hủy trước 24 giờ.";

                    var depositPayment = appt.Payments.FirstOrDefault(p => p.PaymentType == "Deposit" && p.PaymentStatus == "Success");
                    if (depositPayment != null)
                    {
                        var refundPayment = new Payment
                        {
                            AppointmentId = appt.Id,
                            Amount = depositPayment.Amount,
                            PaymentMethod = depositPayment.PaymentMethod,
                            PaymentStatus = "Success",
                            PaymentType = "Refund",
                            TransactionId = $"REF-{appt.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}",
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Payments.Add(refundPayment);
                    }
                }
                else
                {
                    isRefunded = false;
                    refundNotice = "Hủy lịch hẹn dưới 24 giờ trước giờ khám: Không được hoàn trả tiền cọc theo quy định.";
                }

                appt.Status = "Cancelled";
                appt.CancellationReason = string.IsNullOrWhiteSpace(cancellationReason) ? "Bệnh nhân yêu cầu hủy lịch" : cancellationReason.Trim();
                appt.UpdatedAt = DateTime.UtcNow;


                var slot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == appt.TimeSlotId);
                if (slot != null && slot.Status != "Open")
                {
                    slot.Status = "Open";
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, $"Hủy lịch hẹn thành công. {refundNotice}", isRefunded);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi hủy lịch: {ex.Message}", false);
            }
        }

        public async Task<(bool Success, string Message)> RescheduleAppointmentAsync(int appointmentId, int patientId, int newSlotId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appt = await _context.Appointments.Include(a => a.TimeSlot).ThenInclude(ts => ts.DoctorSchedule)
                       .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);
                if (appt == null)
                {
                    return (false, "Cuộc hẹn không tồn tại.");
                }

                if (appt.Status != "Confirmed" && appt.Status != "Pending")
                {
                    return (false, $"Chỉ có thể đổi lịch cho lịch hẹn ở trạng thái 'Đã xác nhận' hoặc 'Chờ xác nhận' (Trạng thái hiện tại: {appt.Status}).");
                }

                if (appt.RescheduledCount >= 1)
                {
                    return (false, "Lịch hẹn này đã được đổi lịch 1 lần. Theo quy định, mỗi lịch hẹn chỉ được đổi tối đa 1 lần.");
                }

                var currentApptDateTime = appt.AppointmentDate.Date.Add(appt.TimeSlot.StartTime);
                if (currentApptDateTime <= DateTime.Now)
                {
                    return (false, "Không thể đổi lịch hẹn cho khung giờ đã qua.");
                }

                if (appt.TimeSlotId == newSlotId)
                {
                    return (false, "Khung giờ mới trùng với khung giờ hiện tại của bạn.");
                }

                var newSlot = await _context.TimeSlots.Include(ts => ts.DoctorSchedule).FirstOrDefaultAsync(ts => ts.Id == newSlotId);

                if (newSlot == null)
                {
                    return (false, "Khung giờ mới không tồn tại.");
                }

                if (newSlot.Status != "Open")
                {
                    return (false, "Khung giờ mới đã có người khác chọn hoặc không còn khả dụng.");
                }

                if (newSlot.DoctorSchedule.DoctorId != appt.DoctorId)
                {
                    return (false, "Chỉ được phép đổi lịch sang khung giờ khác của cùng bác sĩ điều trị.");
                }

                var newSlotDateTime = newSlot.DoctorSchedule.WorkDate.Date.Add(newSlot.StartTime);
                if (newSlotDateTime <= DateTime.Now)
                {
                    return (false, "Không thể chọn khung giờ trong quá khứ.");
                }

                var oldSlot = await _context.TimeSlots.FirstOrDefaultAsync(ts => ts.Id == appt.TimeSlotId);
                if(oldSlot!=null){
                oldSlot.Status = "Open";
                }

                newSlot.Status = "Booked";

                appt.TimeSlotId = newSlotId;
                appt.AppointmentDate = newSlot.DoctorSchedule.WorkDate;
                appt.RescheduledCount += 1;
                appt.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, "Đổi lịch khám thành công.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return (false, "Khung giờ vừa chọn đã có người khác đặt trước. Vui lòng chọn khung giờ khác.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, $"Lỗi hệ thống khi đổi lịch: {ex.Message}");
            }
        }

    }
}
