using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects.Entities
{
    public class ServiceOrder
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Appointment")]
        public int AppointmentId { get; set; }
        [ForeignKey("MedicalService")]
        public int ServiceId { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } 
        [MaxLength(500)]
        public string? ResultSummary { get; set; } 
        [Required, MaxLength(20)]
        public string Status { get; set; } = "Ordered"; // Ordered, Completed, Paid
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Navigation Properties
        public Appointment Appointment { get; set; } = null!;
        public MedicalService MedicalService { get; set; } = null!;
    }
}
