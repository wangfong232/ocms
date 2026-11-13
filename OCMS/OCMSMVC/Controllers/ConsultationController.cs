using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class ConsultationController : Controller
    {
        private readonly IConsultationService _consultationService;
        private readonly IDoctorScheduleService _doctorScheduleService;

        public ConsultationController(
            IConsultationService consultationService,
            IDoctorScheduleService doctorScheduleService)
        {
            _consultationService = consultationService;
            _doctorScheduleService = doctorScheduleService;
        }

        private async Task<(bool IsDoctor, int DoctorId, string? FullName)> GetCurrentDoctorAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Doctor")
            {
                return (false, 0, null);
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return (false, 0, null);
            }

            var doctor = await _doctorScheduleService.GetDoctorByUserIdAsync(userId.Value);
            if (doctor == null)
            {
                return (false, 0, null);
            }

            var fullName = HttpContext.Session.GetString("FullName") ?? doctor.User?.FullName;
            return (true, doctor.Id, fullName);
        }

        // 1. Hàng đợi bệnh nhân hôm nay
        [HttpGet]
        public async Task<IActionResult> Queue(string filter = "waiting")
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var appointments = await _consultationService.GetTodayQueueAsync(doctorId);

            int waitingCount = appointments.Count(a => a.Status == "Confirmed" || a.Status == "Arrived");
            int examiningCount = appointments.Count(a => a.Status == "InConsultation");
            int completedCount = appointments.Count(a => a.Status == "Completed");

            // Lọc theo tab
            var filteredAppointments = filter switch
            {
                "examining" => appointments.Where(a => a.Status == "InConsultation"),
                "completed" => appointments.Where(a => a.Status == "Completed"),
                "all" => appointments,
                _ => appointments.Where(a => a.Status == "Confirmed" || a.Status == "Arrived")
            };

            var viewModelList = filteredAppointments.Select(a => new ConsultationQueueItemViewModel
            {
                AppointmentId = a.Id,
                QueueNumber = a.QueueNumber,
                PatientName = a.Patient?.User?.FullName ?? "N/A",
                Gender = a.Patient?.Gender == "Male" ? "Nam" : (a.Patient?.Gender == "Female" ? "Nữ" : "Khác"),
                BirthYear = a.Patient?.DateOfBirth.Year ?? 0,
                PhoneNumber = a.Patient?.User?.PhoneNumber ?? "N/A",
                HealthInsuranceNumber = a.Patient?.HealthInsuranceNumber,
                TimeSlotDisplay = (a.TimeSlot != null) 
                    ? $"{a.TimeSlot.StartTime:hh\\:mm} - {a.TimeSlot.EndTime:hh\\:mm}" 
                    : "Chưa xếp",
                Reason = a.Reason,
                Status = a.Status,
                HasVitalSigns = a.VitalSigns != null,
                HasMedicalRecord = a.MedicalRecord != null
            }).ToList();

            ViewBag.CurrentFilter = filter;
            ViewBag.WaitingCount = waitingCount;
            ViewBag.ExaminingCount = examiningCount;
            ViewBag.CompletedCount = completedCount;
            ViewBag.TotalCount = appointments.Count;

            return View(viewModelList);
        }

        // 2. Màn hình Bàn khám bệnh (Consultation Workspace)
        [HttpGet]
        public async Task<IActionResult> Examine(int id)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            // Tự động chuyển trạng thái sang "InConsultation" nếu mới bắt đầu vào khám
            await _consultationService.StartConsultationAsync(id, doctorId);

            var appt = await _consultationService.GetConsultationDetailAsync(id, doctorId);
            if (appt == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin ca khám hoặc bạn không phụ trách ca này.";
                return RedirectToAction(nameof(Queue));
            }

            var pastRecords = await _consultationService.GetPatientPastMedicalHistoryAsync(appt.PatientId, id);
            var services = await _consultationService.GetActiveServicesAsync();

            var viewModel = new ConsultationExamineViewModel
            {
                AppointmentId = appt.Id,
                DoctorId = doctorId,
                DoctorName = appt.Doctor?.User?.FullName ?? "Bác sĩ",
                DepartmentName = appt.Doctor?.Department?.Name ?? "Khoa",
                AppointmentDate = appt.AppointmentDate,
                TimeSlotDisplay = (appt.TimeSlot != null) 
                    ? $"{appt.TimeSlot.StartTime:hh\\:mm} - {appt.TimeSlot.EndTime:hh\\:mm}" 
                    : "",
                Status = appt.Status,
                ChiefComplaint = appt.Reason,

                // Bệnh nhân
                PatientId = appt.PatientId,
                PatientName = appt.Patient?.User?.FullName ?? "N/A",
                Gender = appt.Patient?.Gender == "Male" ? "Nam" : (appt.Patient?.Gender == "Female" ? "Nữ" : "Khác"),
                DateOfBirth = appt.Patient?.DateOfBirth ?? DateTime.MinValue,
                Address = appt.Patient?.Address,
                HealthInsuranceNumber = appt.Patient?.HealthInsuranceNumber,
                PhoneNumber = appt.Patient?.User?.PhoneNumber ?? "N/A",

                // Chỉ số sinh tồn
                HasVitalSigns = appt.VitalSigns != null,
                SystolicBP = appt.VitalSigns?.SystolicBP ?? 0,
                DiastolicBP = appt.VitalSigns?.DiastolicBP ?? 0,
                Pulse = appt.VitalSigns?.Pulse ?? 0,
                Temperature = appt.VitalSigns?.Temperature ?? 0,
                SpO2 = appt.VitalSigns?.SpO2 ?? 0,
                Weight = appt.VitalSigns?.Weight ?? 0,
                Height = appt.VitalSigns?.Height ?? 0,

                // Bệnh án EMR
                MedicalRecordId = appt.MedicalRecord?.Id ?? 0,
                Diagnosis = appt.MedicalRecord?.Diagnosis ?? string.Empty,
                ICD10Code = appt.MedicalRecord?.ICD10Code,
                DoctorNotes = appt.MedicalRecord?.DoctorNotes,
                FollowUpDate = appt.MedicalRecord?.FollowUpDate,

                // Tiền sử
                PastHistory = pastRecords.Select(pr => new PastMedicalHistoryItemViewModel
                {
                    Date = pr.Appointment?.AppointmentDate ?? pr.CreatedAt,
                    DoctorName = pr.Appointment?.Doctor?.User?.FullName ?? "Bác sĩ",
                    Diagnosis = pr.Diagnosis,
                    ICD10Code = pr.ICD10Code,
                    DoctorNotes = pr.DoctorNotes
                }).ToList(),

                // Chỉ định xét nghiệm
                ServiceOrders = appt.ServiceOrders.Select(so => new ServiceOrderItemViewModel
                {
                    ServiceOrderId = so.Id,
                    ServiceId = so.ServiceId,
                    ServiceName = so.MedicalService?.Name ?? "Dịch vụ",
                    UnitPrice = so.UnitPrice,
                    ResultSummary = so.ResultSummary,
                    Status = so.Status,
                    CreatedAt = so.CreatedAt
                }).OrderByDescending(so => so.ServiceOrderId).ToList(),

                // Danh mục xét nghiệm có sẵn
                AvailableServices = services.Select(s => new MedicalServiceOptionViewModel
                {
                    ServiceId = s.Id,
                    Name = s.Name,
                    Price = s.Price,
                    DepartmentName = s.Department?.Name
                }).ToList()
            };

            return View(viewModel);
        }

        // 3. Ghi nhận bệnh án EMR (Lưu tạm hoặc Hoàn tất khám)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEMR(
            int appointmentId, 
            string diagnosis, 
            string? icd10Code, 
            string? doctorNotes, 
            DateTime? followUpDate, 
            bool completeConsultation = false)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (string.IsNullOrWhiteSpace(diagnosis))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập chẩn đoán bệnh trước khi lưu.";
                return RedirectToAction(nameof(Examine), new { id = appointmentId });
            }

            var result = await _consultationService.SaveEMRAsync(
                appointmentId, 
                doctorId, 
                diagnosis, 
                icd10Code, 
                doctorNotes, 
                followUpDate, 
                completeConsultation);

            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
                if (completeConsultation)
                {
                    return RedirectToAction(nameof(Queue));
                }
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Examine), new { id = appointmentId });
        }

        // 4. Chỉ định dịch vụ cận lâm sàng / xét nghiệm nhanh
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OrderService(int appointmentId, int serviceId)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var result = await _consultationService.OrderServiceAsync(appointmentId, doctorId, serviceId);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Examine), new { id = appointmentId });
        }

        // 5. Cập nhật kết quả xét nghiệm nhanh
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateServiceResult(
            int appointmentId, 
            int serviceOrderId, 
            string? resultSummary, 
            bool markCompleted = false)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var result = await _consultationService.UpdateServiceResultAsync(serviceOrderId, doctorId, resultSummary, markCompleted);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Examine), new { id = appointmentId });
        }

        // 6. Hủy chỉ định xét nghiệm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteService(int appointmentId, int serviceOrderId)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var result = await _consultationService.DeleteServiceOrderAsync(serviceOrderId, doctorId);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Examine), new { id = appointmentId });
        }
    }
}

