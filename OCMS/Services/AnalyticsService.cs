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

        public async Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate)
        {
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);
            return await _dbContext.Payments
                .Include(p => p.Appointment)
                .Where(p => p.PaymentStatus == "Success"
                         && (p.Appointment.Status == "Completed" || p.Appointment.Status == "Confirmed")
                         && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc)
                .SumAsync(p => p.Amount);
        }

        public async Task<int> GetTotalPatientsAsync(DateTime startDate, DateTime endDate)
        {
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);
            return await _dbContext.Appointments
                .Where(a => a.CreatedAt >= startUtc && a.CreatedAt <= endUtc)
                .Select(a => a.PatientId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetTotalAppointmentsAsync(DateTime startDate, DateTime endDate)
        {
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);
            return await _dbContext.Appointments
                .Where(a => a.CreatedAt >= startUtc && a.CreatedAt <= endUtc)
                .CountAsync();
        }

        public async Task<double> GetCancellationRateAsync(DateTime startDate, DateTime endDate)
        {
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);
            var baseQuery = _dbContext.Appointments
                .Where(a => a.CreatedAt >= startUtc && a.CreatedAt <= endUtc);

            var allAppointments = await baseQuery.CountAsync();
            if (allAppointments == 0) return 0;

            var cancelled = await baseQuery.Where(a => a.Status == "Cancelled").CountAsync();

            return Math.Round((double)cancelled / allAppointments * 100, 2);
        }

        public async Task<Dictionary<string, decimal>> GetRevenueTrendAsync(DateTime startDate, DateTime endDate)
        {
            var dict = new Dictionary<string, decimal>();
            var totalDays = (endDate - startDate).TotalDays;
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);

            var payments = await _dbContext.Payments
                .Include(p => p.Appointment)
                .Where(p => p.PaymentStatus == "Success"
                         && (p.Appointment.Status == "Completed" || p.Appointment.Status == "Confirmed")
                         && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc)
                .Select(p => new { p.CreatedAt, p.Amount })
                .ToListAsync();

            if (totalDays <= 31)
            {
                for (var d = startDate.Date; d <= endDate.Date; d = d.AddDays(1))
                {
                    dict.Add(d.ToString("dd/MM"), 0);
                }

                var dailyData = payments
                    .GroupBy(p => p.CreatedAt.AddHours(7).Date)
                    .Select(g => new { Date = g.Key, Total = g.Sum(p => p.Amount) })
                    .ToList();

                foreach (var data in dailyData)
                {
                    var label = data.Date.ToString("dd/MM");
                    if (dict.ContainsKey(label))
                    {
                        dict[label] = data.Total;
                    }
                }
            }
            else
            {
                var currentMonth = new DateTime(startDate.Year, startDate.Month, 1);
                var endMonth = new DateTime(endDate.Year, endDate.Month, 1);

                while (currentMonth <= endMonth)
                {
                    dict.Add(currentMonth.ToString("MM/yyyy"), 0);
                    currentMonth = currentMonth.AddMonths(1);
                }

                var monthlyData = payments
                    .GroupBy(p => new { p.CreatedAt.AddHours(7).Year, p.CreatedAt.AddHours(7).Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Total = g.Sum(p => p.Amount)
                    })
                    .ToList();

                foreach (var data in monthlyData)
                {
                    var label = $"{data.Month:D2}/{data.Year}";
                    if (dict.ContainsKey(label))
                    {
                        dict[label] = data.Total;
                    }
                }
            }

            return dict;
        }

        public async Task<Dictionary<int, decimal>> GetTodayHourlyRevenueAsync()
        {
            var todayLocal = DateTime.Today;
            var startUtc = todayLocal.AddHours(-7);
            var endUtc = todayLocal.AddDays(1).AddHours(-7);

            var dict = new Dictionary<int, decimal>();
            for (int i = 0; i < 24; i++) dict.Add(i, 0);

            var hourlyPayments = await _dbContext.Payments
                .Include(p => p.Appointment)
                .Where(p => p.PaymentStatus == "Success"
                         && (p.Appointment.Status == "Completed" || p.Appointment.Status == "Confirmed")
                         && p.CreatedAt >= startUtc && p.CreatedAt < endUtc)
                .Select(p => new { p.CreatedAt, p.Amount })
                .ToListAsync();

            var hourlyData = hourlyPayments
                .GroupBy(p => p.CreatedAt.AddHours(7).Hour)
                .Select(g => new { Hour = g.Key, Total = g.Sum(p => p.Amount) })
                .ToList();

            foreach (var item in hourlyData)
            {
                dict[item.Hour] = item.Total;
            }
            return dict;
        }

        public async Task<Dictionary<string, decimal>> GetRevenueByDepartmentAsync(DateTime startDate, DateTime endDate)
        {
            var dict = new Dictionary<string, decimal>();
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);

            var deptStats = await _dbContext.Departments
                .Select(d => new
                {
                    DepartmentName = d.Name,
                    TotalRevenue = d.Doctors
                        .SelectMany(doc => doc.Appointments)
                        .Where(a => a.Status == "Completed" || a.Status == "Confirmed")
                        .SelectMany(a => a.Payments)
                        .Where(p => p.PaymentStatus == "Success" && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc)
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync();

            foreach (var stat in deptStats)
            {
                if (!string.IsNullOrEmpty(stat.DepartmentName))
                {
                    dict.Add(stat.DepartmentName, stat.TotalRevenue);
                }
            }

            return dict;
        }

        public async Task<Dictionary<string, decimal>> GetRevenueByDoctorAsync(DateTime startDate, DateTime endDate)
        {
            var dict = new Dictionary<string, decimal>();
            var startUtc = startDate.AddHours(-7);
            var endUtc = endDate.AddHours(-7);

            var docStats = await _dbContext.Doctors
                .Include(d => d.User)
                .Select(d => new
                {
                    DoctorName = d.User.FullName,
                    TotalRevenue = d.Appointments
                        .Where(a => a.Status == "Completed" || a.Status == "Confirmed")
                        .SelectMany(a => a.Payments)
                        .Where(p => p.PaymentStatus == "Success" && p.CreatedAt >= startUtc && p.CreatedAt <= endUtc)
                        .Sum(p => (decimal?)p.Amount) ?? 0m
                })
                .ToListAsync();

            foreach (var stat in docStats)
            {
                var name = string.IsNullOrEmpty(stat.DoctorName) ? "Khác" : stat.DoctorName;

                if (!dict.ContainsKey(name))
                {
                    dict.Add(name, stat.TotalRevenue);
                }
                else
                {
                    dict[name] += stat.TotalRevenue;
                }
            }

            return dict;
        }
    }
}