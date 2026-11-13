using Microsoft.EntityFrameworkCore;
using BusinessObjects.Entities;

namespace DataAccessObjects
{
    public class OcmsDbContext : DbContext
    {
        public OcmsDbContext(DbContextOptions<OcmsDbContext> options) : base(options)
        {

        }

        public DbSet<User> Users { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedules { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<VitalSigns> VitalSigns { get; set; }
        public DbSet<MedicalRecord> MedicalRecords { get; set; }
        public DbSet<MedicalService> MedicalServices { get; set; }
        public DbSet<ServiceOrder> ServiceOrders { get; set; }
        public DbSet<Medicine> Medicines { get; set; }
        public DbSet<PrescriptionDetail> PrescriptionDetails { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //prevent cascade delete for all relationships
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            //unique constraints
            modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.PhoneNumber).IsUnique();
            modelBuilder.Entity<Department>().HasIndex(d => d.Code).IsUnique();
            modelBuilder.Entity<Room>().HasIndex(r => r.RoomNumber).IsUnique();

            // 1 - 1 relationships
            // User - Patient
            modelBuilder.Entity<Patient>()
                            .HasOne(p => p.User)
                            .WithOne(u => u.Patient)
                            .HasForeignKey<Patient>(p => p.UserId);

            // User - Doctor
            modelBuilder.Entity<Doctor>()
                          .HasOne(d => d.User)
                          .WithOne(u => u.Doctor)
                          .HasForeignKey<Doctor>(d => d.UserId);

            //Appoinment - VitalSigns
            modelBuilder.Entity<VitalSigns>()
                           .HasOne(v => v.Appointment)
                           .WithOne(a => a.VitalSigns)
                           .HasForeignKey<VitalSigns>(v => v.AppointmentId);

            //Appoinment - MedicalRecord
            modelBuilder.Entity<MedicalRecord>()
                           .HasOne(m => m.Appointment)
                           .WithOne(a => a.MedicalRecord)
                           .HasForeignKey<MedicalRecord>(m => m.AppointmentId);

            //Appoinment - Feedback
            modelBuilder.Entity<Feedback>()
                          .HasOne(f => f.Appointment)
                          .WithOne(a => a.Feedback)
                          .HasForeignKey<Feedback>(f => f.AppointmentId);

            modelBuilder.Entity<Payment>()
                .HasOne(p => p.Cashier)
                .WithMany()
                .HasForeignKey(p => p.CashierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PrescriptionDetail>()
                .HasOne(p => p.DispensedBy)
                .WithMany()
                .HasForeignKey(p => p.DispensedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Concurrency token 
            modelBuilder.Entity<TimeSlot>().Property(t => t.RowVersion).IsRowVersion();
        }
    }
}
