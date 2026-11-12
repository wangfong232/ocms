using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories
{
    public class DoctorRepository : IDoctorRepository
    {
        private readonly OcmsDbContext _context;

        public DoctorRepository(OcmsDbContext context)
        {
            _context = context;
        }

        public async Task<Doctor?> GetByIdAsync(int id)
        {
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Include(d => d.DefaultRoom)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task<Doctor?> GetByUserIdAsync(int userId)
        {
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Include(d => d.DefaultRoom)
                .FirstOrDefaultAsync(d => d.UserId == userId);
        }

        public async Task<List<Doctor>> GetAllAsync()
        {
            return await _context.Doctors
                .Include(d => d.User)
                .Include(d => d.Department)
                .Include(d => d.DefaultRoom)
                .ToListAsync();
        }

        public async Task UpdateAsync(Doctor doctor)
        {
            _context.Doctors.Update(doctor);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Room>> GetRoomsByDepartmentIdAsync(int departmentId)
        {
            return await _context.Rooms
                .Where(r => r.DepartmentId == departmentId && r.IsOperational)
                .ToListAsync();
        }

        public async Task<List<Room>> GetAllActiveRoomsAsync()
        {
            return await _context.Rooms
                .Where(r => r.IsOperational)
                .ToListAsync();
        }
 

        public async Task<List<Department>> GetAllDepartmentAsync()
        {
            return await _context.Departments.ToListAsync();
        }

        public async Task<List<Doctor>> GetDoctorsAsync(int? departmentId, string? searchTerm)
        {
            var query = _context.Doctors.Include(d => d.User)
                          .Include(d => d.Department)
                          .Include(d => d.DefaultRoom).AsQueryable();

            if (departmentId.HasValue && departmentId.Value > 0)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }
            if(!string.IsNullOrEmpty(searchTerm)){
                var term = searchTerm.ToLower();
                query = query.Where(d => d.User.FullName.ToLower().Contains(term) || d.Department.Name.ToLower().Contains(term));
            }
            return await query.ToListAsync();
        }

        public async Task<Doctor?> GetDoctorWithDetailsAsync(int doctorId)
        {
            return await _context.Doctors
                 .Include(d => d.User)
                 .Include(d => d.Department)
                 .Include(d => d.DefaultRoom)
                 .Include(d => d.Schedules.Where(s => s.Status == "Active"))
                 .ThenInclude(s => s.TimeSlots)
                 .FirstOrDefaultAsync(d => d.Id == doctorId);
        }
    }
}
