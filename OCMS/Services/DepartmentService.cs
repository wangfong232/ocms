using BusinessObjects.Entities;
using DataAccessObjects; 
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class DepartmentService : IDepartmentService
    {
        private readonly OcmsDbContext _dbContext;

        public DepartmentService(OcmsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync()
        {
            return await _dbContext.Departments.ToListAsync();
        }
    }
}