using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class DoctorScheduleController : Controller
    {
        private readonly IDoctorScheduleService _doctorScheduleService;

        public DoctorScheduleController(IDoctorScheduleService doctorScheduleService)
        {
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

        [HttpGet]
        public async Task<IActionResult> Index(string? date = null)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var today = DateTime.Today;
            const int maxDays = 14;

            // Xác định ngày hiển thị
            DateTime viewDate = today;
            if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
            {
                // Giới hạn trong cửa sổ ±14 ngày
                if (parsedDate.Date < today.AddDays(-maxDays)) viewDate = today.AddDays(-maxDays);
                else if (parsedDate.Date > today.AddDays(maxDays)) viewDate = today.AddDays(maxDays);
                else viewDate = parsedDate.Date;
            }

            var allSchedules = await _doctorScheduleService.GetMySchedulesAsync(doctorId);

            // Lấy tất cả ngày có ca trong cửa sổ ±14 ngày (để tính prev/next)
            var windowStart = today.AddDays(-maxDays);
            var windowEnd = today.AddDays(maxDays);
            var allDatesWithShifts = allSchedules
                .Where(s => s.WorkDate.Date >= windowStart && s.WorkDate.Date <= windowEnd)
                .Select(s => s.WorkDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            // Lọc ca theo ngày đang xem
            var targetSchedules = allSchedules
                .Where(s => s.WorkDate.Date == viewDate)
                .OrderBy(s => s.ShiftType)
                .ToList();

            // Tính ngày điều hướng
            var prevDate = viewDate.AddDays(-1);
            var nextDate = viewDate.AddDays(1);
            if (prevDate < windowStart) prevDate = windowStart;
            if (nextDate > windowEnd) nextDate = windowEnd;

            var firstDate = allDatesWithShifts.FirstOrDefault();
            var lastDate = allDatesWithShifts.LastOrDefault();

            ViewBag.ViewDate = viewDate;
            ViewBag.Today = today;
            ViewBag.PrevDate = prevDate.ToString("yyyy-MM-dd");
            ViewBag.NextDate = nextDate.ToString("yyyy-MM-dd");
            ViewBag.FirstDate = firstDate != default ? firstDate.ToString("yyyy-MM-dd") : null;
            ViewBag.LastDate = lastDate != default ? lastDate.ToString("yyyy-MM-dd") : null;
            ViewBag.WindowStart = windowStart.ToString("yyyy-MM-dd");
            ViewBag.WindowEnd = windowEnd.ToString("yyyy-MM-dd");
            ViewBag.MaxDays = maxDays;

            var viewModelList = new List<DoctorScheduleItemViewModel>();

            foreach (var s in targetSchedules)
            {
                int bookedCount = 0;
                var slotVMs = new List<TimeSlotItemViewModel>();

                foreach (var slot in s.TimeSlots.OrderBy(t => t.StartTime))
                {
                    bool isSlotBooked = slot.Appointments.Any(a => a.Status != "Cancelled");
                    if (isSlotBooked)
                    {
                        bookedCount++;
                    }

                    slotVMs.Add(new TimeSlotItemViewModel
                    {
                        Id = slot.Id,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        Status = slot.Status
                    });
                }

                // Ca trực có thể hủy khi: Đang Active, Chưa có bệnh nhân đặt lịch hẹn
                bool canCancel = s.Status == "Active" && bookedCount == 0 && s.WorkDate.Date >= today;

                viewModelList.Add(new DoctorScheduleItemViewModel
                {
                    Id = s.Id,
                    WorkDate = s.WorkDate,
                    ShiftType = s.ShiftType,
                    RoomNumber = s.Room?.RoomNumber ?? "Chưa xếp",
                    MaxQuota = s.MaxQuota,
                    Status = s.Status,
                    BookedSlotsCount = bookedCount,
                    CanCancel = canCancel,
                    TimeSlots = slotVMs
                });
            }

            return View(viewModelList);
        }

        [HttpGet]
        public async Task<IActionResult> Register()

        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var doctor = await _doctorScheduleService.GetDoctorByUserIdAsync(HttpContext.Session.GetInt32("UserId")!.Value);
            var rooms = await _doctorScheduleService.GetAvailableRoomsForDoctorAsync(doctorId);

            var model = new RegisterShiftViewModel
            {
                WorkDate = DateTime.Today.AddDays(1),
                ShiftType = "Morning",
                // Tự động chọn sẵn phòng mặc định của Bác sĩ nếu có
                RoomId = doctor?.DefaultRoomId ?? (rooms.FirstOrDefault()?.Id ?? 0),
                AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterShiftViewModel model)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            if (!ModelState.IsValid)
            {
                var rooms = await _doctorScheduleService.GetAvailableRoomsForDoctorAsync(doctorId);
                model.AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList();
                return View(model);
            }

            var result = await _doctorScheduleService.RegisterShiftAsync(doctorId, model.WorkDate, model.ShiftType, model.RoomId);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                var rooms = await _doctorScheduleService.GetAvailableRoomsForDoctorAsync(doctorId);
                model.AvailableRooms = rooms.Select(r => new RoomItemViewModel
                {
                    Id = r.Id,
                    RoomNumber = r.RoomNumber
                }).ToList();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? date = null)
        {
            var (isDoctor, doctorId, _) = await GetCurrentDoctorAsync();
            if (!isDoctor)
            {
                return RedirectToAction("Login", "Auth");
            }

            var result = await _doctorScheduleService.CancelShiftAsync(id, doctorId);
            if (result.Success)
            {
                TempData["SuccessMessage"] = result.Message;
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
            }

            return RedirectToAction(nameof(Index), new { date });
        }
    }
}

