using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects.Entities
{
    public class PrescriptionDetail
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("MedicalRecord")]
        public int MedicalRecordId { get; set; }
        [ForeignKey("Medicine")]
        public int MedicineId { get; set; }
        public int Quantity { get; set; } 
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; } 
        [Required, MaxLength(255)]
        public string DosageInstruction { get; set; } = string.Empty; 

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Paid, Dispensed
        public DateTime? DispensedAt { get; set; }
        public int? DispensedByUserId { get; set; }

        // Navigation Properties
        public MedicalRecord MedicalRecord { get; set; } = null!;
        public Medicine Medicine { get; set; } = null!;
        public User? DispensedBy { get; set; }
    }
}
