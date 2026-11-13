using BusinessObjects.Entities;
using Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services
{
    public class ReceptionService : IReceptionService
    {
        private readonly IAppointmentRepository _appointmentRepo;

        public ReceptionService(IAppointmentRepository appointmentRepo)
        {
            _appointmentRepo = appointmentRepo;
        }

        public async Task<Appointment?> FindAppointmentAsync(string query, DateTime? date = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return null;
            }

            query = query.Trim();

            // 1. Try finding by QR token or ID (APP-X, OCMS-APT-X, or numeric)
            var appointment = await _appointmentRepo.FindByQrCodeOrIdAsync(query);
            if (appointment != null)
            {
                return appointment;
            }

            // 2. Try finding by phone number for today
            return await _appointmentRepo.FindTodayAppointmentByPhoneAsync(query, date ?? DateTime.Today);
        }

        public async Task<Appointment?> GetAppointmentByIdAsync(int id)
        {
            return await _appointmentRepo.GetByIdAsync(id);
        }

        public async Task<List<Appointment>> GetTodayAppointmentsAsync(DateTime? date = null)
        {
            return await _appointmentRepo.GetTodayAppointmentsAsync(date ?? DateTime.Today);
        }

        public async Task<(bool Success, string Message, int? QueueNumber, Appointment? Appointment)> CheckInAsync(
            int appointmentId,
            int systolicBP,
            int diastolicBP,
            int pulse,
            int spO2,
            decimal temperature,
            decimal? weight,
            decimal? height)
        {
            var appointment = await _appointmentRepo.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return (false, "Lịch hẹn không tồn tại trong hệ thống.", null, null);
            }

            // Assign queue number if not yet assigned
            if (!appointment.QueueNumber.HasValue)
            {
                appointment.QueueNumber = await _appointmentRepo.GetNextQueueNumberAsync(appointment.DoctorId, appointment.AppointmentDate);
            }

            appointment.Status = "Arrived";
            appointment.UpdatedAt = DateTime.UtcNow;

            // Save Vital Signs (BP, Pulse, SpO2, Temperature, Weight, Height)
            if (appointment.VitalSigns == null)
            {
                var vital = new VitalSigns
                {
                    AppointmentId = appointment.Id,
                    SystolicBP = systolicBP,
                    DiastolicBP = diastolicBP,
                    Pulse = pulse,
                    SpO2 = spO2,
                    Temperature = temperature,
                    Weight = weight ?? 60m,
                    Height = height ?? 165m,
                    RecordedAt = DateTime.UtcNow
                };
                await _appointmentRepo.SaveVitalSignsAsync(vital);
                appointment.VitalSigns = vital;
            }
            else
            {
                appointment.VitalSigns.SystolicBP = systolicBP;
                appointment.VitalSigns.DiastolicBP = diastolicBP;
                appointment.VitalSigns.Pulse = pulse;
                appointment.VitalSigns.SpO2 = spO2;
                appointment.VitalSigns.Temperature = temperature;
                if (weight.HasValue) appointment.VitalSigns.Weight = weight.Value;
                if (height.HasValue) appointment.VitalSigns.Height = height.Value;
                appointment.VitalSigns.RecordedAt = DateTime.UtcNow;
                await _appointmentRepo.SaveVitalSignsAsync(appointment.VitalSigns);
            }

            await _appointmentRepo.UpdateAppointmentAsync(appointment);

            return (
                true,
                $"Check-in thành công! Đã chuyển trạng thái sang [ARRIVED] và cấp Số thứ tự khám: #{appointment.QueueNumber}",
                appointment.QueueNumber,
                appointment
            );
        }

        public async Task<(bool Success, string Message, Appointment? Appointment)> RegisterWalkInAsync(
            string fullName,
            string phone,
            string gender,
            DateTime dob,
            string? address,
            int doctorId,
            int? timeSlotId,
            bool isEmergency,
            string? reason)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (false, "Vui lòng nhập họ và tên bệnh nhân.", null);
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                return (false, "Vui lòng nhập số điện thoại bệnh nhân.", null);
            }

            var schedule = await _appointmentRepo.GetDoctorScheduleTodayAsync(doctorId, DateTime.Today);
            if (schedule == null)
            {
                return (false, "Bác sĩ được chọn không có lịch làm việc trong ngày hôm nay.", null);
            }

            TimeSlot? slot = null;
            if (isEmergency)
            {
                var now = DateTime.Now;
                var startTime = new TimeSpan(now.Hour, now.Minute, 0);
                var endTime = startTime.Add(TimeSpan.FromMinutes(20));
                if (endTime >= TimeSpan.FromHours(24))
                {
                    endTime = new TimeSpan(23, 59, 0);
                }
                slot = await _appointmentRepo.CreateEmergencySlotAsync(schedule.Id, startTime, endTime);
            }
            else
            {
                if (!timeSlotId.HasValue)
                {
                    return (false, "Vui lòng chọn khung giờ khám còn trống (filler slot).", null);
                }

                slot = await _appointmentRepo.GetTimeSlotByIdAsync(timeSlotId.Value);
                if (slot == null || slot.Status != "Open")
                {
                    return (false, "Khung giờ vừa chọn đã không còn trống. Vui lòng chọn khung giờ khác.", null);
                }

                slot.Status = "Booked";
            }

            // Create or reuse patient profile
            var patient = await _appointmentRepo.CreateWalkInPatientAsync(fullName, phone, gender, dob, address);

            // Assign queue number immediately
            int queueNumber = await _appointmentRepo.GetNextQueueNumberAsync(doctorId, DateTime.Today);

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = doctorId,
                TimeSlotId = slot.Id,
                AppointmentDate = DateTime.Today.Add(slot.StartTime),
                Status = "Arrived", // Walk-in patients are checked-in immediately at the counter
                Reason = isEmergency ? $"[CẤP CỨU / KHẨN CẤP] {reason ?? "Tiếp nhận khẩn"}" : (string.IsNullOrWhiteSpace(reason) ? "Khám bệnh vãng lai tại quầy" : reason),
                QrCodeToken = $"OCMS-APT-{DateTime.Now.Ticks % 10000000}",
                QueueNumber = queueNumber,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _appointmentRepo.AddAppointmentAsync(appointment);

            var populatedAppointment = await _appointmentRepo.GetByIdAsync(appointment.Id);

            return (
                true,
                $"Đăng ký vãng lai thành công! Cấp số khám #{queueNumber} tại phòng {schedule.Room.RoomNumber} (BS. {schedule.Doctor.User.FullName})",
                populatedAppointment
            );
        }

        public async Task<List<Doctor>> GetAvailableDoctorsTodayAsync()
        {
            return await _appointmentRepo.GetActiveDoctorsWithScheduleTodayAsync(DateTime.Today);
        }

        public async Task<List<TimeSlot>> GetOpenSlotsForDoctorTodayAsync(int doctorId)
        {
            return await _appointmentRepo.GetOpenSlotsForDoctorTodayAsync(doctorId, DateTime.Today);
        }

        public async Task<List<RoomQueueDto>> GetLobbyQueueBoardDataAsync()
        {
            var doctors = await _appointmentRepo.GetActiveDoctorsWithScheduleTodayAsync(DateTime.Today);
            var todayAppointments = await _appointmentRepo.GetQueueBoardAppointmentsAsync(DateTime.Today);

            var result = new List<RoomQueueDto>();

            foreach (var doc in doctors)
            {
                var schedule = doc.Schedules.FirstOrDefault(s => s.WorkDate.Date == DateTime.Today && s.Status == "Active");
                if (schedule == null || schedule.Room == null) continue;

                var roomAppointments = todayAppointments.Where(a => a.DoctorId == doc.Id).ToList();

                var currentApt = roomAppointments.FirstOrDefault(a => a.Status == "InConsultation");
                var waitingApts = roomAppointments
                    .Where(a => a.Status == "Arrived")
                    .OrderBy(a => a.QueueNumber ?? 999)
                    .ThenBy(a => a.AppointmentDate)
                    .ToList();

                var roomDto = new RoomQueueDto
                {
                    RoomId = schedule.Room.Id,
                    RoomNumber = schedule.Room.RoomNumber,
                    DoctorName = $"{doc.AcademicTitle} {doc.User.FullName}",
                    DepartmentName = doc.Department?.Name ?? "Phòng khám",
                    CurrentPatient = currentApt != null ? new QueuePatientDto
                    {
                        AppointmentId = currentApt.Id,
                        QueueNumber = currentApt.QueueNumber,
                        PatientName = currentApt.Patient?.User?.FullName ?? "N/A",
                        MaskedPhone = MaskPhoneNumber(currentApt.Patient?.User?.PhoneNumber),
                        AppointmentTime = currentApt.AppointmentDate.ToString("HH:mm"),
                        Status = currentApt.Status
                    } : null,
                    WaitingQueue = waitingApts.Select(w => new QueuePatientDto
                    {
                        AppointmentId = w.Id,
                        QueueNumber = w.QueueNumber,
                        PatientName = w.Patient?.User?.FullName ?? "N/A",
                        MaskedPhone = MaskPhoneNumber(w.Patient?.User?.PhoneNumber),
                        AppointmentTime = w.AppointmentDate.ToString("HH:mm"),
                        Status = w.Status
                    }).ToList()
                };

                result.Add(roomDto);
            }

            return result;
        }

        public async Task<(bool Success, string Message, Appointment? Appointment)> CallNextPatientAsync(int appointmentId)
        {
            var appointment = await _appointmentRepo.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return (false, "Lịch hẹn không tồn tại.", null);
            }

            // Complete any ongoing consultation of the same doctor
            var todayApts = await _appointmentRepo.GetQueueBoardAppointmentsAsync(DateTime.Today);
            var previousInConsult = todayApts.FirstOrDefault(a => a.DoctorId == appointment.DoctorId && a.Status == "InConsultation" && a.Id != appointment.Id);
            if (previousInConsult != null)
            {
                previousInConsult.Status = "Completed";
                previousInConsult.UpdatedAt = DateTime.UtcNow;
                await _appointmentRepo.UpdateAppointmentAsync(previousInConsult);
            }

            appointment.Status = "InConsultation";
            appointment.UpdatedAt = DateTime.UtcNow;
            await _appointmentRepo.UpdateAppointmentAsync(appointment);

            var roomNumber = appointment.TimeSlot?.DoctorSchedule?.Room?.RoomNumber ?? "phòng khám";
            var patientName = appointment.Patient?.User?.FullName ?? "Bệnh nhân";

            return (true, $"Đang gọi số #{appointment.QueueNumber} ({patientName}) vào phòng {roomNumber}.", appointment);
        }

        public async Task<(bool Success, string Message)> CompleteConsultationAsync(int appointmentId)
        {
            var appointment = await _appointmentRepo.GetByIdAsync(appointmentId);
            if (appointment == null)
            {
                return (false, "Lịch hẹn không tồn tại.");
            }

            appointment.Status = "Completed";
            appointment.UpdatedAt = DateTime.UtcNow;
            await _appointmentRepo.UpdateAppointmentAsync(appointment);

            return (true, "Đã hoàn thành ca khám.");
        }

        private static string MaskPhoneNumber(string? phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 7)
            {
                return phone ?? "N/A";
            }
            return string.Concat(phone.AsSpan(0, 3), "****", phone.AsSpan(phone.Length - 3));
        }
    }
}
