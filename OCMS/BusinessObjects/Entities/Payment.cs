using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects.Entities
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }
        [ForeignKey("Appointment")]
        public int AppointmentId { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        [Required, MaxLength(20)]
        public string PaymentMethod { get; set; } = "Cash"; // Cash, POS, or another recorded method
        [Required, MaxLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Success, Failed, Refunded
        [Required, MaxLength(30)]
        public string PaymentType { get; set; } = "Deposit"; // Deposit, FinalSettlement, Refund
        [MaxLength(100)]
        public string? TransactionId { get; set; } // Mã giao dịch hoặc mã phiếu thu
        public int? CashierId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // Navigation Properties
        public Appointment Appointment { get; set; } = null!;
        public User? Cashier { get; set; }
    }
}
