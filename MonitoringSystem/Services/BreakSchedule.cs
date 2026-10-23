using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Services
{
    public class BreakTimeInfo
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Aturan jadwal istirahat Panamon Audio (BreakTimeService).
    /// 1. Jadwal custom tanggal itu (dbo.AdditionalBreakTimes) kalau ada.
    /// 2. Jadwal bawaan sesuai standar Audio (RestTime):
    ///    - Morning: 09:30 - 10:00
    ///    - Lunch: 12:00 - 13:00 (Jumat: 11:30 - 13:00)
    ///    - Afternoon: 15:30 - 16:00
    /// </summary>
    public static class BreakSchedule
    {
        public const string CustomBreakSql = @"
            SELECT StartTime, EndTime, Reason FROM dbo.AdditionalBreakTimes WHERE [Date] = @date ORDER BY StartTime";

        public const string ShiftModeSql = @"
            SELECT 'SHIFT 1' AS ShiftMode";

        public static bool IsValidCustom(TimeSpan start, TimeSpan end, string? reason) =>
            start != end && !string.IsNullOrWhiteSpace(reason);

        public static List<BreakTimeInfo> Build(DateOnly date, IEnumerable<BreakTimeInfo> custom, IEnumerable<string> shiftModes)
        {
            var validCustom = custom.Where(b => IsValidCustom(b.StartTime, b.EndTime, b.Reason)).OrderBy(b => b.StartTime).ToList();
            if (validCustom.Count > 0) return validCustom;

            var modes = shiftModes.Select(s => (s ?? "").ToUpperInvariant()).ToList();
            bool hasShift1 = modes.Any(s => s.Contains("SHIFT 1"));
            bool hasShift2 = modes.Any(s => s.Contains("SHIFT 2"));
            bool hasShift3 = modes.Any(s => s.Contains("SHIFT 3"));
            bool hasNonShift = modes.Any(s => s.Contains("NON-SHIFT"));

            var isFriday = date.DayOfWeek == DayOfWeek.Friday;
            var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

            var generated = new List<BreakTimeInfo>();

            // Default Day / Regular / Shift 1
            if (hasShift1 || hasNonShift || (!hasShift2 && !hasShift3))
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(10, 0, 0), Reason = "Morning Break" });

                if (isFriday)
                {
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(11, 30, 0), EndTime = new TimeSpan(13, 0, 0), Reason = "Friday Lunch & Prayer" });
                }
                else
                {
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(12, 0, 0), EndTime = new TimeSpan(13, 0, 0), Reason = "Lunch Break" });
                }

                if (!isWeekend)
                {
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(15, 30, 0), EndTime = new TimeSpan(16, 0, 0), Reason = "Afternoon Break" });
                }
            }

            if (hasShift2)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(18, 0, 0), EndTime = new TimeSpan(18, 30, 0), Reason = "Dinner Break" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(21, 0, 0), EndTime = new TimeSpan(21, 15, 0), Reason = "Shift 2 Break" });
            }

            if (hasShift3)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(1, 0, 0), EndTime = new TimeSpan(1, 30, 0), Reason = "Midnight Break" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(4, 0, 0), EndTime = new TimeSpan(4, 30, 0), Reason = "Early Morning Rest" });
            }

            return generated.OrderBy(g => g.StartTime).ToList();
        }

        public static List<BreakTimeInfo> Load(SqlConnection conn, DateTime date)
        {
            var custom = new List<BreakTimeInfo>();
            using (var cmd = new SqlCommand(CustomBreakSql, conn))
            {
                cmd.Parameters.Add("@date", System.Data.SqlDbType.Date).Value = date.Date;
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    custom.Add(new BreakTimeInfo { StartTime = r.GetTimeSpan(0), EndTime = r.GetTimeSpan(1), Reason = r.IsDBNull(2) ? "" : r.GetString(2) });
            }
            return Build(DateOnly.FromDateTime(date), custom, new[] { "SHIFT 1" });
        }

        public static List<(DateTime Start, DateTime End)> ForProductionDay(DateTime productionDate, IEnumerable<BreakTimeInfo> breaks)
        {
            var result = new List<(DateTime, DateTime)>();
            foreach (var b in breaks)
            {
                var start = productionDate.Date + b.StartTime;
                if (b.StartTime < new TimeSpan(7, 0, 0)) start = start.AddDays(1);
                var duration = b.EndTime - b.StartTime;
                if (duration < TimeSpan.Zero) duration += TimeSpan.FromDays(1);
                if (duration > TimeSpan.Zero) result.Add((start, start + duration));
            }
            return result;
        }

        public static double OverlapMinutes(DateTime start, DateTime end, IEnumerable<(DateTime Start, DateTime End)> breaks)
        {
            double minutes = 0;
            foreach (var (bs, be) in breaks)
            {
                var from = start > bs ? start : bs;
                var to = end < be ? end : be;
                if (to > from) minutes += (to - from).TotalMinutes;
            }
            return minutes;
        }
    }
}
