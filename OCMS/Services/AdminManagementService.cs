using BusinessObjects.Entities;
using Repositories;

namespace Services;

public class AdminManagementService : IAdminManagementService
{
    private static readonly string[] AllowedRoles = ["Doctor", "Staff", "Admin"];
    private readonly IAdminManagementRepository _repository;
    public AdminManagementService(IAdminManagementRepository repository) => _repository = repository;

    public Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default) => _repository.GetUsersAsync(cancellationToken);
    public Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default) => _repository.GetDepartmentsAsync(cancellationToken);
    public Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default) => _repository.GetRoomsAsync(cancellationToken);

    public async Task CreateAccountAsync(string fullName, string email, string phone, string password, string role, int? departmentId, int? roomId, string? academicTitle, decimal? pricePerSlot, CancellationToken cancellationToken = default)
    {
        fullName = fullName.Trim(); email = email.Trim().ToLowerInvariant(); phone = phone.Trim(); role = role.Trim();
        if (!AllowedRoles.Contains(role)) throw new InvalidOperationException("Chỉ được tạo tài khoản Doctor, Staff hoặc Admin.");
        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(phone) || password.Length < 8)
            throw new InvalidOperationException("Họ tên, email, số điện thoại và mật khẩu tối thiểu 8 ký tự là bắt buộc.");
        if (role == "Doctor" && departmentId is null) throw new InvalidOperationException("Chọn khoa cho tài khoản bác sĩ.");
        if (pricePerSlot is < 0) throw new InvalidOperationException("Phí khám không thể âm.");
        var user = new User { FullName = fullName, Email = email, PhoneNumber = phone, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password), Role = role, IsActive = true, CreatedAt = DateTime.UtcNow };
        Doctor? doctor = role == "Doctor" ? new Doctor { DepartmentId = departmentId!.Value, DefaultRoomId = roomId, AcademicTitle = string.IsNullOrWhiteSpace(academicTitle) ? "BS" : academicTitle.Trim(), PricePerSlot = pricePerSlot ?? 150000m } : null;
        await _repository.CreateAccountAsync(user, doctor, cancellationToken);
    }

    public async Task ToggleAccountAsync(int userId, int currentAdminId, CancellationToken cancellationToken = default)
    { if (!await _repository.ToggleAccountAsync(userId, currentAdminId, cancellationToken)) throw new InvalidOperationException("Không tìm thấy tài khoản."); }

    public async Task SaveDepartmentAsync(Department department, CancellationToken cancellationToken = default)
    {
        department.Code = department.Code.Trim().ToUpperInvariant(); department.Name = department.Name.Trim();
        if (string.IsNullOrWhiteSpace(department.Code) || string.IsNullOrWhiteSpace(department.Name) || department.BaseConsultationFee < 0)
            throw new InvalidOperationException("Mã, tên khoa và phí khám hợp lệ là bắt buộc.");
        await _repository.SaveDepartmentAsync(department, cancellationToken);
    }
    public async Task DeleteDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
    { if (!await _repository.DeleteDepartmentAsync(departmentId, cancellationToken)) throw new InvalidOperationException("Không tìm thấy khoa."); }

    public async Task SaveRoomAsync(Room room, CancellationToken cancellationToken = default)
    {
        room.RoomNumber = room.RoomNumber.Trim(); room.RoomType = room.RoomType.Trim();
        if (string.IsNullOrWhiteSpace(room.RoomNumber) || string.IsNullOrWhiteSpace(room.RoomType) || room.DepartmentId <= 0)
            throw new InvalidOperationException("Mã phòng, loại phòng và khoa là bắt buộc.");
        await _repository.SaveRoomAsync(room, cancellationToken);
    }
    public async Task DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default)
    { if (!await _repository.DeleteRoomAsync(roomId, cancellationToken)) throw new InvalidOperationException("Không tìm thấy phòng."); }
}
