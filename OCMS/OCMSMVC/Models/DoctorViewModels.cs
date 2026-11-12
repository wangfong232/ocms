using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OCMSMVC.Models
{
    public class DoctorProfileViewModel
    {
        public int DoctorId { get; set; }
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [Display(Name = "Họ và tên bác sĩ")]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Display(Name = "Số điện thoại")]
        [RegularExpression(@"^0\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ (bắt đầu bằng 0, gồm 10-11 chữ số)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "Chuyên khoa")]
        public string DepartmentName { get; set; } = string.Empty;
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn hoặc nhập học hàm/học vị")]
        [Display(Name = "Học hàm / Học vị")]
        [MaxLength(50)]
        public string AcademicTitle { get; set; } = "BS"; // BS, ThS.BS, CK1, BSCK2, TS.BS, PGS.TS.BS...

        [Required(ErrorMessage = "Vui lòng nhập giá khám")]
        [Display(Name = "Giá khám mỗi slot (PricePerSlot)")]
        [Range(0, 10000000, ErrorMessage = "Giá khám phải từ 0 đến 10.000.000 VNĐ")]
        public decimal PricePerSlot { get; set; } = 150000;

        [Display(Name = "Phòng khám mặc định")]
        public int? DefaultRoomId { get; set; }

        public List<RoomItemViewModel> AvailableRooms { get; set; } = new List<RoomItemViewModel>();

        public bool IsAdminEditing { get; set; } = false;
    }

    public class DoctorListItemViewModel
    {
        public int DoctorId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string AcademicTitle { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string DefaultRoomNumber { get; set; } = string.Empty;
        public decimal PricePerSlot { get; set; }
    }
}

