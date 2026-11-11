using BusinessObjects.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Repositories
{
    public interface IDoctorRepository
    {
        Task<Doctor?> GetByIdAsync(int id);
        Task<Doctor?> GetByUserIdAsync(int userId);
        Task<List<Doctor>> GetAllAsync();
        Task UpdateAsync(Doctor doctor);
        Task<List<Room>> GetRoomsByDepartmentIdAsync(int departmentId);
        Task<List<Room>> GetAllActiveRoomsAsync();
    }
}

