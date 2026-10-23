using System;
using System.ComponentModel.DataAnnotations;

namespace MonitoringSystem.Models
{
    public class HourlyPlanData
    {
        [Key]
        public int Id { get; set; }
        public string MachineCode { get; set; } // FINAL1, FINAL2, etc.
        public DateTime SelectedDate { get; set; }
        public int TotalPlan { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
