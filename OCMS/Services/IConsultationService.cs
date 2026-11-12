using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IConsultationService
    {
        // 1. Lấy danh sách hàng đợi khám bệnh hôm nay của bác sĩ
        Task<List<Appointment>> GetTodayQueueAsync(int doctorId);

        // 2. Lấy thông tin chi tiết ca khám (Bệnh nhân, Sinh hiệu, Tiền sử, Bệnh án, Dịch vụ chỉ định)
        Task<Appointment?> GetConsultationDetailAsync(int appointmentId, int doctorId);

        // 3. Tiền sử các lần khám trước của bệnh nhân
        Task<List<MedicalRecord>> GetPatientPastMedicalHistoryAsync(int patientId, int currentAppointmentId);

        // 4. Danh sách xét nghiệm cận lâm sàng có sẵn
        Task<List<MedicalService>> GetActiveServicesAsync();

        // 5. Ghi nhận bệnh án EMR (Chẩn đoán, ICD-10, Ghi chú, Hẹn tái khám) và có thể hoàn tất ca khám
        Task<(bool Success, string Message)> SaveEMRAsync(
            int appointmentId, 
            int doctorId, 
            string diagnosis, 
            string? icd10Code, 
            string? doctorNotes, 
            DateTime? followUpDate, 
            bool completeConsultation);

        // 6. Chỉ định xét nghiệm nhanh cận lâm sàng
        Task<(bool Success, string Message)> OrderServiceAsync(int appointmentId, int doctorId, int serviceId);

        // 7. Cập nhật kết quả xét nghiệm nhanh
        Task<(bool Success, string Message)> UpdateServiceResultAsync(int serviceOrderId, int doctorId, string? resultSummary, bool markCompleted);

        // 8. Hủy chỉ định xét nghiệm
        Task<(bool Success, string Message)> DeleteServiceOrderAsync(int serviceOrderId, int doctorId);

        // 9. Bắt đầu ca khám (chuyển sang InConsultation)
        Task<(bool Success, string Message)> StartConsultationAsync(int appointmentId, int doctorId);
    }
}

