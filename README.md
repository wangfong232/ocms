# OCMS — Outpatient Clinic Appointment & Management System

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
[![ASP.NET Core MVC](https://img.shields.io/badge/ASP.NET%20Core-MVC%208.0-512BD4?style=flat&logo=aspnet)](https://learn.microsoft.com/aspnet/core)
[![EF Core](https://img.shields.io/badge/EF%20Core-8.0%20(Code--First)-blue?style=flat)](https://learn.microsoft.com/ef/core/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--Time%20Queue-orange?style=flat&logo=signalr)](https://learn.microsoft.com/aspnet/core/signalr)
[![SQL Server](https://img.shields.io/badge/Database-SQL%20Server%202022-CC292B?style=flat&logo=microsoftsqlserver)](https://www.microsoft.com/sql-server)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

> **Course:** PRN222 — Advanced .NET Application Development  
> **Team:** SE1972-NET — **Group 6**  
> **Institution:** FPT University Hanoi  
> **System Scope:** Production-Ready Outpatient Polyclinic Workflow (4 Roles, 15 Core Use Cases, 17 Normalized Entities)

---

## 📖 Table of Contents
1. [Project Overview](#-project-overview)
2. [Key Architectural Highlights & Killer Features](#-key-architectural-highlights--killer-features)
3. [Technology Stack & Architecture](#-technology-stack--architecture)
4. [System Architecture & Roles](#-system-architecture--roles)
5. [Database Schema (17 Entities)](#-database-schema-17-entities)
6. [Getting Started & Installation](#-getting-started--installation)
7. [Default Test Accounts](#-default-test-accounts)
8. [Project Structure (5-Tier Architecture)](#-project-structure-5-tier-architecture)
9. [Git Collaboration & Team Conventions](#-git-collaboration--team-conventions)
10. [Development Team & Use Case Assignments](#-development-team--use-case-assignments)

---

## 🏥 Project Overview

The **Outpatient Clinic Appointment & Management System (OCMS)** is an enterprise-grade web application engineered to digitize end-to-end outpatient workflows for private polyclinics and medical centers. 

Unlike conventional CRUD booking platforms, OCMS enforces strict healthcare domain constraints, eliminating paper logbooks, crowded lobby queues, and scheduling collisions through:
* **Optimistic Concurrency Control (`RowVersion`)** preventing double-booking race conditions down to the millisecond.
* **Real-time SignalR WebSocket dispatching** streaming calling queue numbers directly to lobby monitors without polling.
* **Paperless browser-native operations** including webcam-based 2D QR check-in (`html5-qrcode`) and formatted thermal receipt printing (`window.print()`).
* **Privacy-enforced Electronic Medical Records (EMR)** safeguarding clinical diagnoses and prescription lines under role-based authorization.

---

## ⚡ Key Architectural Highlights & Killer Features

| Feature | Technical Implementation | Business Purpose |
| :--- | :--- | :--- |
| **1. Concurrency Slot Locking** | EF Core `[Timestamp]` / `RowVersion` + Database Transaction | Eliminates double-booking race conditions when multiple patients contest the same 15-minute slot. Unpaid reservations transition to `Status = 'Hold'` with a 10-minute countdown. |
| **2. Real-Time Lobby Queue Display** | ASP.NET Core SignalR `QueueHub` (`/lobbyQueueHub`) | When a doctor clicks *"Call Next"*, lobby TV monitors instantaneously display the ticket number and chime without refreshing the page. |
| **3. Webcam QR Code Check-in** | Client-side JavaScript `html5-qrcode` integration | Front-desk staff scan e-ticket QR codes directly using laptop/desktop webcams, executing intake triage validation in `< 2` seconds. |
| **4. In-Session Paraclinical Orders** | Inline Diagnostic Service Selection (`Locked UnitPrice`) | Doctors order rapid blood/urine tests, enter quantitative findings directly into the active session, and freeze prices against catalog changes. |
| **5. Consolidated Counter Settlement** | Aggregated Formula: $(\text{Consultation} + \text{Tests} + \text{Drugs}) - \text{Deposit}$ | Cashier settles balances via Cash/POS, automatically authorizes medication dispensing, and triggers browser-native thermal receipt printing. |
| **6. Atomic Pharmacy Stock Deduction** | `Medicines.StockQuantity` deduction | Dispensing staff verify paid items, hand medications over to patients, atomically decrement inventory, and print dosage instruction slips. |
| **7. Automated Hold-Release Worker** | `IHostedService` / Background Worker running on a 1-minute cron | Scans for held reservations exceeding the 10-minute window, autonomously releasing slots back to `Status = 'Open'`. |

---

## 🛠️ Technology Stack & Architecture

* **Framework:** ASP.NET Core 8.0 (MVC — Model-View-Controller with Server-Side Rendering)
* **Architecture Pattern:** 5-Tier Layered Architecture (`BusinessObjects`, `DataAccessObjects`, `Repositories`, `Services`, `OCMSMVC`)
* **Language:** C# 12 / .NET 8
* **ORM:** Entity Framework Core 8.0 (Code-First Migrations & Fluent API Mapping)
* **Database:** Microsoft SQL Server 2022
* **Real-time Communication:** Microsoft ASP.NET Core SignalR
* **Security & Auth:** ASP.NET Core Session State (Session-based Role Authentication) kết hợp mã hóa mật khẩu BCrypt
* **Background Tasks:** .NET Worker Service (`IHostedService` / `BackgroundService`)
* **Frontend UI:** Razor Views (`.cshtml`), HTML5, CSS3, Bootstrap 5, FontAwesome 6, Chart.js
* **Hardware Integration (Web Native):** 
  * Barcode/QR Scanning: `html5-qrcode` (Browser Webcam)
  * Slip & Invoice Printing: Native CSS `@media print` + `window.print()`

---

## 👥 System Architecture & Roles

OCMS strictly isolates permissions across **4 core user roles**:

```
                       ┌─────────────────────────┐
                       │   ASP.NET Core Cookie   │
                       │     Authentication      │
                       └────────────┬────────────┘
         ┌───────────────────┬──────┴──────┬───────────────────┐
         ▼                   ▼             ▼                   ▼
    [ Patient ]         [ Doctor ]      [ Staff ]           [ Admin ]
  • Self-service      • Shift Rostering • Webcam QR Checkin • Infrastructure
  • Concurrency Book  • EMR & Diagnosis • Vital Signs Triage • Doctor Profiles
  • 10m Deposit Hold  • Rapid Tests     • SignalR Dispatch  • Shift Approval
  • Reschedule/Cancel • E-Prescriptions • Final Settlement  • Revenue Analytics
  • Booking History   • Patient Queue   • Dispense Medicine • User Accounts
```

---

## 🗄️ Database Schema (17 Entities)

The system is backed by a fully normalized (3NF) relational schema designed in Entity Framework Core with strict `DeleteBehavior.Restrict` foreign key integrity:

1. **`Users`**: Central credentials, salt, password hashes, and RBAC roles (`Patient`, `Doctor`, `Staff`, `Admin`).
2. **`Patients`**: Demographic records, emergency contacts, and health insurance card references (1-to-1 with `Users`).
3. **`Doctors`**: Clinical credentials, department affiliations, assigned room references, and consultation fees (`PricePerSlot`).
4. **`Departments`**: Medical specialties governing doctor assignments and room categorizations.
5. **`Rooms`**: Physical clinic infrastructure (consultation and diagnostic examination rooms).
6. **`DoctorSchedules`**: Working shift rosters (Morning: 07:30–11:30, Afternoon: 13:00–17:00).
7. **`TimeSlots`**: Discrete 15-minute consultation slots governed by `Status (Open, Hold, Booked)` and `RowVersion` concurrency tokens.
8. **`Appointments`**: Core transactional lifecycle (`Pending`, `Confirmed`, `Arrived`, `InConsultation`, `Completed`, `Cancelled`), including `QueueNumber`, `QrCodeToken`, and `CancellationReason`.
9. **`VitalSigns`**: Pre-clinical triage records captured at check-in (`SystolicBP`, `DiastolicBP`, `Pulse`, `SpO2`, `Temperature (decimal 4,1)`, `Weight (decimal 5,2)`, `Height (decimal 5,2)`).
10. **`MedicalRecords`**: Finalized clinical consultations, ICD-10 diagnoses, confidential notes, and recall dates.
11. **`MedicalServices`**: Master catalog of outpatient rapid diagnostic tests and ultrasound/imaging services.
12. **`ServiceOrders`**: Line items for ordered diagnostic tests preserving historical frozen `UnitPrice` and clinical findings.
13. **`Medicines`**: Outpatient pharmacy formulary maintaining unit prices and real-time inventory balances (`StockQuantity`, `IsAvailable`).
14. **`PrescriptionDetails`**: Line items per medical record capturing prescribed quantities, frozen prices, and dosage instructions.
15. **`Payments`**: Financial transactions (`Deposit`, `FinalSettlement`, `Refund`) with statuses (`Pending`, `Success`, `Failed`, `Refunded`).
16. **`Feedbacks`**: Post-visit patient satisfaction ratings (1–5 stars) and qualitative feedback comments (1-to-1 with `Appointments`).
17. **`Notifications`**: Audit logs of automated dispatch reminders and transactional verification codes.

---

## 🚀 Getting Started & Installation

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
* [Microsoft SQL Server 2019/2022](https://www.microsoft.com/sql-server) (or SQL Server Express / LocalDB)
* [Visual Studio 2022](https://visualstudio.microsoft.com/) (v17.8+) or [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit

### 1. Clone the Repository
```bash
git clone https://github.com/wangfong232/ocms.git
cd ocms/OCMS
```

### 2. Configure Database Connection String
Open `OCMSMVC/appsettings.json` and verify the connection string targeting your SQL Server instance:
* **SQL Server Authentication (Default):**
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=(local);Database=OCMS_Db;User Id=sa;Password=123;MultipleActiveResultSets=true;TrustServerCertificate=True"
    }
  }
  ```
* **Windows Authentication (Alternative):**
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=(local);Database=OCMS_Db;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
    }
  }
  ```

### 3. Automatic Database Initialization & Seed Data
You do **not** need to manually run migrations on first setup!
When running the application, `DbInitializer.Initialize(context)` in `Program.cs` automatically creates the `OCMS_Db` database and populates it with:
* 3 Departments (Nội tổng quát, Nhi, Tai Mũi Họng)
* 3 Consultation Rooms (101, 102, 201)
* 4 Ready-to-use Test Accounts (Admin, Doctor, Staff, Patient)

*(Optional) If you modify entities and need to apply manual EF Core migrations:*
```powershell
Update-Database -Project DataAccessObjects -StartupProject OCMSMVC
```

### 4. Run the Application
Press **F5** in Visual Studio, or run via CLI:
```bash
dotnet run --project OCMSMVC
```
Open your browser and navigate to:
* `https://localhost:7196` or `http://localhost:5196`

### Staff billing and pharmacy

* Staff/Admin can use **Billing** to settle completed appointments by Cash or POS, print receipts, and use **Pharmacy** to dispense paid prescriptions with an atomic stock deduction.
* EF Core migrations are applied at startup. Existing databases created by the previous `EnsureCreated` initializer are adopted as the initial migration baseline before applying additive migrations.

---

## 🔑 Default Test Accounts

All test accounts are pre-seeded via `DbInitializer.cs` for immediate workflow testing:

| Role | Username / Email | Password | Access Scope |
| :--- | :--- | :--- | :--- |
| **System Admin** | `admin@ocms.clinic` | `123456` | Master data settings, room catalogs, revenue analytics |
| **Clinic Staff** | `staff@ocms.clinic` | `123456` | Webcam QR check-in, vital signs triage, counter settlement, pharmacy dispensing |
| **Doctor** | `doctor@ocms.clinic` | `123456` | Shift rostering, daily room queue, clinical EMR, test ordering, e-prescriptions |
| **Patient** | `patient@ocms.clinic` | `123456` | Online doctor search, slot reservation, e-ticket QR pass, booking history |

---

## 📂 Project Structure (5-Tier Architecture)

The solution adheres strictly to the PRN222 standard 5-project separation of concerns:

```text
ocms/
├── .gitignore                         # .NET & Visual Studio exclusion rules
├── README.md                          # Technical overview & onboarding guide
└── OCMS/                              # Solution Root Directory
    ├── OCMS.slnx                      # Visual Studio Solution File
    │
    ├── BusinessObjects/               # Domain Entity Models & Enums
    │   ├── Entities/                  # 17 normalized domain model classes
    │   └── BusinessObjects.csproj
    │
    ├── DataAccessObjects/             # Data Access Layer & EF Core Database Context
    │   ├── Migrations/                # EF Core Code-First Migrations (InitialCreate)
    │   ├── DbInitializer.cs           # Database catalog & account seeder
    │   ├── OcmsDbContext.cs           # EF Core DbContext, Fluent API & Restrict FKs
    │   └── DataAccessObjects.csproj
    │
    ├── Repositories/                  # Data Abstraction Layer (Repository Pattern)
    │   ├── IUserRepository.cs         # Sample Repository Interface
    │   ├── UserRepository.cs          # Sample Repository Implementation (EF Core)
    │   └── Repositories.csproj
    │
    ├── Services/                      # Application Business Logic Layer
    │   ├── IUserService.cs            # Sample Service Interface (Auth/Register)
    │   ├── UserService.cs             # Sample Service Implementation (BCrypt validation)
    │   └── Services.csproj
    │
    └── OCMSMVC/                       # Presentation Layer (ASP.NET Core MVC SSR)
        ├── Controllers/               # Controllers (Home, Auth, Booking, Doctor, Reception, Cashier, Admin)
        ├── Hubs/                      # SignalR Realtime Hubs (QueueHub.cs for TV Queue Display)
        ├── Workers/                   # Background Workers (SlotExpiryWorker.cs, DailyReminderWorker.cs)
        ├── Models/                    # ViewModels and UI Transfer Objects
        ├── Views/                     # Razor Views (_Layout, Shared, Feature Views)
        ├── wwwroot/                   # Static files (CSS, JS, Bootstrap 5, html5-qrcode)
        ├── Program.cs                 # DI registration, Cookie Auth, Session, Pipeline
        ├── appsettings.json           # SQL Server database connection string
        └── OCMSMVC.csproj
```

---

## 🌿 Git Collaboration & Team Conventions

To ensure smooth teamwork, avoid merge conflicts, and protect the codebase across 5 team members:

### 1. Branch Structure & Pre-Created Branches
* **`main`**: Production-ready branch for milestone grading and final demo. **Chỉ merge từ `develop` khi nghiệm thu**.
* **`develop`**: Central integration branch. Chứa codebase chung đang phát triển.
* **5 Feature Branches ĐÃ CÓ SẴN trên GitHub** (Mỗi thành viên làm việc trên đúng nhánh của mình, **KHÔNG CẦN TẠO MỚI**):

| Tên nhánh có sẵn trên GitHub | Thành viên phụ trách | 3 Use Cases đảm nhiệm |
| :--- | :--- | :--- |
| **`feature/booking-concurrency`** | **Trần Quang Phong (Leader)** | UC01 (Booking & Lock), UC02 (Hủy/Đổi lịch), UC12 (Auth/Cookie) |
| **`feature/doctor-emr`** | **Lê Đức Anh** | UC03 (Đăng ký ca trực), UC04 (Đổi ca), UC05 (Hồ sơ EMR & Khám) |
| **`feature/reception-queue`** | **Nguyễn Hữu Minh Đức** | UC06 (Quét QR Lễ tân), UC07 (TV phòng chờ SignalR), UC08 (Đo Vitals) |
| **`feature/billing-pharmacy`** | **Nguyễn Xuân Bằng** | UC09 (Thu ngân & In phiếu), UC10 (Phát thuốc & Kho), UC11 (Doanh thu) |
| **`feature/admin-analytics`** | **Nguyễn Việt Hà** | UC13 (VNPAY Webhook), UC14 (Background Worker), UC15 (Admin Data) |

---

### 2. Standard 4-Step Git Workflow (Quy trình làm việc bắt buộc)

1. **Bước 1 — Chuyển vào nhánh của mình & Kéo code mới nhất từ `develop`:**
   * Luôn chạy lệnh này trước khi bắt đầu viết code để nhận cập nhật mới nhất từ cả nhóm:
     ```bash
     git checkout feature/<tên-nhánh-của-bạn>
     git pull origin develop
     ```

2. **Bước 2 — Code, Test & Commit trên nhánh cá nhân:**
   * Sau khi code xong và test chạy thử không lỗi:
     ```bash
     git add .
     git commit -m "feat(UCxx): mô tả ngắn gọn tính năng vừa làm"
     ```

3. **Bước 3 — Push lên nhánh của mình (TUYỆT ĐỐI KHÔNG PUSH THẲNG VÀO DEVELOP):**
   * Đẩy code lên nhánh cá nhân trên GitHub:
     ```bash
     git push origin feature/<tên-nhánh-của-bạn>
     ```

4. **Bước 4 — Mở Pull Request (PR) & Leader Review:**
   * Lên web GitHub, tạo **Pull Request** từ nhánh `feature/...` của bạn vào nhánh **`develop`**.
   * **Leader (Phong)** review code: nếu đúng kiến trúc 5 tầng, không đè file người khác $
ightarrow$ Bấm **Merge vào `develop`**.
   * Khi toàn bộ nhóm hoàn thành xong milestone và kiểm thử luồng thông suốt $
ightarrow$ Leader mới merge từ **`develop` vào `main`** để nộp bài!

---

### 3. Implementation Pattern (Mẫu chuẩn để các thành viên làm theo)
Khi triển khai một thực thể mới (ví dụ `Appointment`, `Payment`, `DoctorSchedule`):
1. **Repositories:** Tạo `I<Entity>Repository.cs` và `<Entity>Repository.cs` trong project `Repositories`.
2. **Services:** Tạo `I<Entity>Service.cs` và `<Entity>Service.cs` trong project `Services`.
3. **Dependency Injection:** Khai báo vào `Program.cs`:
   ```csharp
   builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
   builder.Services.AddScoped<IAppointmentService, AppointmentService>();
   ```
4. **Phân quyền bằng Session (Role-Based Session Check):**
   Kiểm tra trực tiếp biến `Role` lưu trong Session ở đầu mỗi Action hoặc Controller:
   ```csharp
   // Ví dụ trong DoctorController (Chỉ cho Bác sĩ truy cập):
   var role = HttpContext.Session.GetString("Role");
   if (role != "Doctor") 
   {
       return RedirectToAction("Login", "Auth");
   }

   // Ví dụ trong ReceptionController / CashierController (Cho Lễ tân, Thu ngân):
   var role = HttpContext.Session.GetString("Role");
   if (role != "Staff" && role != "Admin")
   {
       return RedirectToAction("AccessDenied", "Auth");
   }
   ```

---

## 👨‍💻 Development Team & Use Case Assignments

**SE1972-NET — Group 6 (FPT University Hanoi)**  
*Mỗi thành viên chịu trách nhiệm độc lập 3 Core Use Cases ($5 \times 3 = 15$ Use Cases):*

| Student ID | Full Name | Project Role | 3 Core Assigned Use Cases |
| :---: | :--- | :---: | :--- |
| **HE190966** | **Trần Quang Phong** | **Team Leader / Architect** | • **UC01:** Doctor Directory & Smart Capacity Search / 10m Concurrency Slot Lock<br>• **UC02:** Self-Service Reschedule & Cancellation Flow<br>• **UC12:** Role-Based Login & Patient Register (Cookie/Session Auth) |
| **HE191710** | **Lê Đức Anh** | **Core Developer** | • **UC03:** Doctor Weekly Shift Registration & Quota<br>• **UC04:** Peer Shift Swapping & Emergency Handover<br>• **UC05:** Clinical Consultation Workspace, Rapid Tests & EMR ICD-10 Tagging |
| **HE191558** | **Nguyễn Hữu Minh Đức** | **Core Developer** | • **UC06:** Front-Desk Webcam QR Scanner Check-in (`html5-qrcode`)<br>• **UC07:** Real-Time SignalR Lobby Waiting Room Monitor (`QueueHub`)<br>• **UC08:** Triage Vital Signs Recording & BMI Calculation |
| **HE190879** | **Nguyễn Xuân Bằng** | **Core Developer** | • **UC09:** Consolidated Counter Settlement & Thermal Slip Printing (`window.print()`)<br>• **UC10:** Pharmacy Dispensing Verification & Atomic Stock Deduction<br>• **UC11:** Financial Revenue & Service Utilization Analytics |
| **HE191827** | **Nguyễn Việt Hà** | **Core Developer** | • **UC13:** Online Deposit Payment via VNPAY Sandbox (IPN Webhook)<br>• **UC14:** Background Worker Automation (10m Slot Expiry Cleanup & Email Reminders)<br>• **UC15:** System Administration & Master Data Catalog Management |

---

## 📄 License
This project is developed for educational purposes under the **PRN222** curriculum at FPT University. Distributed under the MIT License.
