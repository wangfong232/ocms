using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    public class RegisterShiftViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn ngày làm việc")]
        [Display(Name = "Ngày làm việc")]
        [DataType(DataType.Date)]
        public DateTime WorkDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Vui lòng chọn ca trực")]
        [Display(Name = "Ca trực")]
        public string ShiftType { get; set; } = "Morning"; // Morning (07:30 - 11:30), Afternoon (13:00 - 17:00)

        [Required(ErrorMessage = "Vui lòng chọn phòng khám")]
        [Display(Name = "Phòng khám")]
        public int RoomId { get; set; }

        public List<RoomItemViewModel> AvailableRooms { get; set; } = new List<RoomItemViewModel>();
    }

    public class RoomItemViewModel
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
    }

    public class DoctorScheduleItemViewModel
    {
        public int Id { get; set; }
        public DateTime WorkDate { get; set; }
        public string ShiftType { get; set; } = string.Empty;
        public string ShiftDisplayName => ShiftType == "Morning" ? "Ca Sáng (07:30 - 11:30)" : "Ca Chiều (13:00 - 17:00)";
        public string RoomNumber { get; set; } = string.Empty;
        public int MaxQuota { get; set; } = 16;
        public string Status { get; set; } = "Active";
        public int BookedSlotsCount { get; set; }
        public bool CanCancel { get; set; }
        public List<TimeSlotItemViewModel> TimeSlots { get; set; } = new List<TimeSlotItemViewModel>();
    }

    public class TimeSlotItemViewModel
    {
        public int Id { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string TimeDisplay => $"{StartTime:hh\\:mm} - {EndTime:hh\\:mm}";
        public string Status { get; set; } = "Open"; // Open, Hold, Booked, Cancelled
    }
}

