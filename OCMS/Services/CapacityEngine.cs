using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public class CapacityEngine : ICapacityEngine
    {
        public ShiftCapacityMetrics CalculateShiftMetrics(IEnumerable<TimeSlot> slots)
        {
            var slotList = slots.ToList();
            int total = slotList.Count > 0 ? slotList.Count : 16;
            int bookedOrHeld = slotList.Count(s => s.Status == "Booked" || s.Status == "Held");
            int available = slotList.Count(s => s.Status == "Open");

            double percentage = total > 0 ? (double)bookedOrHeld / total * 100 : 0;

            string status;
            string badgeClass;
            string text;

            if (percentage < 50)
            {
                status = "Green";
                badgeClass = "bg-success";
                text = "Bình thường / Vắng";
            }
            else if (percentage <= 80)
            {
                status = "Orange";
                badgeClass = "bg-warning";
                text = "Đông khách";
            }
            else
            {
                status = "Red";
                badgeClass = "bg-danger";
                text = "Gần kín lịch (Hạn chế)";
            }

            return new ShiftCapacityMetrics
            {
                TotalSlots = total,
                BookedSlots = bookedOrHeld,
                AvailableSlots = available,
                LoadPercentage = percentage,
                CapacityStatus = status,
                BadgeClass = badgeClass,
                StatusText = text
            };

        }

        public List<TimeSlot> GetAlternativeSlots(List<TimeSlot> allSlots, int conflictSlotId, int limit = 3){
            var conflictSlot = allSlots.FirstOrDefault(s => s.Id == conflictSlotId);
            var openSlots = allSlots.Where(s => s.Status == "Open" && s.Id != conflictSlotId).ToList();

            if(conflictSlot == null){
                return openSlots.Take(limit).ToList();
            }

            return openSlots.OrderBy(s => Math.Abs((s.StartTime - conflictSlot.StartTime).TotalMinutes)).Take(limit).ToList();
        }
    }
}
