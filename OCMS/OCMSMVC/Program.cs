using DataAccessObjects;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;

namespace OCMSMVC
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<OcmsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));


            //Đăng ký Dependencies cho các Repository và Service
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
            builder.Services.AddScoped<IDoctorScheduleRepository, DoctorScheduleRepository>();
            builder.Services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();



            //cấu hình sesion 
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options => 
            { 
                 options.IdleTimeout = TimeSpan.FromMinutes(60);
                 options.Cookie.HttpOnly = true;    // Đảm bảo cookie session chỉ có thể truy cập thông qua HTTP và không thể truy cập từ JavaScript
                options.Cookie.IsEssential = true; // Đảm bảo cookie session được gửi ngay cả khi người dùng chưa đăng nhập
            });



            var app = builder.Build();

            // Seed the database with initial data
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

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseStaticFiles();

            app.UseRouting();

            app.UseSession();
            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
