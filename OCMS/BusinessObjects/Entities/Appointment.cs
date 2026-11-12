using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects.Entities
{
    public class Appointment
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Patient")]
        public int PatientId { get; set; }
        [ForeignKey("Doctor")]
        public int DoctorId { get; set; }
        [ForeignKey("TimeSlot")]
        public int TimeSlotId { get; set; }
        public DateTime AppointmentDate { get; set; }
        // Snapshot of the consultation fee at booking time. Null is retained for legacy appointments.
        [Column(TypeName = "decimal(18,2)")]
        public decimal? ConsultationFee { get; set; }
        [Required, MaxLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Arrived, InConsultation, Completed, Cancelled
        [MaxLength(500)]
        public string? Reason { get; set; }

        [MaxLength(500)]
        public string? CancellationReason { get; set; }
        [MaxLength(100)]
        public string? QrCodeToken { get; set; }
        public int? QueueNumber { get; set; }

        public int RescheduledCount { get; set; } = 0; // Max 1 lần
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        // Navigation Properties
        public Patient Patient { get; set; } = null!;
        public Doctor Doctor { get; set; } = null!;
        public TimeSlot TimeSlot { get; set; } = null!;
        public VitalSigns? VitalSigns { get; set; }
        public Feedback? Feedback { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }
        public ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public ICollection<ServiceOrder> ServiceOrders { get; set; } = new List<ServiceOrder>();

    }
}
