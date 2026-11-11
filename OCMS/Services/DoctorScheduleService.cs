using BusinessObjects.Entities;
using Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class DoctorScheduleService : IDoctorScheduleService
    {
        private readonly IDoctorScheduleRepository _scheduleRepo;
        private readonly IDoctorRepository _doctorRepo;

        public DoctorScheduleService(IDoctorScheduleRepository scheduleRepo, IDoctorRepository doctorRepo)
        {
            _scheduleRepo = scheduleRepo;
            _doctorRepo = doctorRepo;
        }

        public async Task<DoctorSchedule?> GetByIdAsync(int id)
        {
            return await _scheduleRepo.GetByIdAsync(id);
        }

        public async Task<List<DoctorSchedule>> GetMySchedulesAsync(int doctorId)
        {
            return await _scheduleRepo.GetSchedulesByDoctorIdAsync(doctorId);
        }

        public async Task<List<DoctorSchedule>> GetWeeklyRosterAsync(int doctorId, DateTime anyDateInWeek)
        {
            // Tính ngày thứ 2 (Monday) của tuần
            int diff = (7 + (anyDateInWeek.DayOfWeek - DayOfWeek.Monday)) % 7;
            DateTime monday = anyDateInWeek.AddDays(-1 * diff).Date;
            DateTime sunday = monday.AddDays(6).Date;

            return await _scheduleRepo.GetSchedulesByDoctorAndDateRangeAsync(doctorId, monday, sunday);
        }

        public async Task<Doctor?> GetDoctorByUserIdAsync(int userId)
        {
            return await _doctorRepo.GetByUserIdAsync(userId);
        }

        public async Task<List<Room>> GetAvailableRoomsForDoctorAsync(int doctorId)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                return await _doctorRepo.GetAllActiveRoomsAsync();
            }

            // Ưu tiên lấy phòng thuộc chuyên khoa của bác sĩ, nếu không có thì lấy tất cả phòng hoạt động
            var deptRooms = await _doctorRepo.GetRoomsByDepartmentIdAsync(doctor.DepartmentId);
            if (deptRooms.Count > 0)
            {
                return deptRooms;
            }
            return await _doctorRepo.GetAllActiveRoomsAsync();
        }

        public async Task<(bool Success, string Message, DoctorSchedule? Schedule)> RegisterShiftAsync(int doctorId, DateTime workDate, string shiftType, int roomId)
        {
            // 1. Kiểm tra loại ca trực hợp lệ
            if (shiftType != "Morning" && shiftType != "Afternoon")
            {
                return (false, "Loại ca trực không hợp lệ. Vui lòng chọn Ca sáng hoặc Ca chiều.", null);
            }

            // 2. Kiểm tra thời gian >= 24h trước khi ca trực bắt đầu
            TimeSpan startTimeSpan = shiftType == "Morning" ? new TimeSpan(7, 30, 0) : new TimeSpan(13, 0, 0);
            DateTime shiftStartDateTime = workDate.Date.Add(startTimeSpan);

            if (shiftStartDateTime < DateTime.Now.AddHours(24))
            {
                return (false, "Ca trực phải được đăng ký trước ít nhất 24 giờ tính từ thời điểm hiện tại.", null);
            }

            // 3. Kiểm tra bác sĩ đã đăng ký ca này trong ngày chưa
            bool doctorShiftExists = await _scheduleRepo.ExistsShiftAsync(doctorId, workDate, shiftType);
            if (doctorShiftExists)
            {
                return (false, "Bạn đã đăng ký ca trực này trong ngày rồi.", null);
            }

            // 4. Kiểm tra phòng khám đã có bác sĩ khác đăng ký chưa
            bool roomOccupied = await _scheduleRepo.IsRoomOccupiedAsync(roomId, workDate, shiftType);
            if (roomOccupied)
            {
                return (false, "Phòng khám đã được bác sĩ khác đăng ký trong ca này. Vui lòng chọn phòng khác.", null);
            }

            // 5. Khởi tạo DoctorSchedule
            var schedule = new DoctorSchedule
            {
                DoctorId = doctorId,
                RoomId = roomId,
                WorkDate = workDate.Date,
                ShiftType = shiftType,
                MaxQuota = 16,
                Status = "Active"
            };

            // 6. Tự động sinh 16 discrete 15-minute TimeSlots
            int startMinutes = shiftType == "Morning" ? (7 * 60 + 30) : (13 * 60);

            for (int i = 0; i < 16; i++)
            {
                var slotStart = TimeSpan.FromMinutes(startMinutes + (i * 15));
                var slotEnd = slotStart.Add(TimeSpan.FromMinutes(15));

                var timeSlot = new TimeSlot
                {
                    StartTime = slotStart,
                    EndTime = slotEnd,
                    Status = "Open"
                };

                schedule.TimeSlots.Add(timeSlot);
            }

            // 7. Lưu vào CSDL
            var createdSchedule = await _scheduleRepo.AddAsync(schedule);
            return (true, "Đăng ký ca trực thành công! Hệ thống đã tự động tạo 16 khung giờ khám (15 phút/khung giờ).", createdSchedule);
        }

        public async Task<(bool Success, string Message)> CancelShiftAsync(int scheduleId, int doctorId)
        {
            var schedule = await _scheduleRepo.GetByIdAsync(scheduleId);
            if (schedule == null)
            {
                return (false, "Không tìm thấy thông tin ca trực.");
            }

            if (schedule.DoctorId != doctorId)
            {
                return (false, "Bạn không có quyền thao tác trên ca trực của bác sĩ khác.");
            }

            if (schedule.Status == "Cancelled")
            {
                return (false, "Ca trực này đã được hủy trước đó.");
            }

            // Kiểm tra xem ca trực đã có lịch hẹn chưa
            bool hasBookings = await _scheduleRepo.HasBookedAppointmentsAsync(scheduleId);
            if (hasBookings)
            {
                return (false, "Không thể hủy ca trực khẩn cấp vì đã có bệnh nhân đặt lịch hẹn.");
            }

            // Cập nhật trạng thái ca trực và các slot
            schedule.Status = "Cancelled";
            foreach (var slot in schedule.TimeSlots)
            {
                if (slot.Status == "Open")
                {
                    slot.Status = "Cancelled";
                }
            }

            await _scheduleRepo.UpdateAsync(schedule);
            return (true, "Hủy ca trực thành công.");
        }
    }
}

