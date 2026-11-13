using Microsoft.AspNetCore.Mvc;
using OCMSMVC.Models;
using Services;
using System;
using System.Threading.Tasks;

namespace OCMSMVC.Controllers
{
    public class AuthController : Controller
    {
        private readonly IUserService _userService;
        private readonly IEmailService _emailService;

        public AuthController(IUserService userService, IEmailService emailService)
        {
            _userService = userService;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Role")))
            {
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToRoleDashboard();
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

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

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản của bạn hiện đang bị tạm khóa. Vui lòng liên hệ quản trị viên.");
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

            TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {user.FullName}.";

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToRoleDashboard(user.Role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            TempData["SuccessMessage"] = "Bạn đã đăng xuất an toàn khỏi hệ thống.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
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

            var existingUser = await _userService.GetUserbyEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Địa chỉ email này đã được đăng ký tài khoản trong hệ thống.");
                return View(model);
            }

            string otp = Random.Shared.Next(100000, 999999).ToString();

            HttpContext.Session.SetString("Register_FullName", model.FullName);
            HttpContext.Session.SetString("Register_Email", model.Email.Trim().ToLower());
            HttpContext.Session.SetString("Register_Phone", model.PhoneNumber);
            HttpContext.Session.SetString("Register_Password", model.Password);
            HttpContext.Session.SetString("Register_Otp", otp);
            HttpContext.Session.SetString("Register_Expires", DateTime.UtcNow.AddMinutes(10).ToString("o"));

            string emailBody = $@"
            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                <h2 style='color: #0d6efd; text-align: center;'>Hệ Thống Phòng Khám OCMS</h2>
                <p>Xin chào <strong>{model.FullName}</strong>,</p>
                <p>Cảm ơn bạn đã đăng ký tài khoản. Để kích hoạt tài khoản, vui lòng nhập mã OTP dưới đây:</p>
                <div style='background-color: #f8f9fa; border: 2px dashed #0d6efd; text-align: center; padding: 15px; margin: 20px 0;'>
                    <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #0d6efd;'>{otp}</span>
                </div>
                <p style='color: #dc3545;'><strong>Lưu ý:</strong> Mã có hiệu lực trong vòng <strong>10 phút</strong>.</p>
            </div>";

            await _emailService.SendEmailAsync(model.Email, "[OCMS Clinic] Mã OTP xác nhận kích hoạt tài khoản", emailBody);

            TempData["InfoMessage"] = $"Mã OTP đã được gửi tới email {model.Email}. Vui lòng nhập để hoàn tất đăng ký.";

            return RedirectToAction(nameof(VerifyRegisterOtp));
        }

        [HttpGet]
        public IActionResult VerifyRegisterOtp()
        {
            var email = HttpContext.Session.GetString("Register_Email");
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Phiên đăng ký không hợp lệ. Vui lòng đăng ký lại.";
                return RedirectToAction(nameof(Register));
            }

            return View(new VerifyRegisterOtpViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyRegisterOtp(VerifyRegisterOtpViewModel model)
        {
            var sessionEmail = HttpContext.Session.GetString("Register_Email");
            var sessionOtp = HttpContext.Session.GetString("Register_Otp");
            var expiresStr = HttpContext.Session.GetString("Register_Expires");
            var fullName = HttpContext.Session.GetString("Register_FullName");
            var phone = HttpContext.Session.GetString("Register_Phone");
            var password = HttpContext.Session.GetString("Register_Password");

            if (string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(expiresStr))
            {
                ModelState.AddModelError(string.Empty, "Phiên đăng ký đã hết hạn. Vui lòng đăng ký lại.");
                return View(model);
            }

            if (DateTime.TryParse(expiresStr, out var expires) && DateTime.UtcNow > expires)
            {
                ModelState.AddModelError(string.Empty, "Mã xác thực OTP đã hết hạn (quá 10 phút).");
                return View(model);
            }

            if (model.OtpCode.Trim() != sessionOtp)
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã xác thực OTP không chính xác.");
                return View(model);
            }

            try
            {
                await _userService.RegisterPatientAsync(fullName!, sessionEmail, phone!, password!);

                HttpContext.Session.Remove("Register_FullName");
                HttpContext.Session.Remove("Register_Email");
                HttpContext.Session.Remove("Register_Phone");
                HttpContext.Session.Remove("Register_Password");
                HttpContext.Session.Remove("Register_Otp");
                HttpContext.Session.Remove("Register_Expires");

                TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendRegisterOtp()
        {
            var email = HttpContext.Session.GetString("Register_Email");
            var fullName = HttpContext.Session.GetString("Register_FullName") ?? "Quý khách";

            if (string.IsNullOrEmpty(email))
            {
                return Json(new { success = false, message = "Không tìm thấy phiên đăng ký." });
            }

            string otp = Random.Shared.Next(100000, 999999).ToString();
            HttpContext.Session.SetString("Register_Otp", otp);
            HttpContext.Session.SetString("Register_Expires", DateTime.UtcNow.AddMinutes(10).ToString("o"));

            string emailBody = $"<p>Mã OTP mới của bạn là: <strong>{otp}</strong> (hiệu lực 10 phút).</p>";
            await _emailService.SendEmailAsync(email, "[OCMS Clinic] Mã OTP kích hoạt tài khoản mới", emailBody);

            return Json(new { success = true, message = $"Đã gửi lại mã OTP mới tới {email}." });
        }

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userService.GetUserbyEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError("Email", "Địa chỉ email này chưa được đăng ký trong hệ thống.");
                return View(model);
            }

            string otp = Random.Shared.Next(100000, 999999).ToString();
            HttpContext.Session.SetString("ResetOtp", otp);
            HttpContext.Session.SetString("ResetEmail", model.Email.Trim().ToLower());
            HttpContext.Session.SetString("ResetExpires", DateTime.UtcNow.AddMinutes(10).ToString("o"));

            string emailBody = $"<p>Mã OTP đặt lại mật khẩu của bạn là: <strong>{otp}</strong> (hiệu lực 10 phút).</p>";
            await _emailService.SendEmailAsync(model.Email, "[OCMS Clinic] Mã OTP đặt lại mật khẩu", emailBody);

            TempData["InfoMessage"] = $"Mã OTP đã được gửi tới email {model.Email}.";

            return RedirectToAction(nameof(ResetPassword));
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            if (string.IsNullOrEmpty(email)) return RedirectToAction(nameof(ForgotPassword));
            return View(new ResetPasswordViewModel { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var sessionOtp = HttpContext.Session.GetString("ResetOtp");
            var sessionEmail = HttpContext.Session.GetString("ResetEmail");
            var expiresStr = HttpContext.Session.GetString("ResetExpires");

            if (string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(sessionEmail) || string.IsNullOrEmpty(expiresStr))
            {
                ModelState.AddModelError(string.Empty, "Phiên đặt lại mật khẩu đã hết hạn.");
                return View(model);
            }

            if (DateTime.TryParse(expiresStr, out var expires) && DateTime.UtcNow > expires)
            {
                ModelState.AddModelError(string.Empty, "Mã xác thực OTP đã hết hạn.");
                return View(model);
            }

            if (model.OtpCode.Trim() != sessionOtp || !model.Email.Trim().Equals(sessionEmail, StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError("OtpCode", "Mã xác thực OTP không chính xác.");
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

            TempData["SuccessMessage"] = "Mật khẩu đã được đặt lại thành công! Vui lòng đăng nhập.";
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction(nameof(Login));

            var user = await _userService.GetUserByIdAsync(userId.Value);
            if (user == null) return RedirectToAction(nameof(Login));

            return View(new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber ?? "",
                Role = user.Role,
                DateOfBirth = user.Patient?.DateOfBirth,
                Gender = user.Patient?.Gender ?? "Other",
                Address = user.Patient?.Address,
                HealthInsuranceNumber = user.Patient?.HealthInsuranceNumber
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid) return View(model);

            bool success = await _userService.UpdateProfileAsync(
                userId.Value, model.FullName, model.PhoneNumber, model.DateOfBirth, model.Gender, model.Address, model.HealthInsuranceNumber);

            if (success)
            {
                HttpContext.Session.SetString("FullName", model.FullName);
                HttpContext.Session.SetString("PhoneNumber", model.PhoneNumber);
                TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
            }
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction(nameof(Login));

            var user = await _userService.GetUserByIdAsync(userId.Value);
            ViewBag.UserEmail = user?.Email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendChangePasswordOtp()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Json(new { success = false, message = "Vui lòng đăng nhập lại." });

            var user = await _userService.GetUserByIdAsync(userId.Value);
            if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại." });

            string otp = Random.Shared.Next(100000, 999999).ToString();
            HttpContext.Session.SetString("ChangePasswordOtp", otp);
            HttpContext.Session.SetString("ChangePasswordExpires", DateTime.UtcNow.AddMinutes(10).ToString("o"));

            string emailBody = $"<p>Mã OTP đổi mật khẩu của bạn là: <strong>{otp}</strong> (hiệu lực 10 phút).</p>";
            await _emailService.SendEmailAsync(user.Email, "[OCMS Clinic] Mã OTP xác nhận đổi mật khẩu", emailBody);

            return Json(new { success = true, message = $"Mã OTP đã được gửi tới email {user.Email}." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction(nameof(Login));

            var user = await _userService.GetUserByIdAsync(userId.Value);
            ViewBag.UserEmail = user?.Email;

            if (!ModelState.IsValid) return View(model);

            var sessionOtp = HttpContext.Session.GetString("ChangePasswordOtp");
            var expiresStr = HttpContext.Session.GetString("ChangePasswordExpires");

            if (string.IsNullOrEmpty(sessionOtp) || string.IsNullOrEmpty(expiresStr))
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Vui lòng bấm 'Nhận mã OTP' trước khi đổi mật khẩu.");
                return View(model);
            }

            if (DateTime.TryParse(expiresStr, out var expires) && DateTime.UtcNow > expires)
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã OTP đã hết hiệu lực. Vui lòng nhận lại mã.");
                return View(model);
            }

            if (model.OtpCode.Trim() != sessionOtp)
            {
                ModelState.AddModelError(nameof(model.OtpCode), "Mã xác thực OTP không chính xác.");
                return View(model);
            }

            var (success, message) = await _userService.ChangePasswordAsync(userId.Value, model.CurrentPassword, model.NewPassword);
            if (success)
            {
                HttpContext.Session.Remove("ChangePasswordOtp");
                HttpContext.Session.Remove("ChangePasswordExpires");
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        private IActionResult RedirectToRoleDashboard(string? role = null)
        {
            role ??= HttpContext.Session.GetString("Role");
            return role switch
            {
                "Admin" => RedirectToAction("Index", "Admin"),
                "Doctor" => RedirectToAction("Queue", "Consultation"),
                "Staff" => RedirectToAction("CheckIn", "Reception"),
                _ => RedirectToAction("Index", "Home"),
            };
        }
    }
}