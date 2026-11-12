using DataAccessObjects;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using OCMSMVC.Workers;

namespace OCMSMVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 1. Đăng ký Controllers & Views
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<OcmsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // 3. Đăng ký Dependency Injection (Repository & Service)
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            builder.Services.AddScoped<IReceptionService, ReceptionService>();
            builder.Services.AddScoped<IDepartmentService, DepartmentService>();
            builder.Services.AddScoped<IDoctorService, DoctorService>();
            builder.Services.AddScoped<IEmailService, EmailService>();
            builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

            builder.Services.AddScoped<IClinicWorkflowRepository, ClinicWorkflowRepository>();
            builder.Services.AddScoped<IClinicWorkflowService, ClinicWorkflowService>();
            builder.Services.AddScoped<IAdminManagementRepository, AdminManagementRepository>();
            builder.Services.AddScoped<IAdminManagementService, AdminManagementService>();

            // Đăng ký các dịch vụ Worker
            builder.Services.AddHostedService<SlotExpiryWorker>();
            builder.Services.AddHostedService<DailyReminderWorker>();

            builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
            builder.Services.AddScoped<ITimeSlotRepository, TimeSlotRepository>();
            builder.Services.AddScoped<ICapacityEngine, CapacityEngine>();
            builder.Services.AddScoped<IBookingService, BookingService>();
            builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
            builder.Services.AddScoped<IAppointmentService, AppointmentService>();


            //cấu hình sesion 
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options => 
            { 
                 options.IdleTimeout = TimeSpan.FromMinutes(60);
                 options.Cookie.HttpOnly = true;    // Đảm bảo cookie session chỉ có thể truy cập thông qua HTTP và không thể truy cập từ JavaScript
                options.Cookie.IsEssential = true; // Đảm bảo cookie session được gửi ngay cả khi người dùng chưa đăng nhập
            });

            var app = builder.Build();

            // 5. Seed dữ liệu mẫu khi khởi động ứng dụng
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<OcmsDbContext>();
                    DbInitializer.Initialize(context);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Lỗi xảy ra trong quá trình nạp Seed Data.");
                }
            }

            // 6. Cấu hình HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();
            app.Use(async (context, next) =>
            {
                var userId = context.Session.GetInt32("UserId");
                if (userId.HasValue)
                {
                    var db = context.RequestServices.GetRequiredService<OcmsDbContext>();
                    var isActive = await db.Users.AsNoTracking().AnyAsync(user => user.Id == userId.Value && user.IsActive);
                    if (!isActive)
                    {
                        context.Session.Clear();
                        context.Response.Redirect("/Auth/Login");
                        return;
                    }
                }
                await next();
            });
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
