-- ==============================================================================
-- HỆ THỐNG PHÒNG KHÁM NGOẠI TRÚ (OCMS) - CƠ SỞ DỮ LIỆU & BỘ DỮ LIỆU MẪU (SEED DATA)
-- Hệ quản trị CSDL: Microsoft SQL Server (T-SQL)
-- Mật khẩu mặc định của TẤT CẢ tài khoản test: 123456
-- ==============================================================================
-- BẢNG TRA CỨU TÀI KHOẢN MẪU DÙNG CHO CÁC THÀNH VIÊN TRONG NHÓM TEST TỪNG VAI TRÒ:
-- +----------------------------+--------------+-----------+----------+------------------------------------------------------+
-- | Email                      | Số điện thoại| Vai trò   | Mật khẩu | Mục đích kiểm thử (Use Case)                         |
-- +----------------------------+--------------+-----------+----------+------------------------------------------------------+
-- | admin@ocms.clinic          | 0900000001   | Admin     | 123456   | Quản trị hệ thống, thêm sửa khoa/phòng/tài khoản    |
-- | staff@ocms.clinic          | 0900000002   | Staff     | 123456   | Tiếp đón, quét QR check-in, phân số khám, đo sinh hiệu|
-- | cashier@ocms.clinic        | 0900000005   | Staff     | 123456   | Thu ngân quầy: Tất toán hóa đơn, thanh toán thuốc/CLS|
-- | pharmacist@ocms.clinic     | 0900000006   | Staff     | 123456   | Dược sĩ nhà thuốc: Tiếp nhận đơn thuốc và phát thuốc |
-- | doctor.an@ocms.clinic      | 0900000003   | Doctor    | 123456   | BS Nội P101: Gọi khám, khám bệnh, kê đơn, kết luận EMR|
-- | doctor.ha@ocms.clinic      | 0900000007   | Doctor    | 123456   | BS Nhi P102: Khám nhi, chẩn đoán, kê đơn             |
-- | doctor.long@ocms.clinic    | 0900000008   | Doctor    | 123456   | BS Tai Mũi Họng P201: Khám & chỉ định nội soi TMH   |
-- | doctor.tuan@ocms.clinic    | 0900000009   | Doctor    | 123456   | PGS Tim Mạch P202: Khám tim mạch & đo điện tim/siêu âm|
-- | doctor.phuong@ocms.clinic  | 0900000010   | Doctor    | 123456   | BS Da Liễu P203: Khám da liễu & soi tươi nấm da     |
-- | patient1@ocms.clinic       | 0900000004   | Patient   | 123456   | Ca Confirmed: Test check-in quét QR hoặc nhập SĐT    |
-- | patient2@ocms.clinic       | 0912345678   | Patient   | 123456   | Ca Confirmed: Test check-in nữ bệnh nhân             |
-- | patient3@ocms.clinic       | 0988776655   | Patient   | 123456   | Ca Arrived: Số #1 sảnh chờ -> Test BS bấm "Gọi khám" |
-- | patient4@ocms.clinic       | 0933445566   | Patient   | 123456   | Ca InConsultation: Đang khám tại P102 với BS Hà      |
-- | patient5@ocms.clinic       | 0977112233   | Patient   | 123456   | Ca Completed: Có đơn thuốc -> Test Thu ngân & Nhà thuốc|
-- +----------------------------+--------------+-----------+----------+------------------------------------------------------+
-- ==============================================================================

USE master;
GO

-- 1. Tạo Database OCMS_Db nếu chưa tồn tại
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'OCMS_Db')
BEGIN
    CREATE DATABASE OCMS_Db;
END
GO

USE OCMS_Db;
GO

-- ==============================================================================
-- 2. TẠO CÁC BẢNG (TABLES) THEO ĐÚNG CHUẨN ENTITY TRONG SOURCE CODE
-- ==============================================================================

-- Bảng Khoa (Departments)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Departments')
BEGIN
    CREATE TABLE Departments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Code NVARCHAR(20) NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        BaseConsultationFee DECIMAL(18,2) NOT NULL DEFAULT 150000,
        CONSTRAINT UQ_Departments_Code UNIQUE (Code)
    );
END
GO

-- Bảng Phòng khám (Rooms)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Rooms')
BEGIN
    CREATE TABLE Rooms (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        RoomNumber NVARCHAR(20) NOT NULL,
        DepartmentId INT NOT NULL,
        RoomType NVARCHAR(50) NOT NULL DEFAULT 'Consultation',
        IsOperational BIT NOT NULL DEFAULT 1,
        CONSTRAINT UQ_Rooms_RoomNumber UNIQUE (RoomNumber),
        CONSTRAINT FK_Rooms_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
    );
END
GO

-- Bảng Người dùng hệ thống (Users)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FullName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        PhoneNumber NVARCHAR(20) NOT NULL,
        PasswordHash NVARCHAR(MAX) NOT NULL,
        Role NVARCHAR(20) NOT NULL DEFAULT 'Patient', -- Admin, Staff, Doctor, Patient
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT UQ_Users_PhoneNumber UNIQUE (PhoneNumber)
    );
END
GO

-- Bảng Hồ sơ Bệnh nhân (Patients) - Quan hệ 1-1 với Users
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Patients')
BEGIN
    CREATE TABLE Patients (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        DateOfBirth DATETIME2 NOT NULL,
        Gender NVARCHAR(10) NOT NULL DEFAULT 'Other',
        Address NVARCHAR(255) NULL,
        HealthInsuranceNumber NVARCHAR(50) NULL,
        CONSTRAINT UQ_Patients_UserId UNIQUE (UserId),
        CONSTRAINT FK_Patients_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- Bảng Bác sĩ (Doctors) - Quan hệ 1-1 với Users
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Doctors')
BEGIN
    CREATE TABLE Doctors (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        DepartmentId INT NOT NULL,
        DefaultRoomId INT NULL,
        AcademicTitle NVARCHAR(50) NOT NULL DEFAULT 'BS',
        PricePerSlot DECIMAL(18,2) NOT NULL DEFAULT 150000,
        CONSTRAINT UQ_Doctors_UserId UNIQUE (UserId),
        CONSTRAINT FK_Doctors_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
        CONSTRAINT FK_Doctors_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
        CONSTRAINT FK_Doctors_Rooms FOREIGN KEY (DefaultRoomId) REFERENCES Rooms(Id)
    );
END
GO

-- Bảng Lịch làm việc Bác sĩ theo ca (DoctorSchedules)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorSchedules')
BEGIN
    CREATE TABLE DoctorSchedules (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DoctorId INT NOT NULL,
        RoomId INT NOT NULL,
        WorkDate DATETIME2 NOT NULL,
        ShiftType NVARCHAR(20) NOT NULL DEFAULT 'Morning',
        MaxQuota INT NOT NULL DEFAULT 16,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
        CONSTRAINT FK_DoctorSchedules_Doctors FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
        CONSTRAINT FK_DoctorSchedules_Rooms FOREIGN KEY (RoomId) REFERENCES Rooms(Id)
    );
END
GO

-- Bảng Khung giờ khám (TimeSlots)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TimeSlots')
BEGIN
    CREATE TABLE TimeSlots (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ScheduleId INT NOT NULL,
        StartTime TIME NOT NULL,
        EndTime TIME NOT NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Open',
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_TimeSlots_DoctorSchedules FOREIGN KEY (ScheduleId) REFERENCES DoctorSchedules(Id)
    );
END
GO

-- Bảng Cuộc hẹn khám (Appointments)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Appointments')
BEGIN
    CREATE TABLE Appointments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PatientId INT NOT NULL,
        DoctorId INT NOT NULL,
        TimeSlotId INT NOT NULL,
        AppointmentDate DATETIME2 NOT NULL,
        ConsultationFee DECIMAL(18,2) NULL,
        Status NVARCHAR(30) NOT NULL DEFAULT 'Pending',
        Reason NVARCHAR(500) NULL,
        CancellationReason NVARCHAR(500) NULL,
        QrCodeToken NVARCHAR(100) NULL,
        QueueNumber INT NULL,
        RescheduledCount INT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT FK_Appointments_Patients FOREIGN KEY (PatientId) REFERENCES Patients(Id),
        CONSTRAINT FK_Appointments_Doctors FOREIGN KEY (DoctorId) REFERENCES Doctors(Id),
        CONSTRAINT FK_Appointments_TimeSlots FOREIGN KEY (TimeSlotId) REFERENCES TimeSlots(Id)
    );
END
GO

-- Bảng Chỉ số sinh hiệu (VitalSigns) - Quan hệ 1-1 với Appointments
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VitalSigns')
BEGIN
    CREATE TABLE VitalSigns (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        SystolicBP INT NOT NULL,
        DiastolicBP INT NOT NULL,
        Pulse INT NOT NULL,
        Temperature DECIMAL(4,1) NOT NULL,
        SpO2 INT NOT NULL,
        Weight DECIMAL(5,2) NOT NULL,
        Height DECIMAL(5,2) NOT NULL,
        RecordedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_VitalSigns_AppointmentId UNIQUE (AppointmentId),
        CONSTRAINT FK_VitalSigns_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id)
    );
END
GO

-- Bảng Hồ sơ bệnh án điện tử (MedicalRecords) - Quan hệ 1-1 với Appointments
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicalRecords')
BEGIN
    CREATE TABLE MedicalRecords (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        Diagnosis NVARCHAR(500) NOT NULL,
        ICD10Code NVARCHAR(20) NULL,
        DoctorNotes NVARCHAR(1000) NULL,
        FollowUpDate DATETIME2 NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_MedicalRecords_AppointmentId UNIQUE (AppointmentId),
        CONSTRAINT FK_MedicalRecords_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id)
    );
END
GO

-- Bảng Danh mục Thuốc (Medicines)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Medicines')
BEGIN
    CREATE TABLE Medicines (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Unit NVARCHAR(20) NOT NULL DEFAULT N'Viên',
        Price DECIMAL(18,2) NOT NULL DEFAULT 0,
        StockQuantity INT NOT NULL DEFAULT 100,
        Description NVARCHAR(255) NULL,
        IsAvailable BIT NOT NULL DEFAULT 1
    );
END
GO

-- Bảng Chi tiết đơn thuốc (PrescriptionDetails)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PrescriptionDetails')
BEGIN
    CREATE TABLE PrescriptionDetails (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MedicalRecordId INT NOT NULL,
        MedicineId INT NOT NULL,
        Quantity INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        DosageInstruction NVARCHAR(255) NOT NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        DispensedAt DATETIME2 NULL,
        DispensedByUserId INT NULL,
        CONSTRAINT FK_PrescriptionDetails_MedicalRecords FOREIGN KEY (MedicalRecordId) REFERENCES MedicalRecords(Id),
        CONSTRAINT FK_PrescriptionDetails_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines(Id),
        CONSTRAINT FK_PrescriptionDetails_Users FOREIGN KEY (DispensedByUserId) REFERENCES Users(Id)
    );
END
GO

-- Bảng Dịch vụ Y tế / Cận lâm sàng (MedicalServices)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicalServices')
BEGIN
    CREATE TABLE MedicalServices (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DepartmentId INT NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        Description NVARCHAR(255) NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_MedicalServices_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
    );
END
GO

-- Bảng Chỉ định dịch vụ cận lâm sàng (ServiceOrders)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ServiceOrders')
BEGIN
    CREATE TABLE ServiceOrders (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        ServiceId INT NOT NULL,
        UnitPrice DECIMAL(18,2) NOT NULL,
        ResultSummary NVARCHAR(500) NULL,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Ordered',
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_ServiceOrders_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id),
        CONSTRAINT FK_ServiceOrders_MedicalServices FOREIGN KEY (ServiceId) REFERENCES MedicalServices(Id)
    );
END
GO

-- Bảng Thanh toán (Payments)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        PaymentMethod NVARCHAR(20) NOT NULL DEFAULT 'Cash',
        PaymentStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending',
        PaymentType NVARCHAR(30) NOT NULL DEFAULT 'Deposit',
        TransactionId NVARCHAR(100) NULL,
        CashierId INT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Payments_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id),
        CONSTRAINT FK_Payments_Users FOREIGN KEY (CashierId) REFERENCES Users(Id)
    );
END
GO

-- Bảng Đánh giá của bệnh nhân (Feedbacks)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Feedbacks')
BEGIN
    CREATE TABLE Feedbacks (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        AppointmentId INT NOT NULL,
        PatientId INT NOT NULL,
        DoctorId INT NOT NULL,
        Rating INT NOT NULL DEFAULT 5,
        Comment NVARCHAR(1000) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_Feedbacks_AppointmentId UNIQUE (AppointmentId),
        CONSTRAINT FK_Feedbacks_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id),
        CONSTRAINT FK_Feedbacks_Patients FOREIGN KEY (PatientId) REFERENCES Patients(Id),
        CONSTRAINT FK_Feedbacks_Doctors FOREIGN KEY (DoctorId) REFERENCES Doctors(Id)
    );
END
GO

-- Bảng Thông báo (Notifications)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
BEGIN
    CREATE TABLE Notifications (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        Message NVARCHAR(500) NOT NULL,
        NotificationType NVARCHAR(30) NOT NULL DEFAULT 'Reminder',
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- ==============================================================================
-- 3. AUTO PATCH: ĐẢM BẢO CÁC CỘT MỚI TỒN TẠI (NẾU CHẠY TRÊN DATABASE CŨ)
-- ==============================================================================

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

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Payments' AND COLUMN_NAME = 'CashierId')
BEGIN
    ALTER TABLE Payments ADD CashierId INT NULL;
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
        ALTER TABLE Payments ADD CONSTRAINT FK_Payments_Users FOREIGN KEY (CashierId) REFERENCES Users(Id);
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PrescriptionDetails' AND COLUMN_NAME = 'DispensedAt')
    ALTER TABLE PrescriptionDetails ADD DispensedAt DATETIME2 NULL;

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PrescriptionDetails' AND COLUMN_NAME = 'DispensedByUserId')
BEGIN
    ALTER TABLE PrescriptionDetails ADD DispensedByUserId INT NULL;
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
        ALTER TABLE PrescriptionDetails ADD CONSTRAINT FK_PrescriptionDetails_Users FOREIGN KEY (DispensedByUserId) REFERENCES Users(Id);
END
GO

-- ==============================================================================
-- 4. NẠP DỮ LIỆU NỀN TẢNG (MASTER DATA) VÀ DỮ LIỆU KIỂM THỬ THỰC TẾ
-- Mật khẩu mặc định: 123456 (BCrypt hash)
-- ==============================================================================

DECLARE @defaultHash NVARCHAR(MAX) = N'$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi';

-- 4.1. Chuyên khoa (Departments)
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'INT')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'INT', N'Khoa Nội Tổng Quát', N'Khám và điều trị các bệnh nội khoa tổng quát', 150000);
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'PED')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'PED', N'Khoa Nhi', N'Khám và chăm sóc sức khỏe toàn diện cho trẻ em', 150000);
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'ENT')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'ENT', N'Khoa Tai Mũi Họng', N'Khám, nội soi và điều trị các bệnh lý tai mũi họng', 200000);
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'CAR')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'CAR', N'Khoa Tim Mạch', N'Chẩn đoán, theo dõi và điều trị bệnh lý tim mạch, huyết áp', 250000);
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'DER')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'DER', N'Khoa Da Liễu', N'Khám và điều trị chuyên sâu các bệnh lý về da, thẩm mỹ da', 180000);
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Code = 'OPH')
    INSERT INTO Departments (Code, Name, Description, BaseConsultationFee) VALUES (N'OPH', N'Khoa Mắt', N'Khám mắt, đo thị lực, tầm soát các bệnh lý về mắt', 180000);
GO

-- 4.2. Phòng khám (Rooms)
DECLARE @idInt INT = (SELECT Id FROM Departments WHERE Code = 'INT');
DECLARE @idPed INT = (SELECT Id FROM Departments WHERE Code = 'PED');
DECLARE @idEnt INT = (SELECT Id FROM Departments WHERE Code = 'ENT');
DECLARE @idCar INT = (SELECT Id FROM Departments WHERE Code = 'CAR');
DECLARE @idDer INT = (SELECT Id FROM Departments WHERE Code = 'DER');
DECLARE @idOph INT = (SELECT Id FROM Departments WHERE Code = 'OPH');

IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '101') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'101', @idInt, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '102') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'102', @idPed, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '103') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'103', @idInt, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '104') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'104', @idPed, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '201') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'201', @idEnt, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '202') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'202', @idCar, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '203') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'203', @idDer, N'Consultation', 1);
IF NOT EXISTS (SELECT 1 FROM Rooms WHERE RoomNumber = '204') INSERT INTO Rooms (RoomNumber, DepartmentId, RoomType, IsOperational) VALUES (N'204', @idOph, N'Consultation', 1);
GO

-- 4.3. Người dùng mẫu (Users) - Mật khẩu: 123456
DECLARE @defaultHash NVARCHAR(MAX) = N'$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi';

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'System Administrator', N'admin@ocms.clinic', N'0900000001', @defaultHash, N'Admin', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'staff@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Lễ Tân Nguyễn Thị Mai', N'staff@ocms.clinic', N'0900000002', @defaultHash, N'Staff', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'cashier@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Thu Ngân Trần Thu Thảo', N'cashier@ocms.clinic', N'0900000005', @defaultHash, N'Staff', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'pharmacist@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Dược Sĩ Hoàng Hải Đăng', N'pharmacist@ocms.clinic', N'0900000006', @defaultHash, N'Staff', 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'doctor.an@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'BS. ThS. Nguyễn Văn An', N'doctor.an@ocms.clinic', N'0900000003', @defaultHash, N'Doctor', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'doctor.ha@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'BS. CKI. Trần Thu Hà', N'doctor.ha@ocms.clinic', N'0900000007', @defaultHash, N'Doctor', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'doctor.long@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'BS. CKII. Lê Hoàng Long', N'doctor.long@ocms.clinic', N'0900000008', @defaultHash, N'Doctor', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'doctor.tuan@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'PGS.TS. Phạm Minh Tuấn', N'doctor.tuan@ocms.clinic', N'0900000009', @defaultHash, N'Doctor', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'doctor.phuong@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'BS. ThS. Vũ Mai Phương', N'doctor.phuong@ocms.clinic', N'0900000010', @defaultHash, N'Doctor', 1, GETUTCDATE());

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'patient1@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Trần Quang Phong', N'patient1@ocms.clinic', N'0900000004', @defaultHash, N'Patient', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'patient2@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Lê Thị Mai', N'patient2@ocms.clinic', N'0912345678', @defaultHash, N'Patient', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'patient3@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Hoàng Minh Đức', N'patient3@ocms.clinic', N'0988776655', @defaultHash, N'Patient', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'patient4@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Nguyễn Thị Lan Anh', N'patient4@ocms.clinic', N'0933445566', @defaultHash, N'Patient', 1, GETUTCDATE());
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'patient5@ocms.clinic')
    INSERT INTO Users (FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES (N'Bùi Quốc Khánh', N'patient5@ocms.clinic', N'0977112233', @defaultHash, N'Patient', 1, GETUTCDATE());
GO

-- 4.4. Hồ sơ Bác sĩ (Doctors)
DECLARE @uDocAn INT = (SELECT Id FROM Users WHERE Email = 'doctor.an@ocms.clinic');
DECLARE @uDocHa INT = (SELECT Id FROM Users WHERE Email = 'doctor.ha@ocms.clinic');
DECLARE @uDocLong INT = (SELECT Id FROM Users WHERE Email = 'doctor.long@ocms.clinic');
DECLARE @uDocTuan INT = (SELECT Id FROM Users WHERE Email = 'doctor.tuan@ocms.clinic');
DECLARE @uDocPhuong INT = (SELECT Id FROM Users WHERE Email = 'doctor.phuong@ocms.clinic');

DECLARE @dInt INT = (SELECT Id FROM Departments WHERE Code = 'INT');
DECLARE @dPed INT = (SELECT Id FROM Departments WHERE Code = 'PED');
DECLARE @dEnt INT = (SELECT Id FROM Departments WHERE Code = 'ENT');
DECLARE @dCar INT = (SELECT Id FROM Departments WHERE Code = 'CAR');
DECLARE @dDer INT = (SELECT Id FROM Departments WHERE Code = 'DER');

DECLARE @r101 INT = (SELECT Id FROM Rooms WHERE RoomNumber = '101');
DECLARE @r102 INT = (SELECT Id FROM Rooms WHERE RoomNumber = '102');
DECLARE @r201 INT = (SELECT Id FROM Rooms WHERE RoomNumber = '201');
DECLARE @r202 INT = (SELECT Id FROM Rooms WHERE RoomNumber = '202');
DECLARE @r203 INT = (SELECT Id FROM Rooms WHERE RoomNumber = '203');

IF NOT EXISTS (SELECT 1 FROM Doctors WHERE UserId = @uDocAn)
    INSERT INTO Doctors (UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES (@uDocAn, @dInt, @r101, N'ThS.BS', 150000);
IF NOT EXISTS (SELECT 1 FROM Doctors WHERE UserId = @uDocHa)
    INSERT INTO Doctors (UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES (@uDocHa, @dPed, @r102, N'BS.CKI', 180000);
IF NOT EXISTS (SELECT 1 FROM Doctors WHERE UserId = @uDocLong)
    INSERT INTO Doctors (UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES (@uDocLong, @dEnt, @r201, N'BS.CKII', 200000);
IF NOT EXISTS (SELECT 1 FROM Doctors WHERE UserId = @uDocTuan)
    INSERT INTO Doctors (UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES (@uDocTuan, @dCar, @r202, N'PGS.TS', 250000);
IF NOT EXISTS (SELECT 1 FROM Doctors WHERE UserId = @uDocPhuong)
    INSERT INTO Doctors (UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES (@uDocPhuong, @dDer, @r203, N'ThS.BS', 180000);
GO

-- 4.5. Hồ sơ Bệnh nhân (Patients)
DECLARE @uPat1 INT = (SELECT Id FROM Users WHERE Email = 'patient1@ocms.clinic');
DECLARE @uPat2 INT = (SELECT Id FROM Users WHERE Email = 'patient2@ocms.clinic');
DECLARE @uPat3 INT = (SELECT Id FROM Users WHERE Email = 'patient3@ocms.clinic');
DECLARE @uPat4 INT = (SELECT Id FROM Users WHERE Email = 'patient4@ocms.clinic');
DECLARE @uPat5 INT = (SELECT Id FROM Users WHERE Email = 'patient5@ocms.clinic');

IF NOT EXISTS (SELECT 1 FROM Patients WHERE UserId = @uPat1)
    INSERT INTO Patients (UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES (@uPat1, '2000-01-01', N'Male', N'Hà Nội', N'DN4010123456789');
IF NOT EXISTS (SELECT 1 FROM Patients WHERE UserId = @uPat2)
    INSERT INTO Patients (UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES (@uPat2, '1995-05-12', N'Female', N'Cầu Giấy, Hà Nội', N'DN4010998877665');
IF NOT EXISTS (SELECT 1 FROM Patients WHERE UserId = @uPat3)
    INSERT INTO Patients (UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES (@uPat3, '1988-10-20', N'Male', N'Ba Đình, Hà Nội', N'DN4010112233445');
IF NOT EXISTS (SELECT 1 FROM Patients WHERE UserId = @uPat4)
    INSERT INTO Patients (UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES (@uPat4, '2002-08-15', N'Female', N'Đống Đa, Hà Nội', N'DN4010223344556');
IF NOT EXISTS (SELECT 1 FROM Patients WHERE UserId = @uPat5)
    INSERT INTO Patients (UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES (@uPat5, '1975-03-30', N'Male', N'Nam Từ Liêm, Hà Nội', N'DN4010334455667');
GO

-- 4.6. Danh mục Thuốc trong kho (Medicines)
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Paracetamol 500mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Paracetamol 500mg', N'Viên', 2000, 1000, N'Thuốc giảm đau, hạ sốt', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Amoxicillin 500mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Amoxicillin 500mg', N'Viên', 5000, 500, N'Kháng sinh nhóm Penicillin trị nhiễm khuẩn', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Augmentin 1g')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Augmentin 1g', N'Viên', 18000, 300, N'Kháng sinh phổ rộng Amoxicillin + Acid Clavulanic', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Decolgen Forte')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Decolgen Forte', N'Vỉ', 15000, 200, N'Giảm cảm cúm, nghẹt mũi, nhức đầu', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Berocca Effervescent')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Berocca Effervescent', N'Tuýp', 85000, 150, N'Viên sủi bổ sung vitamin B, C và khoáng chất', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Gaviscon Dual Action')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Gaviscon Dual Action', N'Gói', 12000, 400, N'Giảm triệu chứng trào ngược dạ dày thực quản', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Cefuroxime 500mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Cefuroxime 500mg', N'Viên', 9500, 350, N'Kháng sinh Cephalosporin thế hệ 2', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Panadol Extra')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Panadol Extra', N'Vỉ', 18000, 300, N'Giảm đau mạnh chứa Paracetamol & Caffeine', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Omeprazole 20mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Omeprazole 20mg', N'Viên', 3500, 600, N'Thuốc ức chế bơm proton trị viêm loét dạ dày', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Loratadine 10mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Loratadine 10mg', N'Viên', 2500, 400, N'Thuốc kháng histamin giảm dị ứng, mề đay', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Ibuprofen 400mg')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Ibuprofen 400mg', N'Viên', 4000, 500, N'Thuốc kháng viêm không steroid (NSAID)', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Alphachymotrypsin')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Alphachymotrypsin', N'Vỉ', 25000, 250, N'Thuốc kháng phù nề dạng men sau chấn thương', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Natri Clorid 0.9% 500ml')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Natri Clorid 0.9% 500ml', N'Chai', 10000, 200, N'Dung dịch nước muối sinh lý sát khuẩn', 1);
IF NOT EXISTS (SELECT 1 FROM Medicines WHERE Name = N'Oresol 245')
    INSERT INTO Medicines (Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES (N'Oresol 245', N'Gói', 5000, 500, N'Bột pha bù nước và điện giải khi sốt hoặc tiêu chảy', 1);
GO

-- 4.7. Danh mục Dịch vụ Cận lâm sàng (MedicalServices)
DECLARE @dIntId INT = (SELECT Id FROM Departments WHERE Code = 'INT');
DECLARE @dEntId INT = (SELECT Id FROM Departments WHERE Code = 'ENT');
DECLARE @dCarId INT = (SELECT Id FROM Departments WHERE Code = 'CAR');
DECLARE @dDerId INT = (SELECT Id FROM Departments WHERE Code = 'DER');
DECLARE @dOphId INT = (SELECT Id FROM Departments WHERE Code = 'OPH');

IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Xét nghiệm công thức máu toàn phần')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dIntId, N'Xét nghiệm công thức máu toàn phần', 120000, N'Đánh giá các tế bào máu cơ bản', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Siêu âm ổ bụng tổng quát')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dIntId, N'Siêu âm ổ bụng tổng quát', 180000, N'Khảo sát hình ảnh gan, mật, thận, lách, tụy', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Chụp X-Quang ngực thẳng')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dIntId, N'Chụp X-Quang ngực thẳng', 150000, N'Khảo sát tim phổi và lồng ngực', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Nội soi Tai Mũi Họng ống mềm')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dEntId, N'Nội soi Tai Mũi Họng ống mềm', 200000, N'Nội soi tầm soát bệnh lý tai mũi họng', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Điện tâm đồ ECG 12 chuyển đạo')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dCarId, N'Điện tâm đồ ECG 12 chuyển đạo', 100000, N'Tầm soát rối loạn nhịp tim và thiếu máu cơ tim', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Siêu âm Doppler Tim')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dCarId, N'Siêu âm Doppler Tim', 350000, N'Khảo sát cấu trúc van tim và huyết động học', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Soi tươi & Nuôi cấy nấm da')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dDerId, N'Soi tươi & Nuôi cấy nấm da', 150000, N'Tìm vi nấm gây bệnh lý ngoài da', 1);
IF NOT EXISTS (SELECT 1 FROM MedicalServices WHERE Name = N'Đo khúc xạ máy & Soi đáy mắt')
    INSERT INTO MedicalServices (DepartmentId, Name, Price, Description, IsActive) VALUES (@dOphId, N'Đo khúc xạ máy & Soi đáy mắt', 120000, N'Kiểm tra tật khúc xạ và võng mạc', 1);
GO

-- 4.8. Lịch làm việc hôm nay của Bác sĩ (DoctorSchedules) & Khung giờ khám (TimeSlots)
DECLARE @today DATE = CAST(GETDATE() AS DATE);
DECLARE @docAnId INT = (SELECT Id FROM Doctors WHERE UserId = (SELECT Id FROM Users WHERE Email = 'doctor.an@ocms.clinic'));
DECLARE @docHaId INT = (SELECT Id FROM Doctors WHERE UserId = (SELECT Id FROM Users WHERE Email = 'doctor.ha@ocms.clinic'));
DECLARE @docLongId INT = (SELECT Id FROM Doctors WHERE UserId = (SELECT Id FROM Users WHERE Email = 'doctor.long@ocms.clinic'));

DECLARE @r101Id INT = (SELECT Id FROM Rooms WHERE RoomNumber = '101');
DECLARE @r102Id INT = (SELECT Id FROM Rooms WHERE RoomNumber = '102');
DECLARE @r201Id INT = (SELECT Id FROM Rooms WHERE RoomNumber = '201');

-- Lịch trực BS An (P101)
IF NOT EXISTS (SELECT 1 FROM DoctorSchedules WHERE DoctorId = @docAnId AND WorkDate = @today AND ShiftType = 'Morning')
    INSERT INTO DoctorSchedules (DoctorId, RoomId, WorkDate, ShiftType, MaxQuota, Status) VALUES (@docAnId, @r101Id, @today, N'Morning', 16, N'Active');

-- Lịch trực BS Hà (P102)
IF NOT EXISTS (SELECT 1 FROM DoctorSchedules WHERE DoctorId = @docHaId AND WorkDate = @today AND ShiftType = 'Morning')
    INSERT INTO DoctorSchedules (DoctorId, RoomId, WorkDate, ShiftType, MaxQuota, Status) VALUES (@docHaId, @r102Id, @today, N'Morning', 16, N'Active');

-- Lịch trực BS Long (P201)
IF NOT EXISTS (SELECT 1 FROM DoctorSchedules WHERE DoctorId = @docLongId AND WorkDate = @today AND ShiftType = 'Morning')
    INSERT INTO DoctorSchedules (DoctorId, RoomId, WorkDate, ShiftType, MaxQuota, Status) VALUES (@docLongId, @r201Id, @today, N'Morning', 16, N'Active');

DECLARE @schedAnId INT = (SELECT Id FROM DoctorSchedules WHERE DoctorId = @docAnId AND WorkDate = @today AND ShiftType = 'Morning');
DECLARE @schedHaId INT = (SELECT Id FROM DoctorSchedules WHERE DoctorId = @docHaId AND WorkDate = @today AND ShiftType = 'Morning');
DECLARE @schedLongId INT = (SELECT Id FROM DoctorSchedules WHERE DoctorId = @docLongId AND WorkDate = @today AND ShiftType = 'Morning');

-- Tạo 8 TimeSlots cho BS An
IF NOT EXISTS (SELECT 1 FROM TimeSlots WHERE ScheduleId = @schedAnId)
BEGIN
    INSERT INTO TimeSlots (ScheduleId, StartTime, EndTime, Status) VALUES
    (@schedAnId, '08:00:00', '08:30:00', N'Booked'),
    (@schedAnId, '08:30:00', '09:00:00', N'Booked'),
    (@schedAnId, '09:00:00', '09:30:00', N'Booked'),
    (@schedAnId, '09:30:00', '10:00:00', N'Booked'),
    (@schedAnId, '10:00:00', '10:30:00', N'Open'),
    (@schedAnId, '10:30:00', '11:00:00', N'Open'),
    (@schedAnId, '11:00:00', '11:30:00', N'Open'),
    (@schedAnId, '11:30:00', '12:00:00', N'Open');
END

-- Tạo 8 TimeSlots cho BS Hà
IF NOT EXISTS (SELECT 1 FROM TimeSlots WHERE ScheduleId = @schedHaId)
BEGIN
    INSERT INTO TimeSlots (ScheduleId, StartTime, EndTime, Status) VALUES
    (@schedHaId, '08:00:00', '08:30:00', N'Booked'),
    (@schedHaId, '08:30:00', '09:00:00', N'Open'),
    (@schedHaId, '09:00:00', '09:30:00', N'Open'),
    (@schedHaId, '09:30:00', '10:00:00', N'Open'),
    (@schedHaId, '10:00:00', '10:30:00', N'Open'),
    (@schedHaId, '10:30:00', '11:00:00', N'Open'),
    (@schedHaId, '11:00:00', '11:30:00', N'Open'),
    (@schedHaId, '11:30:00', '12:00:00', N'Open');
END

-- Tạo 8 TimeSlots cho BS Long
IF NOT EXISTS (SELECT 1 FROM TimeSlots WHERE ScheduleId = @schedLongId)
BEGIN
    INSERT INTO TimeSlots (ScheduleId, StartTime, EndTime, Status) VALUES
    (@schedLongId, '08:00:00', '08:30:00', N'Open'),
    (@schedLongId, '08:30:00', '09:00:00', N'Open'),
    (@schedLongId, '09:00:00', '09:30:00', N'Open'),
    (@schedLongId, '09:30:00', '10:00:00', N'Open'),
    (@schedLongId, '10:00:00', '10:30:00', N'Open'),
    (@schedLongId, '10:30:00', '11:00:00', N'Open'),
    (@schedLongId, '11:00:00', '11:30:00', N'Open'),
    (@schedLongId, '11:30:00', '12:00:00', N'Open');
END
GO

-- 4.9. Danh sách 5 Cuộc hẹn mẫu hôm nay bao quát trọn vẹn quy trình (Appointments)
DECLARE @todayDate DATE = CAST(GETDATE() AS DATE);
DECLARE @docAn INT = (SELECT Id FROM Doctors WHERE UserId = (SELECT Id FROM Users WHERE Email = 'doctor.an@ocms.clinic'));
DECLARE @docHa INT = (SELECT Id FROM Doctors WHERE UserId = (SELECT Id FROM Users WHERE Email = 'doctor.ha@ocms.clinic'));

DECLARE @schedAn INT = (SELECT Id FROM DoctorSchedules WHERE DoctorId = @docAn AND WorkDate = @todayDate AND ShiftType = 'Morning');
DECLARE @schedHa INT = (SELECT Id FROM DoctorSchedules WHERE DoctorId = @docHa AND WorkDate = @todayDate AND ShiftType = 'Morning');

DECLARE @slotAn1 INT = (SELECT TOP 1 Id FROM TimeSlots WHERE ScheduleId = @schedAn AND StartTime = '08:00:00');
DECLARE @slotAn2 INT = (SELECT TOP 1 Id FROM TimeSlots WHERE ScheduleId = @schedAn AND StartTime = '08:30:00');
DECLARE @slotAn3 INT = (SELECT TOP 1 Id FROM TimeSlots WHERE ScheduleId = @schedAn AND StartTime = '09:00:00');
DECLARE @slotAn4 INT = (SELECT TOP 1 Id FROM TimeSlots WHERE ScheduleId = @schedAn AND StartTime = '09:30:00');
DECLARE @slotHa1 INT = (SELECT TOP 1 Id FROM TimeSlots WHERE ScheduleId = @schedHa AND StartTime = '08:00:00');

DECLARE @p1 INT = (SELECT Id FROM Patients WHERE UserId = (SELECT Id FROM Users WHERE Email = 'patient1@ocms.clinic'));
DECLARE @p2 INT = (SELECT Id FROM Patients WHERE UserId = (SELECT Id FROM Users WHERE Email = 'patient2@ocms.clinic'));
DECLARE @p3 INT = (SELECT Id FROM Patients WHERE UserId = (SELECT Id FROM Users WHERE Email = 'patient3@ocms.clinic'));
DECLARE @p4 INT = (SELECT Id FROM Patients WHERE UserId = (SELECT Id FROM Users WHERE Email = 'patient4@ocms.clinic'));
DECLARE @p5 INT = (SELECT Id FROM Patients WHERE UserId = (SELECT Id FROM Users WHERE Email = 'patient5@ocms.clinic'));

IF NOT EXISTS (SELECT 1 FROM Appointments WHERE CAST(AppointmentDate AS DATE) = @todayDate)
BEGIN
    -- Ca 1: Chờ check-in tại quầy lễ tân (Status = Confirmed, Test quét QR hoặc nhập SĐT 0900000004)
    INSERT INTO Appointments (PatientId, DoctorId, TimeSlotId, AppointmentDate, ConsultationFee, Status, Reason, QrCodeToken, QueueNumber, CreatedAt)
    VALUES (@p1, @docAn, @slotAn1, DATEADD(hour, 8, CAST(@todayDate AS DATETIME2)), 150000, N'Confirmed', N'Đau họng, sốt nhẹ 2 ngày nay', N'OCMS-APT-1001', NULL, GETUTCDATE());
    DECLARE @aptId1 INT = SCOPE_IDENTITY();

    -- Ca 2: Chờ check-in tại quầy lễ tân (Status = Confirmed, Test nhập SĐT 0912345678)
    INSERT INTO Appointments (PatientId, DoctorId, TimeSlotId, AppointmentDate, ConsultationFee, Status, Reason, QrCodeToken, QueueNumber, CreatedAt)
    VALUES (@p2, @docAn, @slotAn2, DATEADD(minute, 510, CAST(@todayDate AS DATETIME2)), 150000, N'Confirmed', N'Khám sức khỏe định kỳ & đau nửa đầu', N'OCMS-APT-1002', NULL, GETUTCDATE());
    DECLARE @aptId2 INT = SCOPE_IDENTITY();

    -- Ca 3: Đã Check-in thành công (Status = Arrived, Số khám #1) -> Hiện trên màn hình TV sảnh & BS An bấm "Gọi vào khám"
    INSERT INTO Appointments (PatientId, DoctorId, TimeSlotId, AppointmentDate, ConsultationFee, Status, Reason, QrCodeToken, QueueNumber, CreatedAt, UpdatedAt)
    VALUES (@p3, @docAn, @slotAn3, DATEADD(minute, 540, CAST(@todayDate AS DATETIME2)), 150000, N'Arrived', N'Tái khám huyết áp & chóng mặt', N'OCMS-APT-1003', 1, GETUTCDATE(), GETUTCDATE());
    DECLARE @aptId3 INT = SCOPE_IDENTITY();

    -- Ca 4: Đang trong phòng khám (Status = InConsultation) tại P102 với BS Hà khoa Nhi
    INSERT INTO Appointments (PatientId, DoctorId, TimeSlotId, AppointmentDate, ConsultationFee, Status, Reason, QrCodeToken, QueueNumber, CreatedAt, UpdatedAt)
    VALUES (@p4, @docHa, @slotHa1, DATEADD(hour, 8, CAST(@todayDate AS DATETIME2)), 180000, N'InConsultation', N'Sốt phát ban, ho có đờm', N'OCMS-APT-1004', 1, GETUTCDATE(), GETUTCDATE());
    DECLARE @aptId4 INT = SCOPE_IDENTITY();

    -- Ca 5: Đã hoàn tất khám (Status = Completed, Số khám #2) -> Có bệnh án ICD-10 và Đơn thuốc chờ thanh toán tại quầy
    INSERT INTO Appointments (PatientId, DoctorId, TimeSlotId, AppointmentDate, ConsultationFee, Status, Reason, QrCodeToken, QueueNumber, CreatedAt, UpdatedAt)
    VALUES (@p5, @docAn, @slotAn4, DATEADD(minute, 570, CAST(@todayDate AS DATETIME2)), 150000, N'Completed', N'Đau rát họng kéo dài, khàn tiếng', N'OCMS-APT-1005', 2, GETUTCDATE(), GETUTCDATE());
    DECLARE @aptId5 INT = SCOPE_IDENTITY();

    -- Nạp tiền đặt cọc trực tuyến (Payments - Deposit)
    INSERT INTO Payments (AppointmentId, Amount, PaymentMethod, PaymentStatus, PaymentType, TransactionId, CreatedAt) VALUES
    (@aptId1, 150000, N'POS',  N'Success', N'Deposit', N'POS-SEED-1001',  GETUTCDATE()),
    (@aptId2, 150000, N'Cash', N'Success', N'Deposit', N'CASH-SEED-1002', GETUTCDATE()),
    (@aptId3, 150000, N'Cash', N'Success', N'Deposit', N'CASH-SEED-1003', GETUTCDATE()),
    (@aptId4, 180000, N'POS',  N'Success', N'Deposit', N'POS-SEED-1004',  GETUTCDATE()),
    (@aptId5, 150000, N'Cash', N'Success', N'Deposit', N'CASH-SEED-1005', GETUTCDATE());

    -- Nạp Chỉ số sinh hiệu (VitalSigns) cho Ca 3, 4, 5
    INSERT INTO VitalSigns (AppointmentId, SystolicBP, DiastolicBP, Pulse, Temperature, SpO2, Weight, Height, RecordedAt) VALUES
    (@aptId3, 125, 82, 76, 36.8, 98, 68.50, 170.00, GETUTCDATE()),
    (@aptId4, 110, 70, 85, 38.2, 97, 52.00, 158.00, GETUTCDATE()),
    (@aptId5, 120, 80, 72, 37.0, 99, 72.00, 168.00, GETUTCDATE());

    -- Nạp Hồ sơ bệnh án điện tử (MedicalRecords) cho Ca 5
    INSERT INTO MedicalRecords (AppointmentId, Diagnosis, ICD10Code, DoctorNotes, FollowUpDate, CreatedAt)
    VALUES (@aptId5, N'Viêm họng cấp tính do vi khuẩn', N'J02.9', N'Bệnh nhân cần nghỉ ngơi, uống nhiều nước ấm, súc miệng nước muối sinh lý hàng ngày. Tái khám sau 7 ngày nếu còn triệu chứng.', DATEADD(day, 7, CAST(@todayDate AS DATETIME2)), GETUTCDATE());
    DECLARE @medRecordId5 INT = SCOPE_IDENTITY();

    -- Kê đơn thuốc mẫu (PrescriptionDetails) cho Ca 5
    DECLARE @idPara INT = (SELECT TOP 1 Id FROM Medicines WHERE Name LIKE N'%Paracetamol%');
    DECLARE @idAug INT = (SELECT TOP 1 Id FROM Medicines WHERE Name LIKE N'%Augmentin%');

    IF (@idPara IS NOT NULL AND @idAug IS NOT NULL)
    BEGIN
        INSERT INTO PrescriptionDetails (MedicalRecordId, MedicineId, Quantity, UnitPrice, DosageInstruction, Status) VALUES
        (@medRecordId5, @idPara, 20, 2000, N'Uống 1 viên khi sốt trên 38.5°C hoặc đau nhiều, cách nhau 4-6 tiếng.', N'Pending'),
        (@medRecordId5, @idAug,  14, 18000, N'Uống 1 viên sau ăn sáng và 1 viên sau ăn tối, uống liên tục 7 ngày.', N'Pending');
    END
END
GO

PRINT N'==============================================================================';
PRINT N'>>> ĐÃ TẠO DATABASE OCMS VÀ NẠP BỘ SEED DATA HOÀN CHỈNH THÀNH CÔNG! <<<';
PRINT N'Mọi thành viên có thể đăng nhập bằng email trong bảng tra cứu trên với mật khẩu: 123456';
PRINT N'==============================================================================';
GO
