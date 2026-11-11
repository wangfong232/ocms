using BusinessObjects.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IDoctorService
    {
        Task<Doctor?> GetDoctorByIdAsync(int id);
        Task<Doctor?> GetDoctorByUserIdAsync(int userId);
        Task<List<Doctor>> GetAllDoctorsAsync();
        Task<(bool Success, string Message)> UpdateDoctorProfileAsync(int doctorId, string fullName, string phoneNumber, string academicTitle, decimal pricePerSlot, int? defaultRoomId);
        Task<List<Room>> GetAvailableRoomsForDoctorAsync(int doctorId);
    }
}

