using BusinessObjects.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OCMSMVC.Filters;
using OCMSMVC.Models;
using Services;

namespace OCMSMVC.Controllers;

[RoleAuthorize("Admin")]
public class AdminController : Controller
{
    private readonly IAdminManagementService _admin;
    private readonly IAnalyticsService _analyticsService;
    public AdminController(IAdminManagementService admin, IAnalyticsService analyticsService)
    {
        _admin = admin; 
        _analyticsService = analyticsService;
    }

    public async Task<IActionResult> Dashboard(string startDate, string endDate)
    {
        if (!RequireAdmin()) return Denied();
        DateTime start = string.IsNullOrEmpty(startDate) ? DateTime.Now.AddDays(-7).Date : DateTime.Parse(startDate).Date;
        DateTime end = string.IsNullOrEmpty(endDate) ? DateTime.Now : DateTime.Parse(endDate).Date.AddDays(1).AddTicks(-1);

        var hourlyToday = await _analyticsService.GetTodayHourlyRevenueAsync();

        var viewModel = new AdminDashboardViewModel
        {
            StartDate = start.ToString("dd/MM/yyyy"),
            EndDate = end.ToString("dd/MM/yyyy"),

            TotalRevenue = await _analyticsService.GetTotalRevenueAsync(start, end),
            TotalPatients = await _analyticsService.GetTotalPatientsAsync(start, end),
            TotalAppointments = await _analyticsService.GetTotalAppointmentsAsync(start, end),
            CancellationRate = await _analyticsService.GetCancellationRateAsync(start, end),
            RevenueByMonth = await _analyticsService.GetRevenueTrendAsync(start, end),
            RevenueByDepartment = await _analyticsService.GetRevenueByDepartmentAsync(start, end),
            RevenueByDoctor = await _analyticsService.GetRevenueByDoctorAsync(start, end),
            TodayHourLabels = hourlyToday.Keys.Select(k => $"{k}h").ToList(),
            TodayHourlyRevenue = hourlyToday.Values.ToList(),
            Users = (await _admin.GetUsersAsync()).Where(user => user.Role is "Doctor" or "Staff" or "Admin").ToList(),
            Departments = new List<Department>(await _admin.GetDepartmentsAsync()),
            Rooms = new List<Room>(await _admin.GetRoomsAsync())
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        var users = (await _admin.GetUsersAsync(ct)).Where(user => user.Role is "Doctor" or "Staff" or "Admin").ToList();

        return View(new AdminDashboardViewModel
        {
            Users = users,
            Departments = new List<Department>(await _admin.GetDepartmentsAsync(ct)),
            Rooms = new List<Room>(await _admin.GetRoomsAsync(ct)) 
        });
    }

    public async Task<IActionResult> Users(CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        await PopulateAccountOptions(ct);
        ViewBag.Users = (await _admin.GetUsersAsync(ct)).Where(user => user.Role is "Doctor" or "Staff" or "Admin").ToList();
        return View(new AdminAccountCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(AdminAccountCreateViewModel model, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        if (model.Role is not ("Doctor" or "Staff" or "Admin")) ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ.");
        if (model.Role == "Doctor" && model.DepartmentId == null) ModelState.AddModelError(nameof(model.DepartmentId), "Bác sĩ phải thuộc một khoa.");
        if (!ModelState.IsValid)
        {
            await PopulateAccountOptions(ct); ViewBag.Users = (await _admin.GetUsersAsync(ct)).Where(user => user.Role is "Doctor" or "Staff" or "Admin").ToList(); return View("Users", model);
        }
        try
        {
            await _admin.CreateAccountAsync(model.FullName, model.Email, model.PhoneNumber, model.Password, model.Role, model.DepartmentId, model.DefaultRoomId, model.AcademicTitle, model.PricePerSlot, ct);
            TempData["SuccessMessage"] = "Đã tạo tài khoản.";
        }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Email hoặc số điện thoại đã tồn tại, hoặc thông tin liên kết không hợp lệ."; }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(int id, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        try { await _admin.ToggleAccountAsync(id, HttpContext.Session.GetInt32("UserId")!.Value, ct); TempData["SuccessMessage"] = "Đã cập nhật trạng thái tài khoản."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Departments(int? id, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        var list = await _admin.GetDepartmentsAsync(ct); ViewBag.Departments = list;
        var item = id == null ? null : list.FirstOrDefault(d => d.Id == id);
        return View(item == null ? new AdminDepartmentViewModel() : new AdminDepartmentViewModel { Id = item.Id, Code = item.Code, Name = item.Name, Description = item.Description, BaseConsultationFee = item.BaseConsultationFee });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDepartment(AdminDepartmentViewModel model, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        if (!ModelState.IsValid) { ViewBag.Departments = await _admin.GetDepartmentsAsync(ct); return View("Departments", model); }
        try { await _admin.SaveDepartmentAsync(new Department { Id = model.Id, Code = model.Code, Name = model.Name, Description = model.Description, BaseConsultationFee = model.BaseConsultationFee }, ct); TempData["SuccessMessage"] = "Đã lưu khoa."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Mã khoa đã được sử dụng."; }
        return RedirectToAction(nameof(Departments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDepartment(int id, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        try { await _admin.DeleteDepartmentAsync(id, ct); TempData["SuccessMessage"] = "Đã xóa khoa."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Khoa đang được dữ liệu khác sử dụng nên không thể xóa."; }
        return RedirectToAction(nameof(Departments));
    }


    public async Task<IActionResult> Rooms(int? id, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        var rooms = await _admin.GetRoomsAsync(ct); ViewBag.Rooms = rooms;
        await PopulateDepartmentOptions(ct);
        var room = id == null ? null : rooms.FirstOrDefault(r => r.Id == id);
        return View(room == null ? new AdminRoomViewModel() : new AdminRoomViewModel { Id = room.Id, RoomNumber = room.RoomNumber, DepartmentId = room.DepartmentId, RoomType = room.RoomType, IsOperational = room.IsOperational });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRoom(AdminRoomViewModel model, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        if (!ModelState.IsValid) { ViewBag.Rooms = await _admin.GetRoomsAsync(ct); await PopulateDepartmentOptions(ct); return View("Rooms", model); }
        try { await _admin.SaveRoomAsync(new Room { Id = model.Id, RoomNumber = model.RoomNumber, DepartmentId = model.DepartmentId, RoomType = model.RoomType, IsOperational = model.IsOperational }, ct); TempData["SuccessMessage"] = "Đã lưu phòng."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Số phòng đã tồn tại hoặc khoa không hợp lệ."; }
        return RedirectToAction(nameof(Rooms));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRoom(int id, CancellationToken ct)
    {
        if (!RequireAdmin()) return Denied();
        try { await _admin.DeleteRoomAsync(id, ct); TempData["SuccessMessage"] = "Đã xóa phòng."; }
        catch (InvalidOperationException ex) { TempData["ErrorMessage"] = ex.Message; }
        catch (DbUpdateException) { TempData["ErrorMessage"] = "Phòng đang được dữ liệu khác sử dụng nên không thể xóa."; }
        return RedirectToAction(nameof(Rooms));
    }

    private bool RequireAdmin() => HttpContext.Session.GetString("Role") == "Admin";
    private IActionResult Denied() => string.IsNullOrEmpty(HttpContext.Session.GetString("Role")) ? RedirectToAction("Login", "Auth") : RedirectToAction("AccessDenied", "Auth");
    private async Task PopulateDepartmentOptions(CancellationToken ct) => ViewBag.DepartmentOptions = (await _admin.GetDepartmentsAsync(ct)).Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
    private async Task PopulateAccountOptions(CancellationToken ct)
    {
        var departments = await _admin.GetDepartmentsAsync(ct); var rooms = await _admin.GetRoomsAsync(ct);
        ViewBag.DepartmentOptions = departments.Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToList();
        ViewBag.RoomOptions = rooms.Select(r => new SelectListItem($"{r.RoomNumber} — {r.Department.Name}", r.Id.ToString())).ToList();
    }
}
