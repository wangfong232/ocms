using BusinessObjects.Entities;

namespace Services;

public interface IAdminManagementService
{
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default);
    Task CreateAccountAsync(string fullName, string email, string phone, string password, string role, int? departmentId, int? roomId, string? academicTitle, decimal? pricePerSlot, CancellationToken cancellationToken = default);
    Task ToggleAccountAsync(int userId, int currentAdminId, CancellationToken cancellationToken = default);
    Task SaveDepartmentAsync(Department department, CancellationToken cancellationToken = default);
    Task DeleteDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    Task SaveRoomAsync(Room room, CancellationToken cancellationToken = default);
    Task DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default);
}
