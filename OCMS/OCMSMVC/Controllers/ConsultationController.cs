using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Filters;
using OCMSMVC.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    [RoleAuthorize("Doctor")]
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

        // 1. Hàng đợi bệnh nhân hôm nay (Chỉ hiển thị bệnh nhân đã được Staff confirm và cấp số)
        [HttpGet]
        public async Task<IActionResult> Queue(string filter = "waiting")
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var appointments = await _consultationService.GetTodayQueueAsync(doctorId);

            // Bệnh nhân đang được khám dở tại phòng
            var activeApt = appointments.FirstOrDefault(a => a.Status == "InConsultation");

            // Bệnh nhân chờ tiếp theo theo STT (QueueNumber nhỏ nhất)
            var nextWaitingApt = appointments
                .Where(a => a.Status == "Arrived")
                .OrderBy(a => a.QueueNumber ?? 999)
                .FirstOrDefault();

            int waitingCount = appointments.Count(a => a.Status == "Arrived");
            int examiningCount = appointments.Count(a => a.Status == "InConsultation");
            int completedCount = appointments.Count(a => a.Status == "Completed");

            // Lọc theo tab
            var filteredAppointments = filter switch
            {
                "examining" => appointments.Where(a => a.Status == "InConsultation"),
                "completed" => appointments.Where(a => a.Status == "Completed"),
                "all" => appointments,
                _ => appointments.Where(a => a.Status == "Arrived")
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
                HasMedicalRecord = a.MedicalRecord != null,
                IsNextInLine = (nextWaitingApt != null && a.Id == nextWaitingApt.Id)
            }).ToList();

            ViewBag.CurrentFilter = filter;
            ViewBag.WaitingCount = waitingCount;
            ViewBag.ExaminingCount = examiningCount;
            ViewBag.CompletedCount = completedCount;
            ViewBag.TotalCount = appointments.Count;
            ViewBag.HasActiveConsultation = (activeApt != null);
            ViewBag.ActiveConsultationId = activeApt?.Id;
            ViewBag.ActivePatientName = activeApt?.Patient?.User?.FullName;
            ViewBag.ActiveQueueNumber = activeApt?.QueueNumber;

            return View(viewModelList);
        }

        // 1b. Xem trước hồ sơ bệnh án & tiền sử (Read-only Modal)
        [HttpGet]
        public async Task<IActionResult> GetPatientPreview(int id)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return Unauthorized();
            }

            var appt = await _consultationService.GetConsultationDetailAsync(id, doctorId);
            if (appt == null)
            {
                return NotFound();
            }

            var pastRecords = await _consultationService.GetPatientPastMedicalHistoryAsync(appt.PatientId, id);
            var todayQueue = await _consultationService.GetTodayQueueAsync(doctorId);
            var activeConsultation = todayQueue.FirstOrDefault(a => a.Status == "InConsultation" && a.Id != id);

            bool canCallNow = (activeConsultation == null) && (appt.Status == "Arrived");
            string? blockReason = null;
            if (activeConsultation != null && appt.Status != "InConsultation")
            {
                var curName = activeConsultation.Patient?.User?.FullName ?? "bệnh nhân khác";
                var curNum = activeConsultation.QueueNumber.HasValue ? $"#{activeConsultation.QueueNumber}" : "";
                blockReason = $"Bác sĩ đang khám cho {curName} ({curNum})";
            }

            var previewModel = new PatientPreviewViewModel
            {
                AppointmentId = appt.Id,
                QueueNumber = appt.QueueNumber,
                TimeSlotDisplay = (appt.TimeSlot != null) 
                    ? $"{appt.TimeSlot.StartTime:hh\\:mm} - {appt.TimeSlot.EndTime:hh\\:mm}" 
                    : "",
                Status = appt.Status,
                Reason = appt.Reason,
                PatientId = appt.PatientId,
                PatientName = appt.Patient?.User?.FullName ?? "N/A",
                Gender = appt.Patient?.Gender == "Male" ? "Nam" : (appt.Patient?.Gender == "Female" ? "Nữ" : "Khác"),
                DateOfBirth = appt.Patient?.DateOfBirth ?? DateTime.MinValue,
                Address = appt.Patient?.Address,
                HealthInsuranceNumber = appt.Patient?.HealthInsuranceNumber,
                PhoneNumber = appt.Patient?.User?.PhoneNumber ?? "N/A",

                HasVitalSigns = appt.VitalSigns != null,
                SystolicBP = appt.VitalSigns?.SystolicBP ?? 0,
                DiastolicBP = appt.VitalSigns?.DiastolicBP ?? 0,
                Pulse = appt.VitalSigns?.Pulse ?? 0,
                Temperature = appt.VitalSigns?.Temperature ?? 0,
                SpO2 = appt.VitalSigns?.SpO2 ?? 0,
                Weight = appt.VitalSigns?.Weight ?? 0,
                Height = appt.VitalSigns?.Height ?? 0,

                PastHistory = pastRecords.Select(pr => new PastMedicalHistoryItemViewModel
                {
                    Date = pr.Appointment?.AppointmentDate ?? pr.CreatedAt,
                    DoctorName = pr.Appointment?.Doctor?.User?.FullName ?? "Bác sĩ",
                    Diagnosis = pr.Diagnosis,
                    ICD10Code = pr.ICD10Code,
                    DoctorNotes = pr.DoctorNotes
                }).ToList(),

                CanCallNow = canCallNow,
                BlockReason = blockReason
            };

            return PartialView("_PatientPreviewModal", previewModel);
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

            // Tự động chuyển trạng thái sang "InConsultation" nếu mới bắt đầu vào khám (có kiểm tra ca khám trùng lặp)
            var startResult = await _consultationService.StartConsultationAsync(id, doctorId);
            if (!startResult.Success)
            {
                TempData["ErrorMessage"] = startResult.Message;
                return RedirectToAction(nameof(Queue));
            }

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
                }).ToList(),

                // Đơn thuốc điều trị
                Prescriptions = appt.MedicalRecord?.PrescriptionDetails.Select(pd => new PrescriptionItemViewModel
                {
                    PrescriptionDetailId = pd.Id,
                    MedicineId = pd.MedicineId,
                    MedicineName = pd.Medicine?.Name ?? "Thuốc",
                    Unit = pd.Medicine?.Unit ?? "Viên",
                    UnitPrice = pd.UnitPrice,
                    Quantity = pd.Quantity,
                    DosageInstruction = pd.DosageInstruction,
                    Status = pd.Status
                }).OrderBy(p => p.PrescriptionDetailId).ToList() ?? new List<PrescriptionItemViewModel>(),

                // Danh mục thuốc trong DB để bác sĩ chọn nhanh
                AvailableMedicines = (await _consultationService.SearchMedicinesAsync(null)).Select(m => new MedicineOptionViewModel
                {
                    MedicineId = m.Id,
                    Name = m.Name,
                    Unit = m.Unit,
                    Price = m.Price,
                    StockQuantity = m.StockQuantity,
                    Description = m.Description
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

        // 7. Tìm kiếm thuốc từ DB
        [HttpGet]
        public async Task<IActionResult> SearchMedicines(string? term)
        {
            var (isDoctor, _, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return Unauthorized();
            }

            var medicines = await _consultationService.SearchMedicinesAsync(term);
            var results = medicines.Select(m => new
            {
                id = m.Id,
                name = m.Name,
                unit = m.Unit,
                price = m.Price,
                priceDisplay = m.Price.ToString("N0") + " đ",
                stock = m.StockQuantity,
                description = m.Description ?? ""
            });

            return Json(results);
        }

        // 8. Thêm thuốc vào đơn thuốc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrescriptionItem(int appointmentId, int medicineId, int quantity, string? dosageInstruction)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return Unauthorized();
            }

            var result = await _consultationService.AddPrescriptionItemAsync(
                appointmentId, doctorId, medicineId, quantity, dosageInstruction ?? string.Empty);

            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                          Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                if (!result.Success || result.Item == null)
                {
                    return Json(new { success = false, message = result.Message });
                }

                return Json(new
                {
                    success = true,
                    message = result.Message,
                    item = new
                    {
                        prescriptionDetailId = result.Item.Id,
                        medicineId = result.Item.MedicineId,
                        medicineName = result.Item.Medicine?.Name ?? "Thuốc",
                        unit = result.Item.Medicine?.Unit ?? "Viên",
                        unitPrice = result.Item.UnitPrice,
                        unitPriceDisplay = result.Item.UnitPrice.ToString("N0") + " đ",
                        quantity = result.Item.Quantity,
                        totalPrice = result.Item.Quantity * result.Item.UnitPrice,
                        totalPriceDisplay = (result.Item.Quantity * result.Item.UnitPrice).ToString("N0") + " đ",
                        dosageInstruction = result.Item.DosageInstruction
                    }
                });
            }

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

        // 9. Xóa thuốc khỏi đơn thuốc
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePrescriptionItem(int appointmentId, int prescriptionDetailId)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return Unauthorized();
            }

            var result = await _consultationService.DeletePrescriptionItemAsync(
                appointmentId, doctorId, prescriptionDetailId);

            bool isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                          Request.Headers["Accept"].ToString().Contains("application/json");

            if (isAjax)
            {
                return Json(new { success = result.Success, message = result.Message });
            }

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

