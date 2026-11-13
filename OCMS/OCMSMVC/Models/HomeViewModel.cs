using BusinessObjects.Entities;

namespace OCMSMVC.Models
{
    public class HomeViewModel
    {
        public IEnumerable<Department> Specialties { get; set; } = new List<Department>();

        public IEnumerable<Doctor> FeaturedDoctors { get; set; } = new List<Doctor>();
    }
}