namespace DashboardAUDIO.Models
{
    public class BreakTime
    {
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
    }

    public class DefectCategory
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class LineStatusItem
    {
        public int LineNumber { get; set; }
        public string LineCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Actual { get; set; }
        public int Plan { get; set; }
        public int DailyPlan { get; set; }
        public double Speed { get; set; }
        public string StatusColor { get; set; } = "green"; // green, yellow, red
        public string StatusText { get; set; } = "Good";
    }

    public class DashboardViewModel
    {
        public LineData LineCU { get; set; } = new LineData();
        public LineData LineCS { get; set; } = new LineData();
        public LossTimeData LossTimeCU { get; set; } = new LossTimeData();
        public LossTimeData LossTimeCS { get; set; } = new LossTimeData();
        public LineData TotalAll { get; set; } = new LineData();
        public List<DefectCategory> DefectsByCategory { get; set; } = new();
        public List<LineStatusItem> LineStatusList { get; set; } = new();

        // Smart Fallback Status
        public string DataSource { get; set; } = "137.40.93.165";
        public string ConnectionStatusText { get; set; } = "Wi-Fi Internal Pabrik";
        public bool IsFallback { get; set; } = false;
    }
}
