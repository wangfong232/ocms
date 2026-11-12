using System.ComponentModel.DataAnnotations;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OCMSMVC.Models;

public class AdminDashboardViewModel
{
    public IReadOnlyList<User> Users { get; init; } = [];
    public IReadOnlyList<Department> Departments { get; init; } = [];
    public IReadOnlyList<Room> Rooms { get; init; } = [];
}

public class AdminAccountCreateViewModel
{
    [Required, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, Phone, StringLength(20)] public string PhoneNumber { get; set; } = "";
    [Required, MinLength(8)] public string Password { get; set; } = "";
    [Required] public string Role { get; set; } = "Staff";
    public int? DepartmentId { get; set; }
    public int? DefaultRoomId { get; set; }
    [StringLength(50)] public string? AcademicTitle { get; set; }
    [Range(0, 100000000)] public decimal? PricePerSlot { get; set; }
    public IReadOnlyList<SelectListItem> Departments { get; set; } = [];
    public IReadOnlyList<SelectListItem> Rooms { get; set; } = [];
}

public class AdminDepartmentViewModel
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string Code { get; set; } = "";
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(500)] public string? Description { get; set; }
    [Range(0, 100000000)] public decimal BaseConsultationFee { get; set; } = 150000;
}

public class AdminRoomViewModel
{
    public int Id { get; set; }
    [Required, StringLength(20)] public string RoomNumber { get; set; } = "";
    [Required] public int DepartmentId { get; set; }
    [Required, StringLength(50)] public string RoomType { get; set; } = "Consultation";
    public bool IsOperational { get; set; } = true;
    public IReadOnlyList<SelectListItem> Departments { get; set; } = [];
}
