using System.Collections.Generic;
using BusinessObjects.Entities;

namespace OCMSMVC.Models
{
    public class AdminDashboardViewModel
    {
        // Quản lý tài khoản và danh mục phòng khám
        public IReadOnlyList<User> Users { get; init; } = [];
        public IReadOnlyList<Department> Departments { get; init; } = [];
        public IReadOnlyList<Room> Rooms { get; init; } = [];

        // Chỉ số phân tích & báo cáo tài chính
        public decimal TotalRevenue { get; set; }
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public double CancellationRate { get; set; }
        public Dictionary<string, decimal> RevenueByMonth { get; set; } = new();
        public Dictionary<string, int> AppointmentsByDepartment { get; set; } = new();
    }
}
