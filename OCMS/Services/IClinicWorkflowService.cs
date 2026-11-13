using BusinessObjects.Entities;

namespace Services;

public interface IClinicWorkflowService
{
    Task<IReadOnlyList<Appointment>> GetBillingQueueAsync(CancellationToken cancellationToken = default);
    Task<Appointment?> GetBillingAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task<Payment> SettleAppointmentAsync(int appointmentId, string paymentMethod, int cashierId, CancellationToken cancellationToken = default);
    Task<Payment?> GetReceiptAsync(int paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Appointment>> GetPharmacyQueueAsync(CancellationToken cancellationToken = default);
    Task<Appointment?> GetPharmacyAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default);
    Task DispenseAppointmentAsync(int appointmentId, int staffId, CancellationToken cancellationToken = default);
}
