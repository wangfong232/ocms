using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace Repositories;

public class AdminManagementRepository : IAdminManagementRepository
{
    private readonly OcmsDbContext _db;
    public AdminManagementRepository(OcmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await _db.Users.AsNoTracking().Include(u => u.Doctor).ThenInclude(d => d!.Department)
            .OrderBy(u => u.Role).ThenBy(u => u.FullName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default) =>
        await _db.Departments.AsNoTracking().Include(d => d.Doctors).Include(d => d.Rooms).Include(d => d.MedicalServices)
            .OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default) =>
        await _db.Rooms.AsNoTracking().Include(r => r.Department).Include(r => r.Schedules)
            .OrderBy(r => r.Department.Name).ThenBy(r => r.RoomNumber).ToListAsync(cancellationToken);

    public async Task<User> CreateAccountAsync(User user, Doctor? doctor, CancellationToken cancellationToken = default)
    {
        if (doctor != null)
        {
            var departmentExists = await _db.Departments.AnyAsync(d => d.Id == doctor.DepartmentId, cancellationToken);
            if (!departmentExists) throw new InvalidOperationException("Khoa được chọn không tồn tại.");
            if (doctor.DefaultRoomId is int roomId && !await _db.Rooms.AnyAsync(r => r.Id == roomId && r.DepartmentId == doctor.DepartmentId && r.IsOperational, cancellationToken))
                throw new InvalidOperationException("Phòng mặc định phải đang hoạt động và thuộc khoa đã chọn.");
        }
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        if (doctor != null)
        {
            doctor.UserId = user.Id;
            _db.Doctors.Add(doctor);
            await _db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    public async Task<bool> ToggleAccountAsync(int userId, int currentAdminId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user == null) return false;
        if (user.Role == "Patient") throw new InvalidOperationException("Màn hình này quản lý tài khoản nhân viên, bác sĩ và Admin.");
        if (userId == currentAdminId && user.IsActive) throw new InvalidOperationException("Không thể khóa tài khoản Admin đang đăng nhập.");
        if (user.Role == "Admin" && user.IsActive)
        {
            var activeAdmins = await _db.Users.CountAsync(u => u.Role == "Admin" && u.IsActive, cancellationToken);
            if (activeAdmins <= 1) throw new InvalidOperationException("Không thể khóa Admin hoạt động cuối cùng.");
        }
        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task SaveDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        if (department.Id == 0) _db.Departments.Add(department);
        else
        {
            var current = await _db.Departments.SingleOrDefaultAsync(d => d.Id == department.Id, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy khoa cần sửa.");
            current.Code = department.Code; current.Name = department.Name; current.Description = department.Description;
            current.BaseConsultationFee = department.BaseConsultationFee;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
    {
        var department = await _db.Departments.SingleOrDefaultAsync(d => d.Id == departmentId, cancellationToken);
        if (department == null) return false;
        if (await _db.Doctors.AnyAsync(d => d.DepartmentId == departmentId, cancellationToken) ||
            await _db.Rooms.AnyAsync(r => r.DepartmentId == departmentId, cancellationToken) ||
            await _db.MedicalServices.AnyAsync(s => s.DepartmentId == departmentId, cancellationToken))
            throw new InvalidOperationException("Không thể xóa khoa đang có bác sĩ, phòng hoặc dịch vụ liên kết.");
        _db.Departments.Remove(department);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SaveRoomAsync(Room room, CancellationToken cancellationToken = default)
    {
        if (!await _db.Departments.AnyAsync(d => d.Id == room.DepartmentId, cancellationToken))
            throw new InvalidOperationException("Khoa được chọn không tồn tại.");
        if (room.Id == 0) _db.Rooms.Add(room);
        else
        {
            var current = await _db.Rooms.SingleOrDefaultAsync(r => r.Id == room.Id, cancellationToken)
                ?? throw new InvalidOperationException("Không tìm thấy phòng cần sửa.");
            if (current.DepartmentId != room.DepartmentId && await _db.Doctors.AnyAsync(d => d.DefaultRoomId == room.Id && d.DepartmentId != room.DepartmentId, cancellationToken))
                throw new InvalidOperationException("Không thể chuyển phòng sang khoa khác khi phòng đang là phòng mặc định của bác sĩ.");
            current.RoomNumber = room.RoomNumber; current.DepartmentId = room.DepartmentId;
            current.RoomType = room.RoomType; current.IsOperational = room.IsOperational;
        }
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        var room = await _db.Rooms.SingleOrDefaultAsync(r => r.Id == roomId, cancellationToken);
        if (room == null) return false;
        if (await _db.DoctorSchedules.AnyAsync(s => s.RoomId == roomId, cancellationToken) ||
            await _db.Doctors.AnyAsync(d => d.DefaultRoomId == roomId, cancellationToken))
            throw new InvalidOperationException("Không thể xóa phòng đã được gán cho bác sĩ hoặc lịch làm việc.");
        _db.Rooms.Remove(room);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
