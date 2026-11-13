using BusinessObjects.Entities;
using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;

namespace OCMSMVC.Controllers;

public class BillingController : Controller
{
    private readonly IClinicWorkflowService _workflow;

    public BillingController(IClinicWorkflowService workflow) => _workflow = workflow;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var appointments = await _workflow.GetBillingQueueAsync(cancellationToken);
        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(int id, CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var appointment = await _workflow.GetBillingAppointmentAsync(id, cancellationToken);
        if (appointment == null) return NotFound();
        if (appointment.Status != "Completed")
        {
            TempData["ErrorMessage"] = "Chỉ quyết toán lượt khám đã hoàn tất.";
            return RedirectToAction(nameof(Index));
        }

        return View(BuildInvoice(appointment));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settle(SettlementRequestViewModel request, CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu quyết toán không hợp lệ.";
            return RedirectToAction(nameof(Invoice), new { id = request.AppointmentId });
        }

        var cashierId = HttpContext.Session.GetInt32("UserId");
        if (cashierId == null) return RedirectToAction("Login", "Auth");

        try
        {
            var payment = await _workflow.SettleAppointmentAsync(request.AppointmentId, request.PaymentMethod, cashierId.Value, cancellationToken);
            return RedirectToAction(nameof(Receipt), new { id = payment.Id });
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Invoice), new { id = request.AppointmentId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int id, CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var payment = await _workflow.GetReceiptAsync(id, cancellationToken);
        return payment == null ? NotFound() : View(payment);
    }

    private bool RequireStaff()
    {
        var role = HttpContext.Session.GetString("Role");
        return role is "Staff" or "Admin";
    }

    private static BillingInvoiceViewModel BuildInvoice(Appointment appointment)
    {
        var consultationFee = appointment.ConsultationFee ?? appointment.Doctor.PricePerSlot;
        var lines = new List<BillingLineViewModel>
        {
            new() { Description = $"Phí khám - {appointment.Doctor.AcademicTitle} {appointment.Doctor.User.FullName}", Amount = consultationFee, Category = "Khám" }
        };

        lines.AddRange(appointment.ServiceOrders
            .Where(service => service.Status == "Completed")
            .Select(service => new BillingLineViewModel
            {
                Description = service.MedicalService.Name,
                Amount = service.UnitPrice,
                Category = "Xét nghiệm / dịch vụ"
            }));

        if (appointment.MedicalRecord != null)
        {
            lines.AddRange(appointment.MedicalRecord.PrescriptionDetails
                .Where(detail => detail.Status == "Pending")
                .Select(detail => new BillingLineViewModel
                {
                    Description = $"{detail.Medicine.Name} × {detail.Quantity}",
                    Amount = detail.UnitPrice * detail.Quantity,
                    Category = "Thuốc"
                }));
        }

        var total = lines.Sum(line => line.Amount);
        var deposit = appointment.Payments
            .Where(payment => payment.PaymentType == "Deposit" && payment.PaymentStatus == "Success")
            .Sum(payment => payment.Amount);
        var remaining = Math.Max(0m, total - deposit);
        var isAlreadySettled = appointment.Payments.Any(payment => payment.PaymentType == "FinalSettlement" && payment.PaymentStatus == "Success");

        return new BillingInvoiceViewModel
        {
            Appointment = appointment,
            Lines = lines,
            ConsultationFee = consultationFee,
            TotalAmount = total,
            PrepaidDeposit = deposit,
            RemainingAmount = remaining,
            IsAlreadySettled = isAlreadySettled
        };
    }
}
