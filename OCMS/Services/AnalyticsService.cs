using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Services
{
    public class AnalyticsService : IAnalyticsService
    {
        public readonly OcmsDbContext _dbContext;
        public AnalyticsService(OcmsDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<decimal> GetTotalRevenueAsync()
        {
            return await _dbContext.Payments.Where(p => p.PaymentStatus == "Success").SumAsync(p => p.Amount);
        }

        public async Task<int> GetTotalPatientsAsync()
        {
            return await _dbContext.Patients.CountAsync();
        }

        public async Task<int> GetTotalAppointmentsAsync()
        {
            return await _dbContext.Appointments.CountAsync();
        }

        public async Task<double> GetCancellationRateAsync()
        {
            var allAppointments = await _dbContext.Appointments.CountAsync();
            if (allAppointments == 0)
            {
                return 0;
            }

            var cancelled = await _dbContext.Appointments.Where(a => a.Status == "Cancelled").CountAsync();
            return Math.Round((double)cancelled / allAppointments * 100, 2);
        }

        public async Task<Dictionary<string, decimal>> GetRevenueByMonthAsync()
        {
            var currentYear = DateTime.UtcNow.Year;
            var dict = new Dictionary<string, decimal>();

            var monthlyData = await _dbContext.Payments
                .Where(p => p.PaymentStatus == "Success" && p.CreatedAt.Year == currentYear)
                .GroupBy(p => p.CreatedAt.Month)
                .Select(g => new { Month = g.Key, Total = g.Sum(p => p.Amount) })
                .ToListAsync();

            for (int i = 1; i <= 12; i++)
            {
                var data = monthlyData.FirstOrDefault(m => m.Month == i);
                dict.Add($"Tháng {i}", data != null ? data.Total : 0);
            }
            return dict;
        }

        public async Task<Dictionary<string, int>> GetAppointmentsByDepartmentAsync()
        {
            var dict = new Dictionary<string, int>();
            var deptStats = await _dbContext.Appointments
                .Include(a => a.Doctor)
                    .ThenInclude(d => d.Department)
                .Where(a => a.Status == "Completed" || a.Status == "Confirmed")
                .GroupBy(a => a.Doctor.Department.Name)
                .Select(g => new { Department = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var stat in deptStats)
            {
                if (!string.IsNullOrEmpty(stat.Department))
                {
                    dict.Add(stat.Department, stat.Count);
                }
            }
            return dict;
        }
    }
}
