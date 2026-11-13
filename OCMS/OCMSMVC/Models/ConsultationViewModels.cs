using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    // 1. ViewModel hàng đợi khám
    public class ConsultationQueueItemViewModel
    {
        public int AppointmentId { get; set; }
        public int? QueueNumber { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public int BirthYear { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;
        public string? HealthInsuranceNumber { get; set; }
        public string TimeSlotDisplay { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string Status { get; set; } = "Arrived";
        public bool HasVitalSigns { get; set; }
        public bool HasMedicalRecord { get; set; }
        public bool IsNextInLine { get; set; }
    }

    // 2. ViewModel bàn khám bệnh (Workspace)
    public class ConsultationExamineViewModel
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public string TimeSlotDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? ChiefComplaint { get; set; } // Lý do khám

        // Thông tin bệnh nhân
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int Age => DateTime.Today.Year - DateOfBirth.Year;
        public string? Address { get; set; }
        public string? HealthInsuranceNumber { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;

        // Chỉ số sinh tồn (Vital Signs)
        public bool HasVitalSigns { get; set; }
        public int SystolicBP { get; set; }
        public int DiastolicBP { get; set; }
        public int Pulse { get; set; }
        public decimal Temperature { get; set; }
        public int SpO2 { get; set; }
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public decimal BMI => (Height > 0) ? Math.Round(Weight / ((Height / 100) * (Height / 100)), 1) : 0;

        // Bệnh án EMR hiện tại
        public int MedicalRecordId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập chẩn đoán bệnh")]
        [Display(Name = "Chẩn đoán lâm sàng")]
        public string Diagnosis { get; set; } = string.Empty;

        [Display(Name = "Mã bệnh ICD-10")]
        public string? ICD10Code { get; set; }

        [Display(Name = "Lời dặn & Ghi chú của Bác sĩ")]
        public string? DoctorNotes { get; set; }

        [Display(Name = "Ngày hẹn tái khám")]
        [DataType(DataType.Date)]
        public DateTime? FollowUpDate { get; set; }

        // Tiền sử bệnh án cũ
        public List<PastMedicalHistoryItemViewModel> PastHistory { get; set; } = new List<PastMedicalHistoryItemViewModel>();

        // Chỉ định cận lâm sàng / xét nghiệm nhanh
        public List<ServiceOrderItemViewModel> ServiceOrders { get; set; } = new List<ServiceOrderItemViewModel>();

        // Danh mục dịch vụ cận lâm sàng để bác sĩ chọn
        public List<MedicalServiceOptionViewModel> AvailableServices { get; set; } = new List<MedicalServiceOptionViewModel>();

        // Đơn thuốc điều trị
        public List<PrescriptionItemViewModel> Prescriptions { get; set; } = new List<PrescriptionItemViewModel>();

        // Danh mục thuốc có sẵn từ DB để tìm kiếm và chọn
        public List<MedicineOptionViewModel> AvailableMedicines { get; set; } = new List<MedicineOptionViewModel>();
    }

    public class PrescriptionItemViewModel
    {
        public int PrescriptionDetailId { get; set; }
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Unit { get; set; } = "Viên";
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => UnitPrice * Quantity;
        public string DosageInstruction { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
    }

    public class MedicineOptionViewModel
    {
        public int MedicineId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = "Viên";
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public string? Description { get; set; }
    }

    public class PastMedicalHistoryItemViewModel
    {
        public DateTime Date { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string? ICD10Code { get; set; }
        public string? DoctorNotes { get; set; }
    }

    public class ServiceOrderItemViewModel
    {
        public int ServiceOrderId { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public string? ResultSummary { get; set; }
        public string Status { get; set; } = "Ordered";
        public DateTime CreatedAt { get; set; }
    }

    public class MedicalServiceOptionViewModel
    {
        public int ServiceId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? DepartmentName { get; set; }
    }

    public class PatientPreviewViewModel
    {
        public int AppointmentId { get; set; }
        public int? QueueNumber { get; set; }
        public string TimeSlotDisplay { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }

        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public int Age => DateTime.Today.Year - DateOfBirth.Year;
        public string? Address { get; set; }
        public string? HealthInsuranceNumber { get; set; }
        public string PhoneNumber { get; set; } = string.Empty;

        // Sinh hiệu
        public bool HasVitalSigns { get; set; }
        public int SystolicBP { get; set; }
        public int DiastolicBP { get; set; }
        public int Pulse { get; set; }
        public decimal Temperature { get; set; }
        public int SpO2 { get; set; }
        public decimal Weight { get; set; }
        public decimal Height { get; set; }
        public decimal BMI => (Height > 0) ? Math.Round(Weight / ((Height / 100) * (Height / 100)), 1) : 0;

        // Tiền sử khám cũ
        public List<PastMedicalHistoryItemViewModel> PastHistory { get; set; } = new List<PastMedicalHistoryItemViewModel>();

        public bool CanCallNow { get; set; }
        public string? BlockReason { get; set; }
    }
}

