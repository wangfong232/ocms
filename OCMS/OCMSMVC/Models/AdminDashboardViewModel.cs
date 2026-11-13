using BusinessObjects.Entities;
using System.Collections.Generic;

namespace OCMSMVC.Models
{
    public class AdminDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public double CancellationRate { get; set; }
        public string StartDate { get; set; }
        public string EndDate { get; set; }
        public Dictionary<string, decimal> RevenueByMonth { get; set; } = new Dictionary<string, decimal>();
        public List<string> TrendLabels { get; set; } = new List<string>();
        public List<decimal> CurrentTrendData { get; set; } = new List<decimal>();
        public List<decimal> CompareTrendData { get; set; } = new List<decimal>();
        public List<string> TodayHourLabels { get; set; } = new List<string>();
        public List<decimal> TodayHourlyRevenue { get; set; } = new List<decimal>();
        public Dictionary<string, decimal> RevenueByDepartment { get; set; } = new Dictionary<string, decimal>();
        public Dictionary<string, decimal> RevenueByDoctor { get; set; } = new Dictionary<string, decimal>();
        public List<User> Users { get; set; } = new List<User>();
        public List<Department> Departments { get; set; } = new List<Department>();
        public List<Room> Rooms { get; set; } = new List<Room>();
    }
}