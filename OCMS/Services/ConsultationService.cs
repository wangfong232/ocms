using BusinessObjects.Entities;
using Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class ConsultationService : IConsultationService
    {
        private readonly IConsultationRepository _consultationRepo;

        public ConsultationService(IConsultationRepository consultationRepo)
        {
            _consultationRepo = consultationRepo;
        }

        public async Task<List<Appointment>> GetTodayQueueAsync(int doctorId)
        {
            return await _consultationRepo.GetTodayAppointmentsByDoctorIdAsync(doctorId, DateTime.Today);
        }

        public async Task<Appointment?> GetConsultationDetailAsync(int appointmentId, int doctorId)
        {
            var appt = await _consultationRepo.GetAppointmentDetailAsync(appointmentId);
            if (appt == null || appt.DoctorId != doctorId)
            {
                return null;
            }
            return appt;
        }

        public async Task<List<MedicalRecord>> GetPatientPastMedicalHistoryAsync(int patientId, int currentAppointmentId)
        {
            return await _consultationRepo.GetPatientPastMedicalHistoryAsync(patientId, currentAppointmentId);
        }

        public async Task<List<MedicalService>> GetActiveServicesAsync()
        {
            return await _consultationRepo.GetActiveServicesAsync();
        }

        public async Task<(bool Success, string Message)> StartConsultationAsync(int appointmentId, int doctorId)
        {
            var appt = await _consultationRepo.GetAppointmentDetailAsync(appointmentId);
            if (appt == null || appt.DoctorId != doctorId)
            {
                return (false, "Không tìm thấy lịch hẹn hoặc bạn không phụ trách ca này.");
            }

            // Nếu ca này đã ở trạng thái InConsultation hoặc Completed thì cho phép vào tiếp tục/xem lại
            if (appt.Status == "InConsultation" || appt.Status == "Completed")
            {
                return (true, "Vào ca khám.");
            }

            // Kiểm tra xem bác sĩ có đang khám dở bệnh nhân nào khác không (1 phòng khám chỉ khám 1 người tại một thời điểm)
            var todayApts = await _consultationRepo.GetTodayAppointmentsByDoctorIdAsync(doctorId, DateTime.Today);
            var activeConsultation = todayApts.FirstOrDefault(a => a.Status == "InConsultation" && a.Id != appointmentId);
            if (activeConsultation != null)
            {
                var patientName = activeConsultation.Patient?.User?.FullName ?? "bệnh nhân khác";
                var queueNum = activeConsultation.QueueNumber.HasValue ? $"#{activeConsultation.QueueNumber}" : "";
                return (false, $"Bác sĩ đang khám cho {patientName} ({queueNum}). Vui lòng hoàn thành ca khám hiện tại trước khi gọi bệnh nhân tiếp theo!");
            }

            // Cập nhật trạng thái lịch hẹn sang InConsultation
            await _consultationRepo.UpdateAppointmentStatusAsync(appointmentId, "InConsultation");
            return (true, "Đã bắt đầu ca khám.");
        }

        public async Task<(bool Success, string Message)> SaveEMRAsync(
            int appointmentId, 
            int doctorId, 
            string diagnosis, 
            string? icd10Code, 
            string? doctorNotes, 
            DateTime? followUpDate, 
            bool completeConsultation)
        {
            var appt = await _consultationRepo.GetAppointmentDetailAsync(appointmentId);
            if (appt == null || appt.DoctorId != doctorId)
            {
                return (false, "Không tìm thấy thông tin ca khám.");
            }

            if (string.IsNullOrWhiteSpace(diagnosis))
            {
                return (false, "Vui lòng nhập chẩn đoán bệnh.");
            }

            var record = new MedicalRecord
            {
                AppointmentId = appointmentId,
                Diagnosis = diagnosis.Trim(),
                ICD10Code = string.IsNullOrWhiteSpace(icd10Code) ? null : icd10Code.Trim().ToUpper(),
                DoctorNotes = string.IsNullOrWhiteSpace(doctorNotes) ? null : doctorNotes.Trim(),
                FollowUpDate = followUpDate
            };

            bool saved = await _consultationRepo.SaveMedicalRecordAsync(record);
            if (!saved)
            {
                return (false, "Không thể lưu hồ sơ bệnh án, vui lòng thử lại.");
            }

            if (completeConsultation)
            {
                await _consultationRepo.UpdateAppointmentStatusAsync(appointmentId, "Completed");
                return (true, "Đã lưu bệnh án và hoàn thành ca khám!");
            }

            return (true, "Đã lưu tạm bệnh án!");
        }

        public async Task<(bool Success, string Message)> OrderServiceAsync(int appointmentId, int doctorId, int serviceId)
        {
            var appt = await _consultationRepo.GetAppointmentDetailAsync(appointmentId);
            if (appt == null || appt.DoctorId != doctorId)
            {
                return (false, "Không tìm thấy thông tin ca khám.");
            }

            var service = await _consultationRepo.GetServiceByIdAsync(serviceId);
            if (service == null || !service.IsActive)
            {
                return (false, "Dịch vụ cận lâm sàng không tồn tại hoặc đã ngừng hoạt động.");
            }

            var order = new ServiceOrder
            {
                AppointmentId = appointmentId,
                ServiceId = serviceId,
                UnitPrice = service.Price,
                Status = "Ordered"
            };

            bool added = await _consultationRepo.AddServiceOrderAsync(order);
            if (!added)
            {
                return (false, "Không thể thêm chỉ định dịch vụ.");
            }

            return (true, $"Đã chỉ định: {service.Name}");
        }

        public async Task<(bool Success, string Message)> UpdateServiceResultAsync(int serviceOrderId, int doctorId, string? resultSummary, bool markCompleted)
        {
            string status = markCompleted ? "Completed" : "Ordered";
            bool updated = await _consultationRepo.UpdateServiceOrderResultAsync(serviceOrderId, resultSummary, status);
            if (!updated)
            {
                return (false, "Không thể cập nhật kết quả xét nghiệm.");
            }

            return (true, "Đã cập nhật kết quả xét nghiệm thành công.");
        }

        public async Task<(bool Success, string Message)> DeleteServiceOrderAsync(int serviceOrderId, int doctorId)
        {
            bool deleted = await _consultationRepo.DeleteServiceOrderAsync(serviceOrderId);
            if (!deleted)
            {
                return (false, "Không thể xóa chỉ định dịch vụ.");
            }

            return (true, "Đã hủy chỉ định dịch vụ thành công.");
        }
    }
}

