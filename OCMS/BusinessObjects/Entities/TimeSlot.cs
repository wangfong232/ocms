using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessObjects.Entities
{
    public class TimeSlot
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("DoctorSchedule")]
        public int ScheduleId { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Open"; // Open, Hold, Booked

        public DateTime? HoldExpiresAt { get; set; }

        //Concurrency token
        [Timestamp]
        public byte[] RowVersion { get; set; } = null!;

        // Navigation Properties
        public DoctorSchedule DoctorSchedule { get; set; } = null!;
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
