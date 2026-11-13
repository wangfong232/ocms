using System.Data;
using BusinessObjects.Entities;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;

namespace Repositories;

public sealed class ClinicWorkflowRepository : IClinicWorkflowRepository
{
    private const string CompletedAppointment = "Completed";
    private readonly OcmsDbContext _db;

    public ClinicWorkflowRepository(OcmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Appointment>> GetBillingQueueAsync(CancellationToken cancellationToken = default)
    {
        return await BillingAppointmentQuery()
            .Where(a => a.Status == CompletedAppointment &&
                        !_db.Payments.Any(p => p.AppointmentId == a.Id &&
                                               p.PaymentType == "FinalSettlement" &&
                                               p.PaymentStatus == "Success"))
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Appointment?> GetBillingAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default) =>
        BillingAppointmentQuery().SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

    public async Task<Payment> SettleAppointmentAsync(int appointmentId, string paymentMethod, int cashierId, CancellationToken cancellationToken = default)
    {
        if (paymentMethod is not ("Cash" or "POS"))
        {
            throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var appointment = await BillingAppointmentQuery(tracking: true)
            .SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lịch hẹn cần quyết toán.");

        if (appointment.Status != CompletedAppointment)
        {
            throw new InvalidOperationException("Chỉ quyết toán lượt khám đã hoàn tất.");
        }

        var alreadySettled = await _db.Payments.AnyAsync(p => p.AppointmentId == appointmentId &&
            p.PaymentType == "FinalSettlement" && p.PaymentStatus == "Success", cancellationToken);
        if (alreadySettled)
        {
            throw new InvalidOperationException("Lượt khám này đã được quyết toán.");
        }

        var consultationFee = appointment.ConsultationFee ?? appointment.Doctor.PricePerSlot;
        var completedServices = appointment.ServiceOrders.Where(s => s.Status == "Completed").ToList();
        var unpaidPrescriptionLines = appointment.MedicalRecord?.PrescriptionDetails
            .Where(p => p.Status == "Pending")
            .ToList() ?? new List<PrescriptionDetail>();
        var deposit = appointment.Payments
            .Where(p => p.PaymentType == "Deposit" && p.PaymentStatus == "Success")
            .Sum(p => p.Amount);
        var total = consultationFee + completedServices.Sum(s => s.UnitPrice) +
                    unpaidPrescriptionLines.Sum(p => p.Quantity * p.UnitPrice);
        var remaining = Math.Max(0m, total - deposit);

        var payment = new Payment
        {
            AppointmentId = appointmentId,
            Amount = remaining,
            PaymentMethod = paymentMethod,
            PaymentStatus = "Success",
            PaymentType = "FinalSettlement",
            CashierId = cashierId,
            CreatedAt = DateTime.UtcNow
        };
        _db.Payments.Add(payment);

        foreach (var line in unpaidPrescriptionLines)
        {
            line.Status = "Paid";
        }
        foreach (var serviceOrder in completedServices)
        {
            serviceOrder.Status = "Paid";
        }

        await _db.SaveChangesAsync(cancellationToken);
        payment.TransactionId = $"RCPT-{payment.Id:D8}";
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return payment;
    }

    public Task<Payment?> GetReceiptAsync(int paymentId, CancellationToken cancellationToken = default) =>
        _db.Payments
            .AsNoTracking()
            .Include(p => p.Cashier)
            .Include(p => p.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(p => p.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
            .Include(p => p.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(p => p.Appointment).ThenInclude(a => a.MedicalRecord).ThenInclude(m => m!.PrescriptionDetails).ThenInclude(d => d.Medicine)
            .Include(p => p.Appointment).ThenInclude(a => a.ServiceOrders).ThenInclude(s => s.MedicalService)
            .SingleOrDefaultAsync(p => p.Id == paymentId && p.PaymentType == "FinalSettlement", cancellationToken);

    public async Task<IReadOnlyList<Appointment>> GetPharmacyQueueAsync(CancellationToken cancellationToken = default)
    {
        return await PharmacyAppointmentQuery()
            .Where(a => a.MedicalRecord!.PrescriptionDetails.Any(p => p.Status == "Paid"))
            .OrderBy(a => a.AppointmentDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Appointment?> GetPharmacyAppointmentAsync(int appointmentId, CancellationToken cancellationToken = default) =>
        PharmacyAppointmentQuery().SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);

    public async Task DispenseAppointmentAsync(int appointmentId, int staffId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var appointment = await _db.Appointments
            .Include(a => a.MedicalRecord).ThenInclude(m => m!.PrescriptionDetails)
            .ThenInclude(p => p.Medicine)
            .SingleOrDefaultAsync(a => a.Id == appointmentId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lượt khám.");

        if (appointment.Status != CompletedAppointment)
        {
            throw new InvalidOperationException("Chỉ phát thuốc cho lượt khám đã hoàn tất.");
        }

        var paidLines = appointment.MedicalRecord?.PrescriptionDetails.Where(p => p.Status == "Paid").ToList()
                        ?? new List<PrescriptionDetail>();
        if (paidLines.Count == 0)
        {
            throw new InvalidOperationException("Không có thuốc đã thanh toán đang chờ phát.");
        }

        foreach (var group in paidLines.GroupBy(p => p.MedicineId))
        {
            var requiredQuantity = group.Sum(p => p.Quantity);
            var updated = await _db.Medicines
                .Where(m => m.Id == group.Key && m.IsAvailable && m.StockQuantity >= requiredQuantity)
                .ExecuteUpdateAsync(update => update.SetProperty(m => m.StockQuantity, m => m.StockQuantity - requiredQuantity), cancellationToken);
            if (updated != 1)
            {
                var medicineName = group.First().Medicine?.Name ?? "một loại thuốc";
                throw new InvalidOperationException($"Không đủ tồn kho {medicineName}. Hãy liên hệ bác sĩ để điều chỉnh đơn thuốc.");
            }
        }

        var dispensedAt = DateTime.UtcNow;
        foreach (var line in paidLines)
        {
            line.Status = "Dispensed";
            line.DispensedAt = dispensedAt;
            line.DispensedByUserId = staffId;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = staffId,
            NotificationType = "System",
            Message = $"Đã phát thuốc cho lịch hẹn #{appointmentId}.",
            CreatedAt = dispensedAt
        });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private IQueryable<Appointment> BillingAppointmentQuery(bool tracking = false)
    {
        IQueryable<Appointment> query = _db.Appointments
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.Doctor).ThenInclude(d => d.User)
            .Include(a => a.Doctor).ThenInclude(d => d.Department)
            .Include(a => a.Payments)
            .Include(a => a.ServiceOrders).ThenInclude(s => s.MedicalService)
            .Include(a => a.MedicalRecord).ThenInclude(m => m!.PrescriptionDetails).ThenInclude(p => p.Medicine);
        return tracking ? query : query.AsNoTracking();
    }

    private IQueryable<Appointment> PharmacyAppointmentQuery() => _db.Appointments
        .AsNoTracking()
        .Include(a => a.Patient).ThenInclude(p => p.User)
        .Include(a => a.Doctor).ThenInclude(d => d.User)
        .Include(a => a.MedicalRecord).ThenInclude(m => m!.PrescriptionDetails).ThenInclude(p => p.Medicine);
}
