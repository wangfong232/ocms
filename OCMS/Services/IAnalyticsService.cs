using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IAnalyticsService
    {
        Task<decimal> GetTotalRevenueAsync(DateTime startDate, DateTime endDate);
        Task<int> GetTotalPatientsAsync(DateTime startDate, DateTime endDate);
        Task<int> GetTotalAppointmentsAsync(DateTime startDate, DateTime endDate);
        Task<double> GetCancellationRateAsync(DateTime startDate, DateTime endDate);

        Task<Dictionary<string, decimal>> GetRevenueTrendAsync(DateTime startDate, DateTime endDate);

        Task<Dictionary<string, decimal>> GetRevenueByDepartmentAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<int, decimal>> GetTodayHourlyRevenueAsync();
        Task<Dictionary<string, decimal>> GetRevenueByDoctorAsync(DateTime startDate, DateTime endDate);
    }
}