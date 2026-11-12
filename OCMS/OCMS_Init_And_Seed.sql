-- ==============================================================================
-- HỆ THỐNG PHÒNG KHÁM NGOẠI TRÚ (OCMS) - SQL SCRIPT TẠO DATABASE & BẢNG
-- Hệ quản trị CSDL: Microsoft SQL Server (T-SQL)
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

-- Xóa bảng cũ theo thứ tự ràng buộc khóa ngoại (nếu muốn reset sạch)
/*
DROP TABLE IF EXISTS Notifications;
DROP TABLE IF EXISTS Feedbacks;
DROP TABLE IF EXISTS ServiceOrders;
DROP TABLE IF EXISTS PrescriptionDetails;
DROP TABLE IF EXISTS MedicalServices;
DROP TABLE IF EXISTS Medicines;
DROP TABLE IF EXISTS Payments;
DROP TABLE IF EXISTS VitalSigns;
DROP TABLE IF EXISTS MedicalRecords;
DROP TABLE IF EXISTS Appointments;
DROP TABLE IF EXISTS TimeSlots;
DROP TABLE IF EXISTS DoctorSchedules;
DROP TABLE IF EXISTS Doctors;
DROP TABLE IF EXISTS Patients;
DROP TABLE IF EXISTS Rooms;
DROP TABLE IF EXISTS Departments;
DROP TABLE IF EXISTS Users;
*/

-- ==============================================================================
-- 2. TẠO CÁC BẢNG (TABLES)
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
        Gender NVARCHAR(10) NOT NULL DEFAULT 'Other', -- Male, Female, Other
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
        AcademicTitle NVARCHAR(50) NOT NULL DEFAULT 'BS', -- BS, ThS.BS, CK1, PGS.TS...
        PricePerSlot DECIMAL(18,2) NOT NULL DEFAULT 150000,
        CONSTRAINT UQ_Doctors_UserId UNIQUE (UserId),
        CONSTRAINT FK_Doctors_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
        CONSTRAINT FK_Doctors_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(Id),
        CONSTRAINT FK_Doctors_Rooms FOREIGN KEY (DefaultRoomId) REFERENCES Rooms(Id)
    );
END
GO

-- Bảng Lịch làm việc Bác sĩ theo ngày (DoctorSchedules)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DoctorSchedules')
BEGIN
    CREATE TABLE DoctorSchedules (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        DoctorId INT NOT NULL,
        RoomId INT NOT NULL,
        WorkDate DATETIME2 NOT NULL,
        ShiftType NVARCHAR(20) NOT NULL DEFAULT 'Morning', -- Morning, Afternoon
        MaxQuota INT NOT NULL DEFAULT 16,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Active', -- Active, Cancelled
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
        Status NVARCHAR(20) NOT NULL DEFAULT 'Open', -- Open, Hold, Booked
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
        Status NVARCHAR(30) NOT NULL DEFAULT 'Pending', -- Pending, Confirmed, Arrived, InConsultation, Completed, Cancelled
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
        SystolicBP INT NOT NULL, -- Huyết áp tâm thu (mmHg)
        DiastolicBP INT NOT NULL, -- Huyết áp tâm trương (mmHg)
        Pulse INT NOT NULL, -- Nhịp tim (bpm)
        Temperature DECIMAL(4,1) NOT NULL, -- Thân nhiệt (°C)
        SpO2 INT NOT NULL, -- Nồng độ oxy trong máu (%)
        Weight DECIMAL(5,2) NOT NULL, -- Cân nặng (kg)
        Height DECIMAL(5,2) NOT NULL, -- Chiều cao (cm)
        RecordedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_VitalSigns_AppointmentId UNIQUE (AppointmentId),
        CONSTRAINT FK_VitalSigns_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id)
    );
END
GO

-- Bảng Hồ sơ bệnh án (MedicalRecords) - Quan hệ 1-1 với Appointments
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

-- Bảng Thuốc (Medicines)
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
        Status NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Paid, Dispensed
        CONSTRAINT FK_PrescriptionDetails_MedicalRecords FOREIGN KEY (MedicalRecordId) REFERENCES MedicalRecords(Id),
        CONSTRAINT FK_PrescriptionDetails_Medicines FOREIGN KEY (MedicineId) REFERENCES Medicines(Id)
    );
END
GO

-- Bảng Dịch vụ y tế / Cận lâm sàng (MedicalServices)
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
        Status NVARCHAR(20) NOT NULL DEFAULT 'Ordered', -- Ordered, Completed
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
        PaymentMethod NVARCHAR(20) NOT NULL DEFAULT 'Cash', -- Cash, VNPAY, POS
        PaymentStatus NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- Pending, Success, Failed, Refunded
        PaymentType NVARCHAR(30) NOT NULL DEFAULT 'Deposit', -- Deposit, FinalSettlement, Refund
        TransactionId NVARCHAR(100) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Payments_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id)
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
        NotificationType NVARCHAR(30) NOT NULL DEFAULT 'Reminder', -- Reminder, System, OTP
        IsRead BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES Users(Id)
    );
END
GO

-- ==============================================================================
-- 3. NẠP DỮ LIỆU MẪU ĐỂ TEST HỆ THỐNG (SEED DATA)
-- Mật khẩu mặc định của tất cả tài khoản test: 123456
-- (Hash BCrypt tương ứng: $2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi)
-- ==============================================================================

-- 3.1. Chuyên khoa (Departments)
SET IDENTITY_INSERT Departments ON;
INSERT INTO Departments (Id, Code, Name, Description, BaseConsultationFee) VALUES
(1, N'INT', N'Khoa Nội Tổng Quát', N'Khám và điều trị các bệnh nội khoa tổng quát', 150000),
(2, N'PED', N'Khoa Nhi', N'Khám và chăm sóc sức khỏe trẻ em', 150000),
(3, N'ENT', N'Khoa Tai Mũi Họng', N'Khám và nội soi chuyên sâu tai mũi họng', 200000);
SET IDENTITY_INSERT Departments OFF;
GO

-- 3.2. Phòng khám (Rooms)
SET IDENTITY_INSERT Rooms ON;
INSERT INTO Rooms (Id, RoomNumber, DepartmentId, RoomType, IsOperational) VALUES
(1, N'101', 1, N'Consultation', 1),
(2, N'102', 2, N'Consultation', 1),
(3, N'201', 3, N'Consultation', 1);
SET IDENTITY_INSERT Rooms OFF;
GO

-- 3.3. Người dùng mẫu (Users)
-- Mật khẩu: 123456
DECLARE @defaultHash NVARCHAR(MAX) = N'$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi';

SET IDENTITY_INSERT Users ON;
INSERT INTO Users (Id, FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES
(1, N'System Administrator', N'admin@ocms.clinic', N'0900000001', @defaultHash, N'Admin', 1, GETUTCDATE()),
(2, N'Lễ Tân Thu Ngân', N'staff@ocms.clinic', N'0900000002', @defaultHash, N'Staff', 1, GETUTCDATE()),
(3, N'BS. Nguyễn Văn An', N'doctor.an@ocms.clinic', N'0900000003', @defaultHash, N'Doctor', 1, GETUTCDATE()),
(4, N'BS. Trần Thu Hà', N'doctor.ha@ocms.clinic', N'0900000005', @defaultHash, N'Doctor', 1, GETUTCDATE()),
(5, N'Trần Quang Phong', N'patient1@ocms.clinic', N'0900000004', @defaultHash, N'Patient', 1, GETUTCDATE()),
(6, N'Lê Thị Mai', N'patient2@ocms.clinic', N'0912345678', @defaultHash, N'Patient', 1, GETUTCDATE()),
(7, N'Hoàng Minh Đức', N'patient3@ocms.clinic', N'0988776655', @defaultHash, N'Patient', 1, GETUTCDATE());
SET IDENTITY_INSERT Users OFF;
GO

-- 3.4. Hồ sơ Bác sĩ (Doctors)
SET IDENTITY_INSERT Doctors ON;
INSERT INTO Doctors (Id, UserId, DepartmentId, DefaultRoomId, AcademicTitle, PricePerSlot) VALUES
(1, 3, 1, 1, N'ThS.BS', 150000), -- BS An - Khoa Nội - Phòng 101
(2, 4, 2, 2, N'BS.CKI', 180000); -- BS Hà - Khoa Nhi - Phòng 102
SET IDENTITY_INSERT Doctors OFF;
GO

-- 3.5. Hồ sơ Bệnh nhân (Patients)
SET IDENTITY_INSERT Patients ON;
INSERT INTO Patients (Id, UserId, DateOfBirth, Gender, Address, HealthInsuranceNumber) VALUES
(1, 5, '2000-01-01', N'Male', N'Hà Nội', N'DN4010123456789'),
(2, 6, '1995-05-12', N'Female', N'Cầu Giấy, Hà Nội', N'DN4010998877665'),
(3, 7, '1988-10-20', N'Male', N'Ba Đình, Hà Nội', N'DN4010112233445');
SET IDENTITY_INSERT Patients OFF;
GO

-- 3.6. Lịch làm việc hôm nay của Bác sĩ (DoctorSchedules)
-- Lấy ngày hôm nay của server
DECLARE @today DATETIME2 = CAST(GETDATE() AS DATE);

SET IDENTITY_INSERT DoctorSchedules ON;
INSERT INTO DoctorSchedules (Id, DoctorId, RoomId, WorkDate, ShiftType, MaxQuota, Status) VALUES
(1, 1, 1, @today, N'Morning', 16, N'Active'), -- BS An trực P101 sáng nay
(2, 2, 2, @today, N'Morning', 16, N'Active'); -- BS Hà trực P102 sáng nay
SET IDENTITY_INSERT DoctorSchedules OFF;
GO

-- 3.7. Khung giờ khám hôm nay (TimeSlots)
SET IDENTITY_INSERT TimeSlots ON;
INSERT INTO TimeSlots (Id, ScheduleId, StartTime, EndTime, Status) VALUES
-- BS An (P101)
(1, 1, '08:00:00', '08:30:00', N'Booked'),
(2, 1, '08:30:00', '09:00:00', N'Booked'),
(3, 1, '09:00:00', '09:30:00', N'Booked'),
(4, 1, '09:30:00', '10:00:00', N'Open'),   -- Filler slot trống
(5, 1, '10:00:00', '10:30:00', N'Open'),   -- Filler slot trống
(6, 1, '10:30:00', '11:00:00', N'Open'),   -- Filler slot trống
-- BS Hà (P102)
(7, 2, '08:00:00', '08:30:00', N'Booked'),
(8, 2, '08:30:00', '09:00:00', N'Open'),   -- Filler slot trống
(9, 2, '09:00:00', '09:30:00', N'Open');   -- Filler slot trống
SET IDENTITY_INSERT TimeSlots OFF;
GO

-- 3.8. Danh sách Cuộc hẹn mẫu hôm nay để test Check-in & Gọi số (Appointments)
DECLARE @todayDate DATETIME2 = CAST(GETDATE() AS DATE);

SET IDENTITY_INSERT Appointments ON;
INSERT INTO Appointments (Id, PatientId, DoctorId, TimeSlotId, AppointmentDate, Status, Reason, QrCodeToken, QueueNumber, CreatedAt) VALUES
-- Ca 1: Chờ check-in (Status = Confirmed) -> Dùng để test Quét ảnh QR hoặc nhập SĐT 0900000004
(1, 1, 1, 1, DATEADD(hour, 8, @todayDate), N'Confirmed', N'Đau họng, sốt nhẹ 2 ngày nay', N'OCMS-APT-1001', NULL, GETUTCDATE()),

-- Ca 2: Chờ check-in (Status = Confirmed) -> Dùng để test Quét ảnh QR hoặc nhập SĐT 0912345678
(2, 2, 1, 2, DATEADD(minute, 510, @todayDate), N'Confirmed', N'Khám sức khỏe định kỳ & đau nửa đầu', N'OCMS-APT-1002', NULL, GETUTCDATE()),

-- Ca 3: Đã Check-in tại quầy (Status = Arrived, Số khám #1) -> Dùng để test màn hình TV sảnh & nút "Gọi vào khám"
(3, 3, 1, 3, DATEADD(minute, 540, @todayDate), N'Arrived', N'Tái khám huyết áp & chóng mặt', N'OCMS-APT-1003', 1, GETUTCDATE());
SET IDENTITY_INSERT Appointments OFF;
GO

-- 3.9. Chỉ số sinh hiệu mẫu cho ca đã check-in (VitalSigns)
SET IDENTITY_INSERT VitalSigns ON;
INSERT INTO VitalSigns (Id, AppointmentId, SystolicBP, DiastolicBP, Pulse, Temperature, SpO2, Weight, Height, RecordedAt) VALUES
(1, 3, 125, 82, 76, 36.8, 98, 68.50, 170.00, GETUTCDATE());
SET IDENTITY_INSERT VitalSigns OFF;
GO

-- 3.10. Thuốc mẫu trong kho (Medicines)
SET IDENTITY_INSERT Medicines ON;
INSERT INTO Medicines (Id, Name, Unit, Price, StockQuantity, Description, IsAvailable) VALUES
(1, N'Paracetamol 500mg', N'Viên', 2000, 500, N'Thuốc giảm đau, hạ sốt', 1),
(2, N'Amoxicillin 500mg', N'Viên', 5000, 200, N'Kháng sinh phổ rộng', 1),
(3, N'Decolgen Forte', N'Vỉ', 15000, 100, N'Giảm cảm cúm, sổ mũi', 1),
(4, N'Berocca Effervescent', N'Tuýp', 85000, 50, N'Viên sủi bổ sung vitamin', 1);
SET IDENTITY_INSERT Medicines OFF;
GO

-- 3.11. Dịch vụ cận lâm sàng (MedicalServices)
SET IDENTITY_INSERT MedicalServices ON;
INSERT INTO MedicalServices (Id, DepartmentId, Name, Price, Description, IsActive) VALUES
(1, 1, N'Xét nghiệm công thức máu toàn phần', 120000, N'Đánh giá các tế bào máu cơ bản', 1),
(2, 1, N'Siêu âm ổ bụng tổng quát', 180000, N'Khảo sát gan, mật, thận, lách', 1),
(3, 3, N'Nội soi Tai Mũi Họng', 200000, N'Nội soi tầm soát bệnh lý TMH', 1);
SET IDENTITY_INSERT MedicalServices OFF;
GO

PRINT N'>>> ĐÃ TẠO DATABASE VÀ NẠP DỮ LIỆU TEST OCMS THÀNH CÔNG! <<<';
GO
