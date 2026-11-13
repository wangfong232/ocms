using BusinessObjects.Entities;
using Services;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    public class DoctorSearchViewModel
    {
        public int? SelectedDepartmentId { get; set; }
        public string? SearchTerm { get; set; }
        public DateTime? TargetDate { get; set; } = DateTime.Today;

        public List<Department> Departments { get; set; } = new();
        public List<DoctorCardItem> DoctorCards { get; set; } = new();

    }
    public class DoctorCardItem
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public decimal PricePerSlot { get; set; }


        public ShiftCapacityMetrics MorningCapacity { get; set; } = new ShiftCapacityMetrics();
        public ShiftCapacityMetrics AfternoonCapacity { get; set; } = new ShiftCapacityMetrics();
    }

    public class SelectSlotViewModel
    {
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public decimal PricePerSlot { get; set; }
        public DateTime SelectedDate { get; set; }
        public List<SlotGridItem> MorningSlots { get; set; } = new();
        public List<SlotGridItem> AfternoonSlots { get; set; } = new();
        public ShiftCapacityMetrics DayCapacity { get; set; } = new();

        public SlotGridItem? CurrentHeldSlot { get; set; }
        public int RemainingSeconds { get; set; } = 0;
    }

    public class SlotGridItem
    {
        public int SlotId { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Status { get; set; } = "Open";    // Open, Hold, Booked
        public string RowVersion { get; set; } = string.Empty;
        public bool IsAvailable => Status == "Open";

        public bool IsHeldByCurrentUser { get; set; } = false;
        public bool IsClickable => Status == "Open" || IsHeldByCurrentUser;
    }

    public class HoldSlotPostModel
    {
        [Required]
        public int SlotId { get; set; }
        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }

    public class BookingConfirmViewModel
    {
        public int SlotId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal ConsultationFee { get; set; }

        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "Lý do khám không được vượt quá 500 ký tự.")]
        public string? Reason { get; set; }
    }

    public class ConfirmBookingPostModel
    {
        [Required]
        public int SlotId { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }
    }

    public class BookingSuccessViewModel
    {
        public int AppointmentId { get; set; }
        public string QrCodeToken { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal ConsultationFee { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string Status { get; set; } = "Confirmed";
    }
}
