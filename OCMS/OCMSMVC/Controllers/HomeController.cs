using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services; 

namespace OCMSMVC.Controllers
{
    public class HomeController : Controller
    {
        private readonly IDepartmentService _departmentService;
        private readonly IDoctorService _doctorService;

        public HomeController(IDepartmentService departmentService, IDoctorService doctorService)
        {
            _departmentService = departmentService;
            _doctorService = doctorService;
        }

        public async Task<IActionResult> Index()
        {
            var specialties = await _departmentService.GetAllDepartmentsAsync();
            var featuredDoctors = await _doctorService.GetFeaturedDoctorsAsync(4);

            var viewModel = new HomeViewModel
            {
                Specialties = specialties,
                FeaturedDoctors = featuredDoctors
            };

            return View(viewModel);
        }
    }
}