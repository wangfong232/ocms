using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public interface IAnalyticsService
    {

        Task<decimal> GetTotalRevenueAsync();
        Task<int> GetTotalPatientsAsync();
        Task<int> GetTotalAppointmentsAsync();
        Task<double> GetCancellationRateAsync();
        Task<Dictionary<string, decimal>> GetRevenueByMonthAsync();
        Task<Dictionary<string, int>> GetAppointmentsByDepartmentAsync();
    }
}
