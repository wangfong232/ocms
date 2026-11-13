using Services;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    public class MyAppointmentsViewModel
    {
        public string PatientFullName { get; set; } = string.Empty;
        public List<AppointmentItemViewModel> UpcomingAppointments { get; set; } = new();
        public List<AppointmentItemViewModel> PastAppointments { get; set; } = new();
    }

    public class AppointmentItemViewModel
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public decimal ConsultationFee { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? CancellationReason { get; set; }
        public string QrCodeToken { get; set; } = string.Empty;
        public int? QueueNumber { get; set; }
        public int RescheduledCount { get; set; } = 0;

        public bool CanReschedule
        {
            get
            {
                var appointmentDateTime = AppointmentDate.Date.Add(StartTime);
                return RescheduledCount < 1 && (Status == "Confirmed" || Status == "Pending") && appointmentDateTime > DateTime.Now;
            }
        }

        public bool CanCancel
        {
            get
            {
                var appointmentDateTime = AppointmentDate.Date.Add(StartTime);
                return (Status == "Confirmed" || Status == "Pending") &&
                       appointmentDateTime > DateTime.Now;
            }
        }

        public double HoursUntilAppointment
        {
            get
            {
                var appointmentDateTime = AppointmentDate.Date.Add(StartTime);
                return (appointmentDateTime - DateTime.Now).TotalHours;
            }
        }

        public bool IsEligibleForRefund => HoursUntilAppointment >= 24;

        public string StatusBadgeClass => Status switch
        {
            "Confirmed" => "bg-success",
            "Pending" => "bg-warning text-dard",
            "Arrived" => "bg-primary",
            "Completed" => "bg-secondary",
            "Cancelled" => "bg-danger",
            _ => "bg-light text-dark"
        };

        public string StatusDisplayName => Status switch
        {
            "Confirmed" => "Đã xác nhận",
            "Pending" => "Chờ xác nhận",
            "Arrived" => "Đã đến (Check-in)",
            "InConsultation" => "Đang khám",
            "Completed" => "Hoàn thành",
            "Cancelled" => "Đã hủy",
            _ => Status
        };
    }

    public class CancelAppointmentPostModel
    {
        [Required]
        public int AppointmentId { get; set; }
        [MaxLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
        public string? CancellationReason { get; set; }
    }

    public class RescheduleViewModel
    {
        public int AppointmentId { get; set; }
        public int DoctorId { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string RoomNumber { get; set; } = string.Empty;
        public decimal PricePerSlot { get; set; }
        public DateTime CurrentAppointmentDate { get; set; }
        public TimeSpan CurrentStartTime { get; set; }
        public TimeSpan CurrentEndTime { get; set; }
        public int CurrentSlotId { get; set; }
        public int RescheduledCount { get; set; }
        public DateTime SelectedDate { get; set; }
        public List<SlotGridItem> MorningSlots { get; set; } = new();
        public List<SlotGridItem> AfternoonSlots { get; set; } = new();
        public ShiftCapacityMetrics DayCapacity { get; set; } = new();
    }

    public class ConfirmReschedulePostModel
    {
        [Required]
        public int AppointmentId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn một khung giờ mới.")]
        public int NewSlotId { get; set; }
    }

    public class AppointmentDetailViewModel
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
        public string? CancellationReason { get; set; }
        public string Status { get; set; } = "Confirmed";
        public int? QueueNumber { get; set; }
        public int RescheduledCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
