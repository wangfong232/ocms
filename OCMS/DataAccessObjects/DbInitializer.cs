using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using BCrypt.Net;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects
{
    public static class DbInitializer
    {
        public static void Initialize(OcmsDbContext context)
        {
            // 1. Tạo database và schema cơ bản nếu chưa tồn tại
            context.Database.EnsureCreated();

            // 2. Tự động kiểm tra và vá các cột mới vào CSDL nếu người khác pull git về bị thiếu cột
            AutoPatchSchema(context);

            // 3. Đánh dấu migration history baseline nếu database được tạo bằng EnsureCreated
            AdoptEnsureCreatedDatabase(context);

            // 4. Nạp danh mục dữ liệu nền (Chuyên khoa, Phòng khám, Users các vai trò, Thuốc, Dịch vụ CLS)
            SeedMasterData(context);

            // 5. Đảm bảo có lịch trực và các cuộc hẹn mẫu hôm nay (Confirmed, Arrived, InConsultation, Completed)
            EnsureTodayDemoData(context);
        }

        /// <summary>
        /// Tự động kiểm tra INFORMATION_SCHEMA và bổ sung các cột mới (non-destructive)
        /// Giúp các thành viên trong nhóm khi pull git về không bị crash do lệch cột trong CSDL đã tồn tại.
        /// </summary>
        public static void AutoPatchSchema(OcmsDbContext context)
        {
            try
            {
                if (!context.Database.CanConnect()) return;

                var patchScripts = new[]
                {
                    // Table: Appointments
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Appointments')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'ConsultationFee')
                            ALTER TABLE Appointments ADD ConsultationFee DECIMAL(18,2) NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'CancellationReason')
                            ALTER TABLE Appointments ADD CancellationReason NVARCHAR(500) NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'QrCodeToken')
                            ALTER TABLE Appointments ADD QrCodeToken NVARCHAR(100) NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'QueueNumber')
                            ALTER TABLE Appointments ADD QueueNumber INT NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'RescheduledCount')
                            ALTER TABLE Appointments ADD RescheduledCount INT NOT NULL CONSTRAINT DF_Appointments_RescheduledCount DEFAULT 0;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'UpdatedAt')
                            ALTER TABLE Appointments ADD UpdatedAt DATETIME2 NULL;
                    END
                    """,

                    // Table: Doctors
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Doctors')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Doctors' AND COLUMN_NAME = 'DefaultRoomId')
                        BEGIN
                            ALTER TABLE Doctors ADD DefaultRoomId INT NULL;
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Rooms')
                               ALTER TABLE Doctors ADD CONSTRAINT FK_Doctors_Rooms FOREIGN KEY (DefaultRoomId) REFERENCES Rooms(Id);
                        END
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Doctors' AND COLUMN_NAME = 'AcademicTitle')
                            ALTER TABLE Doctors ADD AcademicTitle NVARCHAR(50) NOT NULL CONSTRAINT DF_Doctors_AcademicTitle DEFAULT 'BS';
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Doctors' AND COLUMN_NAME = 'PricePerSlot')
                            ALTER TABLE Doctors ADD PricePerSlot DECIMAL(18,2) NOT NULL CONSTRAINT DF_Doctors_PricePerSlot DEFAULT 150000;
                    END
                    """,

                    // Table: Payments
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Payments')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'CashierId')
                        BEGIN
                            ALTER TABLE Payments ADD CashierId INT NULL;
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
                               ALTER TABLE Payments ADD CONSTRAINT FK_Payments_Users FOREIGN KEY (CashierId) REFERENCES Users(Id);
                        END
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'PaymentType')
                            ALTER TABLE Payments ADD PaymentType NVARCHAR(30) NOT NULL CONSTRAINT DF_Payments_PaymentType DEFAULT 'Deposit';
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'TransactionId')
                            ALTER TABLE Payments ADD TransactionId NVARCHAR(100) NULL;
                    END
                    """,

                    // Table: PrescriptionDetails
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'PrescriptionDetails')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PrescriptionDetails' AND COLUMN_NAME = 'DispensedAt')
                            ALTER TABLE PrescriptionDetails ADD DispensedAt DATETIME2 NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PrescriptionDetails' AND COLUMN_NAME = 'DispensedByUserId')
                        BEGIN
                            ALTER TABLE PrescriptionDetails ADD DispensedByUserId INT NULL;
                            IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
                               ALTER TABLE PrescriptionDetails ADD CONSTRAINT FK_PrescriptionDetails_Users FOREIGN KEY (DispensedByUserId) REFERENCES Users(Id);
                        END
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PrescriptionDetails' AND COLUMN_NAME = 'Status')
                            ALTER TABLE PrescriptionDetails ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_PrescriptionDetails_Status DEFAULT 'Pending';
                    END
                    """,

                    // Table: Medicines
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Medicines')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medicines' AND COLUMN_NAME = 'StockQuantity')
                            ALTER TABLE Medicines ADD StockQuantity INT NOT NULL CONSTRAINT DF_Medicines_StockQuantity DEFAULT 100;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medicines' AND COLUMN_NAME = 'Description')
                            ALTER TABLE Medicines ADD Description NVARCHAR(255) NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Medicines' AND COLUMN_NAME = 'IsAvailable')
                            ALTER TABLE Medicines ADD IsAvailable BIT NOT NULL CONSTRAINT DF_Medicines_IsAvailable DEFAULT 1;
                    END
                    """,

                    // Table: MedicalServices
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MedicalServices')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MedicalServices' AND COLUMN_NAME = 'Description')
                            ALTER TABLE MedicalServices ADD Description NVARCHAR(255) NULL;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'MedicalServices' AND COLUMN_NAME = 'IsActive')
                            ALTER TABLE MedicalServices ADD IsActive BIT NOT NULL CONSTRAINT DF_MedicalServices_IsActive DEFAULT 1;
                    END
                    """,

                    // Table: DoctorSchedules
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DoctorSchedules')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorSchedules' AND COLUMN_NAME = 'MaxQuota')
                            ALTER TABLE DoctorSchedules ADD MaxQuota INT NOT NULL CONSTRAINT DF_DoctorSchedules_MaxQuota DEFAULT 16;
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorSchedules' AND COLUMN_NAME = 'ShiftType')
                            ALTER TABLE DoctorSchedules ADD ShiftType NVARCHAR(20) NOT NULL CONSTRAINT DF_DoctorSchedules_ShiftType DEFAULT 'Morning';
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DoctorSchedules' AND COLUMN_NAME = 'Status')
                            ALTER TABLE DoctorSchedules ADD Status NVARCHAR(20) NOT NULL CONSTRAINT DF_DoctorSchedules_Status DEFAULT 'Active';
                    END
                    """,

                    // Table: TimeSlots
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TimeSlots')
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TimeSlots' AND COLUMN_NAME = 'HoldExpiresAt')
                            ALTER TABLE TimeSlots ADD HoldExpiresAt DATETIME2 NULL;

                        -- Tự động giải phóng toàn bộ các slot cũ bị kẹt trạng thái 'Hold'
                        UPDATE TimeSlots SET Status = 'Open', HoldExpiresAt = NULL WHERE Status = 'Hold' AND HoldExpiresAt IS NULL;
                    END
                    """,

                    // Auto-clean overdue appointments (Lịch hẹn quá hạn)
                    """
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Appointments')
                    BEGIN
                        -- Chuyển các ca khám ngày cũ còn dở dang sang Đã hoàn thành (Completed)
                        UPDATE Appointments
                        SET Status = 'Completed'
                        WHERE CAST(AppointmentDate AS DATE) < CAST(GETDATE() AS DATE)
                          AND Status = 'InConsultation';

                        -- Chuyển các lịch hẹn ngày cũ chưa khám hoặc vắng mặt sang NoShow (Bỏ khám)
                        UPDATE Appointments
                        SET Status = 'NoShow',
                            CancellationReason = COALESCE(CancellationReason, N'Quá ngày hẹn - Bệnh nhân không đến khám')
                        WHERE CAST(AppointmentDate AS DATE) < CAST(GETDATE() AS DATE)
                          AND Status IN ('Confirmed', 'Pending', 'Arrived');
                    END
                    """
                };

                foreach (var sql in patchScripts)
                {
                    context.Database.ExecuteSqlRaw(sql);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutoPatchSchema] Lưu ý: {ex.Message}");
            }
        }

        /// <summary>
        /// Nạp toàn bộ dữ liệu nền tảng (Master Data): Khoa, Phòng, Users (tất cả vai trò), Bác sĩ, Bệnh nhân, Thuốc, Dịch vụ CLS
        /// </summary>
        public static void SeedMasterData(OcmsDbContext context)
        {
            // 1. Chuyên khoa (Departments)
            var existingDeptCodes = context.Departments.Select(d => d.Code).ToHashSet();
            var departmentsToAdd = new List<Department>
            {
                new Department { Code = "INT", Name = "Khoa Nội Tổng Quát", Description = "Khám và điều trị các bệnh nội khoa tổng quát", BaseConsultationFee = 150000 },
                new Department { Code = "PED", Name = "Khoa Nhi", Description = "Khám và chăm sóc sức khỏe toàn diện cho trẻ em", BaseConsultationFee = 150000 },
                new Department { Code = "ENT", Name = "Khoa Tai Mũi Họng", Description = "Khám, nội soi và điều trị các bệnh lý tai mũi họng", BaseConsultationFee = 200000 },
                new Department { Code = "CAR", Name = "Khoa Tim Mạch", Description = "Chẩn đoán, theo dõi và điều trị bệnh lý tim mạch, huyết áp", BaseConsultationFee = 250000 },
                new Department { Code = "DER", Name = "Khoa Da Liễu", Description = "Khám và điều trị chuyên sâu các bệnh lý về da, thẩm mỹ da", BaseConsultationFee = 180000 },
                new Department { Code = "OPH", Name = "Khoa Mắt", Description = "Khám mắt, đo thị lực, tầm soát các bệnh lý về mắt", BaseConsultationFee = 180000 }
            };

            foreach (var dept in departmentsToAdd)
            {
                if (!existingDeptCodes.Contains(dept.Code))
                {
                    context.Departments.Add(dept);
                }
            }
            context.SaveChanges();

            // Lấy map mã khoa -> ID
            var deptMap = context.Departments.ToDictionary(d => d.Code, d => d.Id);

            // 2. Phòng khám (Rooms)
            var existingRoomNumbers = context.Rooms.Select(r => r.RoomNumber).ToHashSet();
            var roomsToAdd = new List<Room>
            {
                new Room { RoomNumber = "101", DepartmentId = deptMap["INT"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "102", DepartmentId = deptMap["PED"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "103", DepartmentId = deptMap["INT"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "104", DepartmentId = deptMap["PED"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "201", DepartmentId = deptMap["ENT"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "202", DepartmentId = deptMap["CAR"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "203", DepartmentId = deptMap["DER"], RoomType = "Consultation", IsOperational = true },
                new Room { RoomNumber = "204", DepartmentId = deptMap["OPH"], RoomType = "Consultation", IsOperational = true }
            };

            foreach (var r in roomsToAdd)
            {
                if (!existingRoomNumbers.Contains(r.RoomNumber))
                {
                    context.Rooms.Add(r);
                }
            }
            context.SaveChanges();

            var roomMap = context.Rooms.ToDictionary(r => r.RoomNumber, r => r.Id);

            // 3. Tài khoản người dùng (Users) - Mật khẩu đồng nhất cho mọi tài khoản test: 123456
            string defaultHash = BCrypt.Net.BCrypt.HashPassword("123456");

            var usersToSeed = new List<(string FullName, string Email, string Phone, string Role)>
            {
                // Ban quản trị & Vận hành quầy
                ("System Administrator", "admin@ocms.clinic", "0900000001", "Admin"),
                ("Lễ Tân Nguyễn Thị Mai", "staff@ocms.clinic", "0900000002", "Staff"),
                ("Thu Ngân Trần Thu Thảo", "cashier@ocms.clinic", "0900000005", "Staff"),
                ("Dược Sĩ Hoàng Hải Đăng", "pharmacist@ocms.clinic", "0900000006", "Staff"),

                // Đội ngũ Bác sĩ chuyên khoa
                ("BS. ThS. Nguyễn Văn An", "doctor.an@ocms.clinic", "0900000003", "Doctor"),
                ("BS. CKI. Trần Thu Hà", "doctor.ha@ocms.clinic", "0900000007", "Doctor"),
                ("BS. CKII. Lê Hoàng Long", "doctor.long@ocms.clinic", "0900000008", "Doctor"),
                ("PGS.TS. Phạm Minh Tuấn", "doctor.tuan@ocms.clinic", "0900000009", "Doctor"),
                ("BS. ThS. Vũ Mai Phương", "doctor.phuong@ocms.clinic", "0900000010", "Doctor"),

                // Danh sách Bệnh nhân mẫu
                ("Trần Quang Phong", "patient1@ocms.clinic", "0900000004", "Patient"),
                ("Lê Thị Mai", "patient2@ocms.clinic", "0912345678", "Patient"),
                ("Hoàng Minh Đức", "patient3@ocms.clinic", "0988776655", "Patient"),
                ("Nguyễn Thị Lan Anh", "patient4@ocms.clinic", "0933445566", "Patient"),
                ("Bùi Quốc Khánh", "patient5@ocms.clinic", "0977112233", "Patient")
            };

            foreach (var item in usersToSeed)
            {
                if (!context.Users.Any(u => u.Email == item.Email || u.PhoneNumber == item.Phone))
                {
                    context.Users.Add(new User
                    {
                        FullName = item.FullName,
                        Email = item.Email,
                        PhoneNumber = item.Phone,
                        PasswordHash = defaultHash,
                        Role = item.Role,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            context.SaveChanges();

            // 4. Hồ sơ Bác sĩ (Doctor Profiles)
            var doctorConfigs = new List<(string Email, string DeptCode, string RoomNum, string AcademicTitle, decimal Price)>
            {
                ("doctor.an@ocms.clinic", "INT", "101", "ThS.BS", 150000),
                ("doctor.ha@ocms.clinic", "PED", "102", "BS.CKI", 180000),
                ("doctor.long@ocms.clinic", "ENT", "201", "BS.CKII", 200000),
                ("doctor.tuan@ocms.clinic", "CAR", "202", "PGS.TS", 250000),
                ("doctor.phuong@ocms.clinic", "DER", "203", "ThS.BS", 180000),
            };

            foreach (var cfg in doctorConfigs)
            {
                var docUser = context.Users.FirstOrDefault(u => u.Email == cfg.Email);
                if (docUser != null && !context.Doctors.Any(d => d.UserId == docUser.Id))
                {
                    context.Doctors.Add(new Doctor
                    {
                        UserId = docUser.Id,
                        DepartmentId = deptMap[cfg.DeptCode],
                        DefaultRoomId = roomMap.GetValueOrDefault(cfg.RoomNum),
                        AcademicTitle = cfg.AcademicTitle,
                        PricePerSlot = cfg.Price
                    });
                }
            }
            context.SaveChanges();

            // 5. Hồ sơ Bệnh nhân (Patient Profiles)
            var patientConfigs = new List<(string Email, DateTime Dob, string Gender, string Address, string Insurance)>
            {
                ("patient1@ocms.clinic", new DateTime(2000, 1, 1), "Male", "Hà Nội", "DN4010123456789"),
                ("patient2@ocms.clinic", new DateTime(1995, 5, 12), "Female", "Cầu Giấy, Hà Nội", "DN4010998877665"),
                ("patient3@ocms.clinic", new DateTime(1988, 10, 20), "Male", "Ba Đình, Hà Nội", "DN4010112233445"),
                ("patient4@ocms.clinic", new DateTime(2002, 8, 15), "Female", "Đống Đa, Hà Nội", "DN4010223344556"),
                ("patient5@ocms.clinic", new DateTime(1975, 3, 30), "Male", "Nam Từ Liêm, Hà Nội", "DN4010334455667")
            };

            foreach (var cfg in patientConfigs)
            {
                var patUser = context.Users.FirstOrDefault(u => u.Email == cfg.Email);
                if (patUser != null && !context.Patients.Any(p => p.UserId == patUser.Id))
                {
                    context.Patients.Add(new Patient
                    {
                        UserId = patUser.Id,
                        DateOfBirth = cfg.Dob,
                        Gender = cfg.Gender,
                        Address = cfg.Address,
                        HealthInsuranceNumber = cfg.Insurance
                    });
                }
            }
            context.SaveChanges();

            // 6. Danh mục Thuốc trong kho (Medicines)
            var medicinesToSeed = new List<Medicine>
            {
                new Medicine { Name = "Paracetamol 500mg", Unit = "Viên", Price = 2000, StockQuantity = 1000, Description = "Thuốc giảm đau, hạ sốt", IsAvailable = true },
                new Medicine { Name = "Amoxicillin 500mg", Unit = "Viên", Price = 5000, StockQuantity = 500, Description = "Kháng sinh nhóm Penicillin trị nhiễm khuẩn", IsAvailable = true },
                new Medicine { Name = "Augmentin 1g", Unit = "Viên", Price = 18000, StockQuantity = 300, Description = "Kháng sinh phổ rộng Amoxicillin + Acid Clavulanic", IsAvailable = true },
                new Medicine { Name = "Decolgen Forte", Unit = "Vỉ", Price = 15000, StockQuantity = 200, Description = "Giảm cảm cúm, nghẹt mũi, nhức đầu", IsAvailable = true },
                new Medicine { Name = "Berocca Effervescent", Unit = "Tuýp", Price = 85000, StockQuantity = 150, Description = "Viên sủi bổ sung vitamin B, C và khoáng chất", IsAvailable = true },
                new Medicine { Name = "Gaviscon Dual Action", Unit = "Gói", Price = 12000, StockQuantity = 400, Description = "Giảm triệu chứng trào ngược dạ dày thực quản", IsAvailable = true },
                new Medicine { Name = "Cefuroxime 500mg", Unit = "Viên", Price = 9500, StockQuantity = 350, Description = "Kháng sinh Cephalosporin thế hệ 2", IsAvailable = true },
                new Medicine { Name = "Panadol Extra", Unit = "Vỉ", Price = 18000, StockQuantity = 300, Description = "Giảm đau mạnh chứa Paracetamol & Caffeine", IsAvailable = true },
                new Medicine { Name = "Omeprazole 20mg", Unit = "Viên", Price = 3500, StockQuantity = 600, Description = "Thuốc ức chế bơm proton trị viêm loét dạ dày", IsAvailable = true },
                new Medicine { Name = "Loratadine 10mg", Unit = "Viên", Price = 2500, StockQuantity = 400, Description = "Thuốc kháng histamin giảm dị ứng, mề đay", IsAvailable = true },
                new Medicine { Name = "Ibuprofen 400mg", Unit = "Viên", Price = 4000, StockQuantity = 500, Description = "Thuốc kháng viêm không steroid (NSAID)", IsAvailable = true },
                new Medicine { Name = "Alphachymotrypsin", Unit = "Vỉ", Price = 25000, StockQuantity = 250, Description = "Thuốc kháng phù nề dạng men sau chấn thương", IsAvailable = true },
                new Medicine { Name = "Natri Clorid 0.9% 500ml", Unit = "Chai", Price = 10000, StockQuantity = 200, Description = "Dung dịch nước muối sinh lý sát khuẩn", IsAvailable = true },
                new Medicine { Name = "Oresol 245", Unit = "Gói", Price = 5000, StockQuantity = 500, Description = "Bột pha bù nước và điện giải khi sốt hoặc tiêu chảy", IsAvailable = true }
            };

            foreach (var med in medicinesToSeed)
            {
                if (!context.Medicines.Any(m => m.Name == med.Name))
                {
                    context.Medicines.Add(med);
                }
            }
            context.SaveChanges();

            // 7. Danh mục Dịch vụ Cận lâm sàng (MedicalServices)
            var servicesToSeed = new List<MedicalService>
            {
                new MedicalService { DepartmentId = deptMap["INT"], Name = "Xét nghiệm công thức máu toàn phần", Price = 120000, Description = "Đánh giá các tế bào máu cơ bản", IsActive = true },
                new MedicalService { DepartmentId = deptMap["INT"], Name = "Siêu âm ổ bụng tổng quát", Price = 180000, Description = "Khảo sát hình ảnh gan, mật, thận, lách, tụy", IsActive = true },
                new MedicalService { DepartmentId = deptMap["INT"], Name = "Chụp X-Quang ngực thẳng", Price = 150000, Description = "Khảo sát tim phổi và lồng ngực", IsActive = true },
                new MedicalService { DepartmentId = deptMap["ENT"], Name = "Nội soi Tai Mũi Họng ống mềm", Price = 200000, Description = "Nội soi tầm soát bệnh lý tai mũi họng", IsActive = true },
                new MedicalService { DepartmentId = deptMap["CAR"], Name = "Điện tâm đồ ECG 12 chuyển đạo", Price = 100000, Description = "Tầm soát rối loạn nhịp tim và thiếu máu cơ tim", IsActive = true },
                new MedicalService { DepartmentId = deptMap["CAR"], Name = "Siêu âm Doppler Tim", Price = 350000, Description = "Khảo sát cấu trúc van tim và huyết động học", IsActive = true },
                new MedicalService { DepartmentId = deptMap["DER"], Name = "Soi tươi & Nuôi cấy nấm da", Price = 150000, Description = "Tìm vi nấm gây bệnh lý ngoài da", IsActive = true },
                new MedicalService { DepartmentId = deptMap["OPH"], Name = "Đo khúc xạ máy & Soi đáy mắt", Price = 120000, Description = "Kiểm tra tật khúc xạ và võng mạc", IsActive = true }
            };

            foreach (var srv in servicesToSeed)
            {
                if (!context.MedicalServices.Any(s => s.Name == srv.Name))
                {
                    context.MedicalServices.Add(srv);
                }
            }
            context.SaveChanges();
        }

        /// <summary>
        /// Tạo dữ liệu lịch trực và các cuộc hẹn mẫu hôm nay trải dài qua các trạng thái:
        /// Confirmed (chờ check-in), Arrived (đã vào sảnh chờ khám), InConsultation (đang khám), Completed (đã xong, có đơn thuốc & thanh toán)
        /// </summary>
        public static void EnsureTodayDemoData(OcmsDbContext context)
        {
            var today = DateTime.Today;

            // Lấy danh sách bác sĩ
            var doctors = context.Doctors.Include(d => d.User).Include(d => d.DefaultRoom).ToList();
            if (doctors.Count == 0) return;

            var docAn = doctors.FirstOrDefault(d => d.User.Email == "doctor.an@ocms.clinic") ?? doctors[0];
            var docHa = doctors.FirstOrDefault(d => d.User.Email == "doctor.ha@ocms.clinic") ?? (doctors.Count > 1 ? doctors[1] : docAn);
            var docLong = doctors.FirstOrDefault(d => d.User.Email == "doctor.long@ocms.clinic") ?? docAn;

            var room101 = context.Rooms.FirstOrDefault(r => r.RoomNumber == "101") ?? context.Rooms.First();
            var room102 = context.Rooms.FirstOrDefault(r => r.RoomNumber == "102") ?? room101;
            var room201 = context.Rooms.FirstOrDefault(r => r.RoomNumber == "201") ?? room101;

            // 1. Đảm bảo lịch trực sáng hôm nay cho BS An, BS Hà, BS Long
            var doctorWorkList = new List<(Doctor Doc, Room Rm)>
            {
                (docAn, room101),
                (docHa, room102),
                (docLong, room201)
            };

            foreach (var (doc, rm) in doctorWorkList)
            {
                var schedule = context.DoctorSchedules
                    .Include(s => s.TimeSlots)
                    .FirstOrDefault(s => s.DoctorId == doc.Id && s.WorkDate.Date == today && s.ShiftType == "Morning");

                if (schedule == null)
                {
                    schedule = new DoctorSchedule
                    {
                        DoctorId = doc.Id,
                        RoomId = rm.Id,
                        WorkDate = today,
                        ShiftType = "Morning",
                        MaxQuota = 16,
                        Status = "Active"
                    };
                    context.DoctorSchedules.Add(schedule);
                    context.SaveChanges();
                }

                // Nếu chưa có time slots cho ca trực này thì tạo 8 khung giờ (08:00 đến 12:00)
                if (!context.TimeSlots.Any(t => t.ScheduleId == schedule.Id))
                {
                    var baseHours = new (int StartH, int StartM, int EndH, int EndM)[]
                    {
                        (8, 0, 8, 30),
                        (8, 30, 9, 0),
                        (9, 0, 9, 30),
                        (9, 30, 10, 0),
                        (10, 0, 10, 30),
                        (10, 30, 11, 0),
                        (11, 0, 11, 30),
                        (11, 30, 12, 0)
                    };

                    var slots = baseHours.Select(h => new TimeSlot
                    {
                        ScheduleId = schedule.Id,
                        StartTime = new TimeSpan(h.StartH, h.StartM, 0),
                        EndTime = new TimeSpan(h.EndH, h.EndM, 0),
                        Status = "Open"
                    }).ToList();

                    context.TimeSlots.AddRange(slots);
                    context.SaveChanges();
                }
            }

            // 2. Lấy danh sách bệnh nhân
            var patients = context.Patients.Include(p => p.User).ToList();
            if (patients.Count < 5) return;

            var pat1 = patients.FirstOrDefault(p => p.User.Email == "patient1@ocms.clinic") ?? patients[0];
            var pat2 = patients.FirstOrDefault(p => p.User.Email == "patient2@ocms.clinic") ?? (patients.Count > 1 ? patients[1] : pat1);
            var pat3 = patients.FirstOrDefault(p => p.User.Email == "patient3@ocms.clinic") ?? (patients.Count > 2 ? patients[2] : pat1);
            var pat4 = patients.FirstOrDefault(p => p.User.Email == "patient4@ocms.clinic") ?? (patients.Count > 3 ? patients[3] : pat1);
            var pat5 = patients.FirstOrDefault(p => p.User.Email == "patient5@ocms.clinic") ?? (patients.Count > 4 ? patients[4] : pat1);

            // 3. Nếu chưa có cuộc hẹn nào hôm nay thì seed trọn vẹn quy trình 5 ca khám thực tế
            if (!context.Appointments.Any(a => a.AppointmentDate.Date == today))
            {
                var schedAn = context.DoctorSchedules.FirstOrDefault(s => s.DoctorId == docAn.Id && s.WorkDate.Date == today);
                var schedHa = context.DoctorSchedules.FirstOrDefault(s => s.DoctorId == docHa.Id && s.WorkDate.Date == today);

                if (schedAn == null || schedHa == null) return;

                var slotsAn = context.TimeSlots.Where(t => t.ScheduleId == schedAn.Id).OrderBy(t => t.StartTime).ToList();
                var slotsHa = context.TimeSlots.Where(t => t.ScheduleId == schedHa.Id).OrderBy(t => t.StartTime).ToList();

                if (slotsAn.Count < 4 || slotsHa.Count < 1) return;

                // --- Ca 1: Confirmed (Chờ lễ tân quét QR hoặc nhập SĐT 0900000004 để check-in) ---
                slotsAn[0].Status = "Booked";
                var apt1 = new Appointment
                {
                    PatientId = pat1.Id,
                    DoctorId = docAn.Id,
                    TimeSlotId = slotsAn[0].Id,
                    AppointmentDate = today.Add(slotsAn[0].StartTime),
                    ConsultationFee = docAn.PricePerSlot,
                    Status = "Confirmed",
                    Reason = "Đau họng, sốt nhẹ 2 ngày nay",
                    QrCodeToken = "OCMS-APT-1001",
                    CreatedAt = DateTime.UtcNow
                };

                // --- Ca 2: Confirmed (Chờ check-in, test SĐT 0912345678) ---
                slotsAn[1].Status = "Booked";
                var apt2 = new Appointment
                {
                    PatientId = pat2.Id,
                    DoctorId = docAn.Id,
                    TimeSlotId = slotsAn[1].Id,
                    AppointmentDate = today.Add(slotsAn[1].StartTime),
                    ConsultationFee = docAn.PricePerSlot,
                    Status = "Confirmed",
                    Reason = "Khám sức khỏe định kỳ & đau nửa đầu",
                    QrCodeToken = "OCMS-APT-1002",
                    CreatedAt = DateTime.UtcNow
                };

                // --- Ca 3: Arrived (Đã check-in thành công, số thứ tự khám #1 -> Đang chờ trên màn hình TV & BS An gọi vào khám) ---
                slotsAn[2].Status = "Booked";
                var apt3 = new Appointment
                {
                    PatientId = pat3.Id,
                    DoctorId = docAn.Id,
                    TimeSlotId = slotsAn[2].Id,
                    AppointmentDate = today.Add(slotsAn[2].StartTime),
                    ConsultationFee = docAn.PricePerSlot,
                    Status = "Arrived",
                    QueueNumber = 1,
                    Reason = "Tái khám huyết áp & chóng mặt",
                    QrCodeToken = "OCMS-APT-1003",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // --- Ca 4: InConsultation (Đang trong phòng khám với BS Hà khoa Nhi) ---
                slotsHa[0].Status = "Booked";
                var apt4 = new Appointment
                {
                    PatientId = pat4.Id,
                    DoctorId = docHa.Id,
                    TimeSlotId = slotsHa[0].Id,
                    AppointmentDate = today.Add(slotsHa[0].StartTime),
                    ConsultationFee = docHa.PricePerSlot,
                    Status = "InConsultation",
                    QueueNumber = 1,
                    Reason = "Sốt phát ban, ho có đờm",
                    QrCodeToken = "OCMS-APT-1004",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                // --- Ca 5: Completed (Đã hoàn tất khám, có bệnh án ICD-10 và đơn thuốc -> Chờ thu ngân & phát thuốc) ---
                slotsAn[3].Status = "Booked";
                var apt5 = new Appointment
                {
                    PatientId = pat5.Id,
                    DoctorId = docAn.Id,
                    TimeSlotId = slotsAn[3].Id,
                    AppointmentDate = today.Add(slotsAn[3].StartTime),
                    ConsultationFee = docAn.PricePerSlot,
                    Status = "Completed",
                    QueueNumber = 2,
                    Reason = "Đau rát họng kéo dài, khàn tiếng",
                    QrCodeToken = "OCMS-APT-1005",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                context.Appointments.AddRange(apt1, apt2, apt3, apt4, apt5);
                context.SaveChanges();

                // Nạp tiền đặt cọc (Deposit) cho các cuộc hẹn
                context.Payments.AddRange(
                    new Payment
                    {
                        AppointmentId = apt1.Id,
                        Amount = apt1.ConsultationFee ?? 150000,
                        PaymentMethod = "POS",
                        PaymentStatus = "Success",
                        PaymentType = "Deposit",
                        TransactionId = "POS-SEED-1001",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Payment
                    {
                        AppointmentId = apt2.Id,
                        Amount = apt2.ConsultationFee ?? 150000,
                        PaymentMethod = "Cash",
                        PaymentStatus = "Success",
                        PaymentType = "Deposit",
                        TransactionId = "CASH-SEED-1002",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Payment
                    {
                        AppointmentId = apt3.Id,
                        Amount = apt3.ConsultationFee ?? 150000,
                        PaymentMethod = "Cash",
                        PaymentStatus = "Success",
                        PaymentType = "Deposit",
                        TransactionId = "CASH-SEED-1003",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Payment
                    {
                        AppointmentId = apt4.Id,
                        Amount = apt4.ConsultationFee ?? 180000,
                        PaymentMethod = "POS",
                        PaymentStatus = "Success",
                        PaymentType = "Deposit",
                        TransactionId = "POS-SEED-1004",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Payment
                    {
                        AppointmentId = apt5.Id,
                        Amount = apt5.ConsultationFee ?? 150000,
                        PaymentMethod = "Cash",
                        PaymentStatus = "Success",
                        PaymentType = "Deposit",
                        TransactionId = "CASH-SEED-1005",
                        CreatedAt = DateTime.UtcNow
                    }
                );
                context.SaveChanges();

                // Nạp chỉ số sinh hiệu cho ca 3, 4, 5
                context.VitalSigns.AddRange(
                    new VitalSigns
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
                    },
                    new VitalSigns
                    {
                        AppointmentId = apt4.Id,
                        SystolicBP = 110,
                        DiastolicBP = 70,
                        Pulse = 85,
                        SpO2 = 97,
                        Temperature = 38.2m,
                        Weight = 52.0m,
                        Height = 158.0m,
                        RecordedAt = DateTime.UtcNow
                    },
                    new VitalSigns
                    {
                        AppointmentId = apt5.Id,
                        SystolicBP = 120,
                        DiastolicBP = 80,
                        Pulse = 72,
                        SpO2 = 99,
                        Temperature = 37.0m,
                        Weight = 72.0m,
                        Height = 168.0m,
                        RecordedAt = DateTime.UtcNow
                    }
                );
                context.SaveChanges();

                // Nạp Bệnh án & Đơn thuốc cho Ca 5 (Completed)
                var medRecord5 = new MedicalRecord
                {
                    AppointmentId = apt5.Id,
                    Diagnosis = "Viêm họng cấp tính do vi khuẩn",
                    ICD10Code = "J02.9",
                    DoctorNotes = "Bệnh nhân cần nghỉ ngơi, uống nhiều nước ấm, súc miệng nước muối sinh lý hàng ngày. Tái khám sau 7 ngày nếu còn triệu chứng.",
                    FollowUpDate = today.AddDays(7),
                    CreatedAt = DateTime.UtcNow
                };
                context.MedicalRecords.Add(medRecord5);
                context.SaveChanges();

                // Lấy thuốc từ kho để kê đơn mẫu
                var medPara = context.Medicines.FirstOrDefault(m => m.Name.Contains("Paracetamol"));
                var medAug = context.Medicines.FirstOrDefault(m => m.Name.Contains("Augmentin"));

                if (medPara != null && medAug != null)
                {
                    context.PrescriptionDetails.AddRange(
                        new PrescriptionDetail
                        {
                            MedicalRecordId = medRecord5.Id,
                            MedicineId = medPara.Id,
                            Quantity = 20,
                            UnitPrice = medPara.Price,
                            DosageInstruction = "Uống 1 viên khi sốt trên 38.5°C hoặc đau nhiều, cách nhau 4-6 tiếng.",
                            Status = "Pending"
                        },
                        new PrescriptionDetail
                        {
                            MedicalRecordId = medRecord5.Id,
                            MedicineId = medAug.Id,
                            Quantity = 14,
                            UnitPrice = medAug.Price,
                            DosageInstruction = "Uống 1 viên sau ăn sáng và 1 viên sau ăn tối, uống liên tục 7 ngày.",
                            Status = "Pending"
                        }
                    );
                    context.SaveChanges();
                }
            }
        }

        /// <summary>
        /// Reset lại dữ liệu demo hôm nay để phục vụ test lại quy trình khám từ đầu
        /// </summary>
        public static void ResetDemoData(OcmsDbContext context)
        {
            var today = DateTime.Today;

            var todayApts = context.Appointments
                .Include(a => a.VitalSigns)
                .Include(a => a.MedicalRecord)
                    .ThenInclude(m => m!.PrescriptionDetails)
                .Include(a => a.Payments)
                .Include(a => a.ServiceOrders)
                .Include(a => a.Feedback)
                .Where(a => a.AppointmentDate.Date == today)
                .ToList();

            foreach (var a in todayApts)
            {
                if (a.VitalSigns != null) context.VitalSigns.Remove(a.VitalSigns);
                if (a.MedicalRecord != null)
                {
                    context.PrescriptionDetails.RemoveRange(a.MedicalRecord.PrescriptionDetails);
                    context.MedicalRecords.Remove(a.MedicalRecord);
                }
                if (a.Payments.Any()) context.Payments.RemoveRange(a.Payments);
                if (a.ServiceOrders.Any()) context.ServiceOrders.RemoveRange(a.ServiceOrders);
                if (a.Feedback != null) context.Feedbacks.Remove(a.Feedback);
            }
            context.Appointments.RemoveRange(todayApts);
            context.SaveChanges();

            // Mở lại toàn bộ các TimeSlots hôm nay về trạng thái "Open"
            var todaySlots = context.TimeSlots
                .Include(t => t.DoctorSchedule)
                .Where(t => t.DoctorSchedule.WorkDate.Date == today)
                .ToList();

            foreach (var s in todaySlots)
            {
                s.Status = "Open";
            }
            context.SaveChanges();
        }

        private static void AdoptEnsureCreatedDatabase(OcmsDbContext context)
        {
            try
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
            catch
            {
                // Bỏ qua nếu bảng đã tồn tại
            }
        }
    }
}
