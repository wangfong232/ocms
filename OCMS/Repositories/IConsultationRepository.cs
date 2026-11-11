using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories
{
    public interface IConsultationRepository
    {
        // 1. Danh sách bệnh nhân trong ngày của bác sĩ
        Task<List<Appointment>> GetTodayAppointmentsByDoctorIdAsync(int doctorId, DateTime date);

        // 2. Chi tiết 1 ca khám cụ thể (kèm bệnh nhân, sinh hiệu, bệnh án, xét nghiệm)
        Task<Appointment?> GetAppointmentDetailAsync(int appointmentId);

        // 3. Tiền sử các lần khám trước của bệnh nhân
        Task<List<MedicalRecord>> GetPatientPastMedicalHistoryAsync(int patientId, int currentAppointmentId);

        // 4. Danh mục dịch vụ cận lâm sàng / xét nghiệm nhanh có sẵn
        Task<List<MedicalService>> GetActiveServicesAsync();
        Task<MedicalService?> GetServiceByIdAsync(int serviceId);

        // 5. Ghi nhận bệnh án EMR (Thêm mới hoặc Cập nhật)
        Task<bool> SaveMedicalRecordAsync(MedicalRecord medicalRecord);

        // 6. Chỉ định dịch vụ xét nghiệm (Tạo / Cập nhật kết quả / Xóa khi chưa thanh toán)
        Task<bool> AddServiceOrderAsync(ServiceOrder serviceOrder);
        Task<bool> UpdateServiceOrderResultAsync(int serviceOrderId, string? resultSummary, string status);
        Task<bool> DeleteServiceOrderAsync(int serviceOrderId);

        // 7. Cập nhật trạng thái lịch hẹn (InConsultation, Completed)
        Task<bool> UpdateAppointmentStatusAsync(int appointmentId, string status);
    }
}
