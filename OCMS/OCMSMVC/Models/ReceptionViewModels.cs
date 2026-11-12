using System;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    public class WalkInViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên bệnh nhân")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Giới tính")]
        public string Gender { get; set; } = "Male";

        [Required(ErrorMessage = "Vui lòng nhập ngày tháng năm sinh")]
        [DataType(DataType.Date)]
        [Display(Name = "Ngày sinh")]
        public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-25);

        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn bác sĩ khám")]
        [Display(Name = "Bác sĩ phụ trách")]
        public int DoctorId { get; set; }

        [Display(Name = "Khung giờ khám còn trống (Filler Slot)")]
        public int? TimeSlotId { get; set; }

        [Display(Name = "Tiếp nhận khẩn cấp / Cấp cứu")]
        public bool IsEmergency { get; set; } = false;

        [Display(Name = "Triệu chứng ban đầu / Lý do khám")]
        public string? Reason { get; set; }
    }
}
