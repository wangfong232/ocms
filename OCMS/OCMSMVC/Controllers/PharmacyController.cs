using Microsoft.AspNetCore.Mvc;
using Services;

namespace OCMSMVC.Controllers;

public class PharmacyController : Controller
{
    private readonly IClinicWorkflowService _workflow;

    public PharmacyController(IClinicWorkflowService workflow) => _workflow = workflow;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var appointments = await _workflow.GetPharmacyQueueAsync(cancellationToken);
        return View(appointments);
    }

    [HttpGet]
    public async Task<IActionResult> Order(int id, CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var appointment = await _workflow.GetPharmacyAppointmentAsync(id, cancellationToken);
        if (appointment == null) return NotFound();
        var lines = appointment.MedicalRecord?.PrescriptionDetails.ToList() ?? new();
        if (!lines.Any(line => line.Status is "Paid" or "Dispensed"))
        {
            TempData["ErrorMessage"] = "Đơn thuốc chưa được thanh toán.";
            return RedirectToAction(nameof(Index));
        }
        return View(appointment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(int id, CancellationToken cancellationToken)
    {
        if (!RequireStaff()) return RedirectToAction("Login", "Auth");
        var staffId = HttpContext.Session.GetInt32("UserId");
        if (staffId == null) return RedirectToAction("Login", "Auth");

        try
        {
            await _workflow.DispenseAppointmentAsync(id, staffId.Value, cancellationToken);
            TempData["SuccessMessage"] = $"Đã phát thuốc cho lịch hẹn #{id} và cập nhật tồn kho.";
        }
        catch (InvalidOperationException exception)
        {
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Order), new { id });
        }

        return RedirectToAction(nameof(Index));
    }

    private bool RequireStaff()
    {
        var role = HttpContext.Session.GetString("Role");
        return role is "Staff" or "Admin";
    }
}
