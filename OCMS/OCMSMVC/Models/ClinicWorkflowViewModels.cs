using System.ComponentModel.DataAnnotations;
using BusinessObjects.Entities;

namespace OCMSMVC.Models;

public sealed class BillingInvoiceViewModel
{
    public required Appointment Appointment { get; init; }
    public required IReadOnlyList<BillingLineViewModel> Lines { get; init; }
    public decimal ConsultationFee { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal PrepaidDeposit { get; init; }
    public decimal RemainingAmount { get; init; }
    public bool IsAlreadySettled { get; init; }
}

public sealed class BillingLineViewModel
{
    public required string Description { get; init; }
    public decimal Amount { get; init; }
    public string Category { get; init; } = string.Empty;
}

public sealed class SettlementRequestViewModel
{
    [Range(1, int.MaxValue)]
    public int AppointmentId { get; init; }

    [Required]
    [RegularExpression("^(Cash|POS)$", ErrorMessage = "Chọn Cash hoặc POS.")]
    public string PaymentMethod { get; init; } = "Cash";
}
