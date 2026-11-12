using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository _doctorRepo;
        private readonly IUserRepository _userRepo;

        public DoctorService(IDoctorRepository doctorRepo, IUserRepository userRepo)
        {
            _doctorRepo = doctorRepo;
            _userRepo = userRepo;
        }

        public async Task<Doctor?> GetDoctorByIdAsync(int id)
        {
            return await _doctorRepo.GetByIdAsync(id);
        }

        public async Task<Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _doctorRepo.GetByUserIdAsync(userId);
        }

        public async Task<List<Doctor>> GetAllDoctorsAsync()
        {
            return await _doctorRepo.GetAllAsync();
        }

        public async Task<List<Room>> GetAvailableRoomsForDoctorAsync(int doctorId)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                return await _doctorRepo.GetAllActiveRoomsAsync();
            }

            var rooms = await _doctorRepo.GetRoomsByDepartmentIdAsync(doctor.DepartmentId);
            if (rooms.Count > 0)
            {
                return rooms;
            }
            return await _doctorRepo.GetAllActiveRoomsAsync();
        }

        public async Task<(bool Success, string Message)> UpdateDoctorProfileAsync(
            int doctorId, 
            string fullName, 
            string phoneNumber, 
            string academicTitle, 
            decimal pricePerSlot, 
            int? defaultRoomId)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                return (false, "Không tìm thấy hồ sơ bác sĩ.");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                return (false, "Họ và tên bác sĩ không được để trống.");
            }

            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return (false, "Số điện thoại không được để trống.");
            }

            if (pricePerSlot < 0)
            {
                return (false, "Giá khám mỗi lượt (PricePerSlot) không được nhỏ hơn 0.");
            }

            // Cập nhật thông tin chuyên môn & biểu phí
            doctor.AcademicTitle = string.IsNullOrWhiteSpace(academicTitle) ? "BS" : academicTitle.Trim();
            doctor.PricePerSlot = pricePerSlot;
            doctor.DefaultRoomId = defaultRoomId;

            // Cập nhật thông tin tài khoản người dùng
            if (doctor.User != null)
            {
                doctor.User.FullName = fullName.Trim();
                doctor.User.PhoneNumber = phoneNumber.Trim();
            }

            await _doctorRepo.UpdateAsync(doctor);
            return (true, "Cập nhật hồ sơ bác sĩ và biểu phí khám thành công!");
        }
        private readonly OcmsDbContext _dbContext;

        public DoctorService(OcmsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Doctor>> GetFeaturedDoctorsAsync(int count)
        {
            return await _dbContext.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Take(count)
                .ToListAsync();
        }
    }
    
}

   