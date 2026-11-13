using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public interface ITimeSlotRepository
    {
        Task<List<TimeSlot>> GetSlotsByDoctorAndDateAsync(int doctorId, DateTime targetDate);
        Task<TimeSlot?> GetSlotByIdAsync(int slotId);
        Task<(bool success, string? newRowVersion)> HoldSlotAsync(int slotId, byte[] expectedRowVersion);
        Task<string?> ReleaseSlotAsync(int slotId);
    }
}
