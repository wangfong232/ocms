using System.Collections.Generic;

namespace OCMSMVC.Models
{
    public class AdminDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public double CancellationRate { get; set; }
        public required Dictionary<string, decimal> RevenueByMonth { get; set; }
        public required Dictionary<string, int> AppointmentsByDepartment { get; set; }
    }
}