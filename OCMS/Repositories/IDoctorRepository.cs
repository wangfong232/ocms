using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
       Task<List<Doctor>> GetDoctorsAsync(int? department, string? searchTerm);
        Task<Doctor?> GetDoctorWithDetailsAsync(int doctorId);
        Task<List<Department>> GetAllDepartmentAsync();
    }
}

