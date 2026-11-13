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

            // 1. Kiểm tra trạng thái ca hẹn: Không cho phép check-in nếu đã hoàn tất, đã hủy hoặc đang khám
            if (appointment.Status == "Cancelled")
            {
                return (false, "Lịch hẹn này đã bị hủy bỏ, không thể thực hiện check-in.", null, null);
            }
            if (appointment.Status == "Completed")
            {
                return (false, "Lịch hẹn này đã hoàn tất khám bệnh từ trước.", null, null);
            }
            if (appointment.Status == "InConsultation")
            {
                return (false, "Bệnh nhân hiện đang ở trong phòng khám với bác sĩ.", null, null);
            }

            // 2. Kiểm tra ngày khám: Chỉ cho phép check-in vào đúng ngày hẹn khám
            var today = DateTime.Today;
            if (appointment.AppointmentDate.Date > today)
            {
                return (false, $"Chưa đến ngày khám của bệnh nhân (Ngày hẹn: {appointment.AppointmentDate:dd/MM/yyyy}). Bệnh nhân chỉ được tiếp đón vào đúng ngày hẹn.", null, null);
            }
            if (appointment.AppointmentDate.Date < today)
            {
                return (false, $"Lịch hẹn đã quá hạn khám từ ngày {appointment.AppointmentDate:dd/MM/yyyy}.", null, null);
            }

            // 3. Kiểm tra khung giờ khám: Chỉ khi đến khung giờ khám (hoặc trước tối đa 15 phút) Staff mới được check-in
            var now = DateTime.Now;
            if (appointment.TimeSlot != null)
            {
                var slotStartTime = appointment.AppointmentDate.Date.Add(appointment.TimeSlot.StartTime);
                var earliestCheckInTime = slotStartTime.AddMinutes(-15);

                if (now < earliestCheckInTime)
                {
                    return (false, $"Chưa đến khung giờ khám! Khung giờ hẹn: {appointment.TimeSlot.StartTime:hh\\:mm} - {appointment.TimeSlot.EndTime:hh\\:mm}. Lễ tân chỉ được tiếp đón check-in từ {earliestCheckInTime:HH:mm} (trước giờ hẹn 15 phút).", null, null);
                }
            }

            // 4. Kiểm tra tính hợp lý y khoa của các chỉ số sinh hiệu (Triage Vital Signs)
            if (systolicBP < 60 || systolicBP > 250)
            {
                return (false, "Huyết áp tâm thu không hợp lý (chỉ số y khoa chuẩn: 60 - 250 mmHg).", null, null);
            }
            if (diastolicBP < 40 || diastolicBP > 150)
            {
                return (false, "Huyết áp tâm trương không hợp lý (chỉ số y khoa chuẩn: 40 - 150 mmHg).", null, null);
            }
            if (systolicBP <= diastolicBP)
            {
                return (false, "Chỉ số bất thường: Huyết áp tâm thu bắt buộc phải lớn hơn huyết áp tâm trương.", null, null);
            }
            if (pulse < 40 || pulse > 220)
            {
                return (false, "Chỉ số mạch / nhịp tim không hợp lý (phải từ 40 đến 220 nhịp/phút).", null, null);
            }
            if (spO2 < 70 || spO2 > 100)
            {
                return (false, "Chỉ số nồng độ oxy máu SpO2 không hợp lý (phải từ 70% đến 100%).", null, null);
            }
            if (temperature < 34.0m || temperature > 43.0m)
            {
                return (false, "Chỉ số thân nhiệt không hợp lý (phải từ 34.0°C đến 43.0°C).", null, null);
            }
            if (weight.HasValue && (weight.Value < 1m || weight.Value > 300m))
            {
                return (false, "Chỉ số cân nặng không hợp lý (phải từ 1 đến 300 kg).", null, null);
            }
            if (height.HasValue && (height.Value < 30m || height.Value > 250m))
            {
                return (false, "Chỉ số chiều cao không hợp lý (phải từ 30 đến 250 cm).", null, null);
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
            if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length < 2)
            {
                return (false, "Họ và tên bệnh nhân không hợp lệ (tối thiểu 2 ký tự).", null);
            }

            if (string.IsNullOrWhiteSpace(phone) || !System.Text.RegularExpressions.Regex.IsMatch(phone.Trim(), @"^0[0-9]{9}$"))
            {
                return (false, "Số điện thoại không hợp lệ (bắt buộc gồm 10 chữ số và bắt đầu bằng số 0).", null);
            }

            if (dob > DateTime.Today)
            {
                return (false, "Ngày sinh không hợp lý (ngày sinh không thể nằm trong tương lai).", null);
            }

            if (dob < DateTime.Today.AddYears(-130))
            {
                return (false, "Ngày sinh không hợp lý (tuổi không thể vượt quá 130).", null);
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

            if (appointment.Status != "Arrived")
            {
                return (false, $"Không thể gọi bệnh nhân này (Trạng thái hiện tại: {appointment.Status}). Chỉ có thể gọi bệnh nhân đã check-in đang chờ tại sảnh [Arrived].", null);
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

            if (appointment.Status != "InConsultation")
            {
                return (false, $"Không thể hoàn tất ca khám này (Trạng thái hiện tại: {appointment.Status}). Chỉ có thể hoàn tất ca khám đang diễn ra trong phòng [InConsultation].");
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
