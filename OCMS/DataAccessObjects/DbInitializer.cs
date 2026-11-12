using BCrypt.Net;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace DataAccessObjects
{
    public static class DbInitializer
    {
        public static void Initialize(OcmsDbContext context)
        {
            context.Database.EnsureCreated();

            if (!context.Users.Any())
            {
                // 1. Seed Khoa (Departments)
                var deptNoi = new Department { Code = "INT", Name = "Khoa Nội Tổng Quát", Description = "Khám bệnh nội khoa tổng quát", BaseConsultationFee = 150000 };
                var deptNhi = new Department { Code = "PED", Name = "Khoa Nhi", Description = "Khám và điều trị bệnh nhi khoa", BaseConsultationFee = 150000 };
                var deptTMH = new Department { Code = "ENT", Name = "Khoa Tai Mũi Họng", Description = "Khám tai mũi họng chuyên sâu", BaseConsultationFee = 200000 };
                context.Departments.AddRange(deptNoi, deptNhi, deptTMH);
                context.SaveChanges();

                // 2. Seed Phòng khám (Rooms)
                var room101 = new Room { RoomNumber = "101", DepartmentId = deptNoi.Id, RoomType = "Consultation", IsOperational = true };
                var room102 = new Room { RoomNumber = "102", DepartmentId = deptNhi.Id, RoomType = "Consultation", IsOperational = true };
                var room201 = new Room { RoomNumber = "201", DepartmentId = deptTMH.Id, RoomType = "Consultation", IsOperational = true };
                context.Rooms.AddRange(room101, room102, room201);
                context.SaveChanges();

                // 3. Seed 4 Tài khoản test (Password mặc định: 123456)
                string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");
                var adminUser = new User { FullName = "System Administrator", Email = "admin@ocms.clinic", PhoneNumber = "0900000001", PasswordHash = defaultHash, Role = "Admin" };
                var staffUser = new User { FullName = "Lễ Tân Thu Ngân", Email = "staff@ocms.clinic", PhoneNumber = "0900000002", PasswordHash = defaultHash, Role = "Staff" };
                var doctorUser = new User { FullName = "BS. Nguyễn Văn An", Email = "doctor@ocms.clinic", PhoneNumber = "0900000003", PasswordHash = defaultHash, Role = "Doctor" };
                var patientUser = new User { FullName = "Trần Quang Phong", Email = "patient@ocms.clinic", PhoneNumber = "0900000004", PasswordHash = defaultHash, Role = "Patient" };
                context.Users.AddRange(adminUser, staffUser, doctorUser, patientUser);
                context.SaveChanges();

                // Seed hồ sơ Bác sĩ & Bệnh nhân liên kết
                var doctorProfile = new Doctor { UserId = doctorUser.Id, DepartmentId = deptNoi.Id, DefaultRoomId = room101.Id, AcademicTitle = "ThS.BS", PricePerSlot = 150000 };
                var patientProfile = new Patient { UserId = patientUser.Id, DateOfBirth = new DateTime(2000, 1, 1), Gender = "Male", Address = "Hà Nội", HealthInsuranceNumber = "DN4010123456789" };
                context.Doctors.Add(doctorProfile);
                context.Patients.Add(patientProfile);

                // 4. Seed Thuốc mẫu
                context.Medicines.AddRange(
                    new Medicine { Name = "Paracetamol 500mg", Unit = "Viên", Price = 2000, StockQuantity = 500, IsAvailable = true },
                    new Medicine { Name = "Amoxicillin 500mg", Unit = "Viên", Price = 5000, StockQuantity = 200, IsAvailable = true },
                    new Medicine { Name = "Decolgen Forte", Unit = "Vỉ", Price = 15000, StockQuantity = 100, IsAvailable = true },
                    new Medicine { Name = "Berocca Effervescent", Unit = "Tuýp", Price = 85000, StockQuantity = 50, IsAvailable = true }
                );

                // 5. Seed Dịch vụ Cận lâm sàng mẫu
                context.MedicalServices.AddRange(
                    new MedicalService { DepartmentId = deptNoi.Id, Name = "Xét nghiệm công thức máu toàn phần", Price = 120000, IsActive = true },
                    new MedicalService { DepartmentId = deptNoi.Id, Name = "Siêu âm ổ bụng tổng quát", Price = 180000, IsActive = true },
                    new MedicalService { DepartmentId = deptTMH.Id, Name = "Nội soi Tai Mũi Họng", Price = 200000, IsActive = true }
                );
                context.SaveChanges();
            }

            // Đảm bảo có lịch trực và cuộc hẹn mẫu cho ngày hôm nay để test
            EnsureTodayDemoData(context);
        }

        public static void EnsureTodayDemoData(OcmsDbContext context)
        {
            var today = DateTime.Today;

            var doctor = context.Doctors.Include(d => d.User).FirstOrDefault();
            var room = context.Rooms.FirstOrDefault();
            if (doctor == null || room == null) return;

            // 1. Tạo lịch làm việc hôm nay cho Bác sĩ nếu chưa có
            var schedule = context.DoctorSchedules
                .Include(s => s.TimeSlots)
                .FirstOrDefault(s => s.DoctorId == doctor.Id && s.WorkDate.Date == today);

            if (schedule == null)
            {
                schedule = new DoctorSchedule
                {
                    DoctorId = doctor.Id,
                    RoomId = room.Id,
                    WorkDate = today,
                    ShiftType = "Morning",
                    MaxQuota = 16,
                    Status = "Active"
                };
                context.DoctorSchedules.Add(schedule);
                context.SaveChanges();

                // Tạo 8 khung giờ (08:00 đến 12:00)
                var slots = new List<TimeSlot>
                {
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(8, 0, 0), EndTime = new TimeSpan(8, 30, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(8, 30, 0), EndTime = new TimeSpan(9, 0, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(9, 30, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 0, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(10, 0, 0), EndTime = new TimeSpan(10, 30, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(10, 30, 0), EndTime = new TimeSpan(11, 0, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(11, 0, 0), EndTime = new TimeSpan(11, 30, 0), Status = "Open" },
                    new TimeSlot { ScheduleId = schedule.Id, StartTime = new TimeSpan(11, 30, 0), EndTime = new TimeSpan(12, 0, 0), Status = "Open" },
                };
                context.TimeSlots.AddRange(slots);
                context.SaveChanges();
            }

            // 2. Tạo thêm 2 bệnh nhân test nếu chưa có
            string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");
            var patient1 = context.Patients.Include(p => p.User).FirstOrDefault();

            var patient2User = context.Users.FirstOrDefault(u => u.PhoneNumber == "0912345678");
            Patient? patient2 = null;
            if (patient2User == null)
            {
                patient2User = new User
                {
                    FullName = "Lê Thị Mai",
                    Email = "mai.le@gmail.com",
                    PhoneNumber = "0912345678",
                    PasswordHash = defaultHash,
                    Role = "Patient",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(patient2User);
                context.SaveChanges();

                patient2 = new Patient
                {
                    UserId = patient2User.Id,
                    DateOfBirth = new DateTime(1995, 5, 12),
                    Gender = "Female",
                    Address = "Cầu Giấy, Hà Nội",
                    HealthInsuranceNumber = "DN4010998877665"
                };
                context.Patients.Add(patient2);
                context.SaveChanges();
            }
            else
            {
                patient2 = context.Patients.FirstOrDefault(p => p.UserId == patient2User.Id);
            }

            var patient3User = context.Users.FirstOrDefault(u => u.PhoneNumber == "0988776655");
            Patient? patient3 = null;
            if (patient3User == null)
            {
                patient3User = new User
                {
                    FullName = "Hoàng Minh Đức",
                    Email = "duc.hoang@gmail.com",
                    PhoneNumber = "0988776655",
                    PasswordHash = defaultHash,
                    Role = "Patient",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                context.Users.Add(patient3User);
                context.SaveChanges();

                patient3 = new Patient
                {
                    UserId = patient3User.Id,
                    DateOfBirth = new DateTime(1988, 10, 20),
                    Gender = "Male",
                    Address = "Ba Đình, Hà Nội",
                    HealthInsuranceNumber = "DN4010112233445"
                };
                context.Patients.Add(patient3);
                context.SaveChanges();
            }
            else
            {
                patient3 = context.Patients.FirstOrDefault(p => p.UserId == patient3User.Id);
            }

            // 3. Seed 3 cuộc hẹn mẫu hôm nay nếu chưa có cuộc hẹn nào
            if (!context.Appointments.Any(a => a.AppointmentDate.Date == today))
            {
                var slots = context.TimeSlots.Where(t => t.ScheduleId == schedule.Id).OrderBy(t => t.StartTime).ToList();
                if (slots.Count >= 3 && patient1 != null && patient2 != null && patient3 != null)
                {
                    // Ca 1: Confirmed (đang chờ tới quầy check-in)
                    slots[0].Status = "Booked";
                    var apt1 = new Appointment
                    {
                        PatientId = patient1.Id,
                        DoctorId = doctor.Id,
                        TimeSlotId = slots[0].Id,
                        AppointmentDate = today.Add(slots[0].StartTime),
                        Status = "Confirmed",
                        Reason = "Đau họng, sốt nhẹ 2 ngày nay",
                        QrCodeToken = "OCMS-APT-1001",
                        CreatedAt = DateTime.UtcNow
                    };

                    // Ca 2: Confirmed (đang chờ tới quầy check-in)
                    slots[1].Status = "Booked";
                    var apt2 = new Appointment
                    {
                        PatientId = patient2.Id,
                        DoctorId = doctor.Id,
                        TimeSlotId = slots[1].Id,
                        AppointmentDate = today.Add(slots[1].StartTime),
                        Status = "Confirmed",
                        Reason = "Khám sức khỏe định kỳ & đau nửa đầu",
                        QrCodeToken = "OCMS-APT-1002",
                        CreatedAt = DateTime.UtcNow
                    };

                    // Ca 3: Đã check-in (Arrived) với QueueNumber = 1 để test màn hình TV sảnh & gọi số
                    slots[2].Status = "Booked";
                    var apt3 = new Appointment
                    {
                        PatientId = patient3.Id,
                        DoctorId = doctor.Id,
                        TimeSlotId = slots[2].Id,
                        AppointmentDate = today.Add(slots[2].StartTime),
                        Status = "Arrived",
                        QueueNumber = 1,
                        Reason = "Tái khám huyết áp & chóng mặt",
                        QrCodeToken = "OCMS-APT-1003",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.Appointments.AddRange(apt1, apt2, apt3);
                    context.SaveChanges();

                    // Seed sinh hiệu cho ca đã check-in
                    var vital3 = new VitalSigns
                    {
                        AppointmentId = apt3.Id,
                        SystolicBP = 125,
                        DiastolicBP = 82,
                        Pulse = 76,
                        SpO2 = 98,
                        Temperature = 36.8m,
                        Weight = 68.5m,
                        Height = 170.0m,
                        RecordedAt = DateTime.UtcNow
                    };
                    context.VitalSigns.Add(vital3);
                    context.SaveChanges();
                }
            }
        }

        public static void ResetDemoData(OcmsDbContext context)
        {
            var today = DateTime.Today;

            // Xóa các cuộc hẹn và sinh hiệu của hôm nay
            var todayApts = context.Appointments
                .Include(a => a.VitalSigns)
                .Where(a => a.AppointmentDate.Date == today)
                .ToList();

            foreach (var a in todayApts)
            {
                if (a.VitalSigns != null)
                {
                    context.VitalSigns.Remove(a.VitalSigns);
                }
            }
            context.Appointments.RemoveRange(todayApts);
            context.SaveChanges();

            // Mở lại các TimeSlots của hôm nay
            var todaySlots = context.TimeSlots
                .Include(t => t.DoctorSchedule)
                .Where(t => t.DoctorSchedule.WorkDate.Date == today)
                .ToList();

            foreach (var s in todaySlots)
            {
                s.Status = "Open";
            }
            context.SaveChanges();

            // Nạp lại 3 cuộc hẹn mẫu chuẩn
            EnsureTodayDemoData(context);
        }

        private static void AdoptEnsureCreatedDatabase(OcmsDbContext context)
        {
            if (!context.Database.CanConnect()) return;
            var connection = context.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere) connection.Open();

            bool hasLegacySchema;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.Appointments', N'U') IS NOT NULL THEN 1 ELSE 0 END";
                hasLegacySchema = Convert.ToInt32(command.ExecuteScalar()) == 1;
            }

            if (openedHere) connection.Close();
            if (!hasLegacySchema) return;

            context.Database.ExecuteSqlRaw("""
                CREATE TABLE [dbo].[__EFMigrationsHistory] (
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                );
                INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
                VALUES (N'20261016203146_InitialCreate', N'8.0.10');
                """);
        }
    }
}
