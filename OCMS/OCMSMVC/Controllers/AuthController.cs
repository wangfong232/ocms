using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;

namespace OCMSMVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Role")))
            {
                return RedirectToRoleDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userService.AuthenticateAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
                return View(model);
            }

            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("FullName", user.FullName);
            HttpContext.Session.SetString("Email", user.Email);
            HttpContext.Session.SetString("Role", user.Role);
            if (!string.IsNullOrEmpty(user.PhoneNumber))
            {
                HttpContext.Session.SetString("PhoneNumber", user.PhoneNumber);
            }
            return RedirectToRoleDashboard(user.Role);
        }


        [HttpGet]
        public IActionResult Register()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Role")))
            {
                return RedirectToRoleDashboard();
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                await _userService.RegisterPatientAsync(model.FullName, model.Email, model.PhoneNumber, model.Password);

                TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction("Login");
            }
            catch (InvalidOperationException e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
        }


        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userService.GetUserbyEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("Email", "Địa chỉ email này chưa được đăng ký trong hệ thống phòng khám.");
                return View(model);
            }

            //OTP hardcode 
            string otp = Random.Shared.Next(100000, 999999).ToString();

            HttpContext.Session.SetString("ResetOtp", otp);
            HttpContext.Session.SetString("ResetEmail", model.Email.Trim().ToLower());
            HttpContext.Session.SetString("ResetExpires", DateTime.Now.AddMinutes(10).ToString("o"));

            TempData["DemoOtp"] = otp;
            TempData["InfoMessage"] = $"Mã xác thực OTP đã được tạo cho email {model.Email}.";

            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            var model = new ResetPasswordViewModel
            {
                Email = email ?? string.Empty
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var sessionOtp = HttpContext.Session.GetString("ResetOtp");
            var sessionEmail = HttpContext.Session.GetString("ResetEmail");
            var expiresStr = HttpContext.Session.GetString("ResetExpires");

            if (string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(expiresStr))
            {
                ModelState.AddModelError(string.Empty, "Phiên đặt lại mật khẩu đã hết hạn. Vui lòng yêu cầu mã xác thực mới.");
                return View(model);
            }

            if (model.OtpCode.Trim() != sessionOtp || !model.Email.Trim().Equals(sessionEmail, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("OtpCode", "Mã xác thực không hợp lệ.");
                return View(model);
            }

            bool success = await _userService.ResetPasswordAsync(model.Email, model.NewPassword);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy người dùng để cập nhật mật khẩu.");
                return View(model);
            }


            HttpContext.Session.Remove("ResetOtp");
            HttpContext.Session.Remove("ResetEmail");
            HttpContext.Session.Remove("ResetExpires");

            TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công! Vui lòng đăng nhập với mật khẩu mới.";
            return RedirectToAction("Login");
        }
        private IActionResult RedirectToRoleDashboard(string? role = null)
        {
            role ??= HttpContext.Session.GetString("Role");

            return role switch
            {
                "Admin" => RedirectToAction("Index", "Home"),
                "Doctor" => RedirectToAction("Index", "DoctorSchedule"),
                "Staff" => RedirectToAction("Index", "Home"),
                _ => RedirectToAction("Index", "Home"),
            };
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login");
            var user = await _userService.GetUserByIdAsync(userId.Value);
            if (user == null) return RedirectToAction("Login");
            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? "",
                Role = user.Role,
                DateOfBirth = user.Patient?.DateOfBirth,
                Gender = user.Patient?.Gender ?? "Other",
                Address = user.Patient?.Address,
                HealthInsuranceNumber = user.Patient?.HealthInsuranceNumber
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login");
            if (!ModelState.IsValid) return View(model);
            bool success = await _userService.UpdateProfileAsync(
                userId.Value,
                model.FullName,
                model.PhoneNumber,
                model.DateOfBirth,
                model.Gender,
                model.Address,
                model.HealthInsuranceNumber);
            if (success)
            {
                HttpContext.Session.SetString("FullName", model.FullName);
                HttpContext.Session.SetString("PhoneNumber", model.PhoneNumber);
                TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi cập nhật thông tin.";
            }
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            if (!HttpContext.Session.GetInt32("UserId").HasValue) return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login");
            if (!ModelState.IsValid) return View(model);
            var (success, message) = await _userService.ChangePasswordAsync(userId.Value, model.CurrentPassword, model.NewPassword);
            if (success)
            {
                TempData["SuccessMessage"] = message;
                return RedirectToAction("Profile");
            }
            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }
    }

}
