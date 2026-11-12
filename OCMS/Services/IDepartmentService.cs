using BusinessObjects.Entities;

namespace Services
{
    public interface IDepartmentService
    {
        Task<IEnumerable<Department>> GetAllDepartmentsAsync();
    }
}