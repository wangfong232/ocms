using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class DoctorController : Controller
    {
        private readonly IDoctorService _doctorService;

        public DoctorController(IDoctorService doctorService)
        {
            _doctorService = doctorService;
        }

        // 1. Danh sách Bác sĩ & Biểu phí niêm yết (Transparent Pricing)
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var doctors = await _doctorService.GetAllDoctorsAsync();

            var modelList = new List<DoctorListItemViewModel>();
            foreach (var d in doctors)
            {
                modelList.Add(new DoctorListItemViewModel
                {
                    DoctorId = d.Id,
                    FullName = d.User?.FullName ?? "N/A",
                    Email = d.User?.Email ?? "N/A",
                    PhoneNumber = d.User?.PhoneNumber ?? "N/A",
                    AcademicTitle = d.AcademicTitle,
                    DepartmentName = d.Department?.Name ?? "Chưa phân khoa",
                    DefaultRoomNumber = d.DefaultRoom?.RoomNumber ?? "Chưa xếp",
                    PricePerSlot = d.PricePerSlot
                });
            }

            return View(modelList);
        }

        // 2. Lấy Partial View Popup Profile cho Modal
        [HttpGet]
        public async Task<IActionResult> GetProfileModal()
        {
            var role = HttpContext.Session.GetString("Role");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (role != "Doctor" || !userId.HasValue)
            {
                return Unauthorized();
            }

            var doctor = await _doctorService.GetDoctorByUserIdAsync(userId.Value);
            if (doctor == null)
            {
                return NotFound();
            }

            var rooms = await _doctorService.GetAvailableRoomsForDoctorAsync(doctor.Id);

            var model = new DoctorProfileViewModel
            {
                DoctorId = doctor.Id,
                UserId = doctor.UserId,
                FullName = doctor.User?.FullName ?? string.Empty,
                Email = doctor.User?.Email ?? string.Empty,
                PhoneNumber = doctor.User?.PhoneNumber ?? string.Empty,
                DepartmentName = doctor.Department?.Name ?? "Chưa phân khoa",
                DepartmentId = doctor.DepartmentId,
                AcademicTitle = doctor.AcademicTitle,
                PricePerSlot = doctor.PricePerSlot,
                DefaultRoomId = doctor.DefaultRoomId,
                IsAdminEditing = false,
                AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList()
            };

            return PartialView("_ProfileModal", model);
        }

        // 3. Cập nhật Hồ sơ & Biểu phí của Bác sĩ (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(DoctorProfileViewModel model, string? returnUrl = null)
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Doctor" && role != "Admin")
            {
                return RedirectToAction("Login", "Auth");
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Nếu vai trò là Bác sĩ thì chỉ được sửa hồ sơ của chính mình
            if (role == "Doctor")
            {
                var currentDoctor = await _doctorService.GetDoctorByUserIdAsync(userId.Value);
                if (currentDoctor == null || currentDoctor.Id != model.DoctorId)
                {
                    return RedirectToAction("AccessDenied", "Auth");
                }
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Thông tin không hợp lệ, vui lòng kiểm tra lại.";
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                var rooms = await _doctorService.GetAvailableRoomsForDoctorAsync(model.DoctorId);
                model.AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList();
                return View(model);
            }

            var result = await _doctorService.UpdateDoctorProfileAsync(
                model.DoctorId,
                model.FullName,
                model.PhoneNumber,
                model.AcademicTitle,
                model.PricePerSlot,
                model.DefaultRoomId);

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                ModelState.AddModelError(string.Empty, result.Message);
                var rooms = await _doctorService.GetAvailableRoomsForDoctorAsync(model.DoctorId);
                model.AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList();
                return View(model);
            }

            // Đồng bộ tên hiển thị trong Session nếu Bác sĩ tự sửa
            if (role == "Doctor")
            {
                HttpContext.Session.SetString("FullName", model.FullName.Trim());
            }

            TempData["SuccessMessage"] = "Cập nhật hồ sơ & biểu phí thành công!";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Profile), new { id = model.DoctorId });
        }
    }
}

