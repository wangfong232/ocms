using BusinessObjects.Entities;
using Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository userRepository;
        public UserService(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }

        public async Task<User?> AuthenticateAsync(string email, string password)
        {
            var user = await userRepository.GetByEmailAsync(email);
            if (user == null || !user.IsActive)
            {
                return null;
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);

            if (!isPasswordValid)
            {
                return null;
            }

            return user;
        }
        public async Task<User> RegisterPatientAsync(string fullName, string email, string phoneNumber, string password)
        {
            if (await userRepository.ExistsByEmailAsync(email))
            {
                throw new InvalidOperationException("User with this email already exists.");
            }

            if (await userRepository.ExistsByPhoneNumberAsync(phoneNumber))
            {
                throw new InvalidOperationException("User with this phone number already exists.");
            }

            var newUser = new User
            {
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Patient",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                Patient = new Patient
                {
                    Gender = "Other",
                    DateOfBirth = DateTime.Today.AddYears(-20)
                }
            };
            return await userRepository.AddAsync(newUser);
        }

        public async Task<User?> GetUserbyEmailAsync(string email)
        {
            return await userRepository.GetByEmailAsync(email);
        }

        public async Task<User?> GetUserByIdAsync(int id)
        {
            return await userRepository.GetByIdAsync(id);
        }

        public async Task<bool> ResetPasswordAsync(string email, string newPassword)
        {
            var user = await userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                return false;
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> UpdateProfileAsync(int userId, string fullName, string phoneNumber, DateTime? dob, string? gender, string? address, string? healthInsuranceNumber)
        {
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.FullName = fullName.Trim();
            user.PhoneNumber = phoneNumber.Trim();

            if (user.Patient != null)
            {
                if (dob.HasValue) user.Patient.DateOfBirth = dob.Value;
                if (!string.IsNullOrEmpty(gender)) user.Patient.Gender = gender;
                user.Patient.Address = address?.Trim();
                user.Patient.HealthInsuranceNumber = healthInsuranceNumber?.Trim();
            }
            await userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<(bool Success, string Message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await userRepository.GetByIdAsync(userId);
            if (user == null) return (false, "Người dùng không tồn tại.");

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                return (false, "Mật khẩu hiện tại không chính xác.");
            }

            if (currentPassword == newPassword)
            {
                return (false, "Mật khẩu mới không được trùng với mật khẩu hiện tại.");
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await userRepository.UpdateAsync(user);
            return (true, "Đổi mật khẩu thành công!");
        }

    }
}
