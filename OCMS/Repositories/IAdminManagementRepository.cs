using BusinessObjects.Entities;

namespace Repositories;

public interface IAdminManagementRepository
{
    Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Room>> GetRoomsAsync(CancellationToken cancellationToken = default);
    Task<User> CreateAccountAsync(User user, Doctor? doctor, CancellationToken cancellationToken = default);
    Task<bool> ToggleAccountAsync(int userId, int currentAdminId, CancellationToken cancellationToken = default);
    Task SaveDepartmentAsync(Department department, CancellationToken cancellationToken = default);
    Task<bool> DeleteDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    Task SaveRoomAsync(Room room, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoomAsync(int roomId, CancellationToken cancellationToken = default);
}
