using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class ShiftCapacityMetrics       
    { 
        public int TotalSlots { get; set; }
        public int BookedSlots { get; set; }
        public int AvailableSlots { get; set; }
        public double LoadPercentage { get; set; }
        public string CapacityStatus { get; set; } = "Green";   //"Green": (<50%), "Orange": (50%-80%), "Red": (>80%)
        public string BadgeClass { get; set; } = "bg-success";  // "bg-success": Green, "bg-warning": Orange, "bg-danger": Red
        public string StatusText { get; set; } = "Rảnh"; // "Rảnh": Green, "Bận": Orange, "Quá tải": Red

    }
    public interface ICapacityEngine
    {
        ShiftCapacityMetrics CalculateShiftMetrics(IEnumerable<TimeSlot> slot);
        List<TimeSlot> GetAlternativeSlots(List<TimeSlot> allSlots, int conflictSlotId, int limit= 3);
    }
}
