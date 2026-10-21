using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Services
{
    public class BreakTimeInfo
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    // Aturan jadwal istirahat Panamon, SATU sumber untuk Panamon (BreakTimeService) dan PLC ROHIB / Plclogger
    // (file ini ikut dikompilasi Plclogger lewat <Compile Include> di Plclogger.csproj). Ubah jadwal hanya di sini.
    //   1. Jadwal custom tanggal itu (dbo.AdditionalBreakTimes) kalau ada.
    //   2. Selain itu jadwal bawaan sesuai shift yang jalan di line Assembly (ShiftMode OEESN MCH1-01/MCH1-02) tanggal itu.
    public static class BreakSchedule
    {
        // Istirahat tambahan pada rentang tanggal tertentu, ditambahkan ke jadwal bawaan Shift 1 / Non-Shift
        // (tidak dipakai jika tanggal itu punya jadwal custom di AdditionalBreakTimes).
        // Friday: null = semua hari, true = hanya Jumat, false = selain Jumat.
        private static readonly (DateOnly From, DateOnly To, bool? Friday, TimeSpan Start, TimeSpan End, string Reason)[] ExtraDayShiftBreaks =
        {
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), false, new TimeSpan(12, 15, 0), new TimeSpan(13, 0, 0), "Lunch"),
            (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), true, new TimeSpan(11, 40, 0), new TimeSpan(13, 5, 0), "Lunch"),
        };

        public const string CustomBreakSql = @"
            SELECT StartTime, EndTime, Reason FROM dbo.AdditionalBreakTimes WHERE [Date] = @date ORDER BY StartTime";

        public const string ShiftModeSql = @"
            SELECT DISTINCT UPPER(LTRIM(RTRIM(ShiftMode)))
            FROM [OEESN]
            WHERE MachineCode IN ('MCH1-01', 'MCH1-02')
              AND SDate >= @date
              AND SDate < DATEADD(DAY, 1, @date)
              AND SN_GOOD IS NOT NULL
              AND LTRIM(RTRIM(SN_GOOD)) <> ''
              AND ShiftMode IS NOT NULL";

        // Baris custom lama tanpa durasi / alasan diabaikan supaya jadwal bawaan tetap dipakai
        public static bool IsValidCustom(TimeSpan start, TimeSpan end, string? reason) =>
            start != end && !string.IsNullOrWhiteSpace(reason);

        // Jadwal istirahat satu tanggal: custom (yang valid) kalau ada, selain itu jadwal bawaan dari shift yang jalan
        public static List<BreakTimeInfo> Build(DateOnly date, IEnumerable<BreakTimeInfo> custom, IEnumerable<string> shiftModes)
        {
            var validCustom = custom.Where(b => IsValidCustom(b.StartTime, b.EndTime, b.Reason)).OrderBy(b => b.StartTime).ToList();
            if (validCustom.Count > 0) return validCustom;

            var modes = shiftModes.Select(s => (s ?? "").ToUpperInvariant()).ToList();
            bool hasShift1 = modes.Any(s => s.Contains("SHIFT 1"));
            bool hasShift3 = modes.Any(s => s.Contains("SHIFT 3"));
            bool hasNonShift = modes.Any(s => s.Contains("NON-SHIFT"));

            var generated = new List<BreakTimeInfo>();
            if (hasShift1 || hasNonShift)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(7, 0, 0), EndTime = new TimeSpan(7, 7, 0), Reason = "Morning Assembly" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(9, 30, 0), EndTime = new TimeSpan(9, 35, 0), Reason = "Break Time" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(14, 30, 0), EndTime = new TimeSpan(14, 35, 0), Reason = "Break Time" });

                var isFriday = date.DayOfWeek == DayOfWeek.Friday;
                foreach (var extra in ExtraDayShiftBreaks.Where(x => date >= x.From && date <= x.To && (x.Friday == null || x.Friday == isFriday)))
                    generated.Add(new BreakTimeInfo { StartTime = extra.Start, EndTime = extra.End, Reason = extra.Reason });

                if (hasShift1)
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(15, 40, 0), EndTime = new TimeSpan(15, 45, 0), Reason = "5S" });
                else if (hasNonShift)
                    generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(15, 55, 0), EndTime = new TimeSpan(16, 0, 0), Reason = "5S" });
            }

            if (hasShift3)
            {
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(23, 15, 0), EndTime = new TimeSpan(23, 22, 0), Reason = "Assembly" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(5, 0, 0), EndTime = new TimeSpan(5, 10, 0), Reason = "Rest / Pray" });
                generated.Add(new BreakTimeInfo { StartTime = new TimeSpan(6, 55, 0), EndTime = new TimeSpan(6, 59, 0), Reason = "5S" });
            }

            return generated.OrderBy(g => g.StartTime).ToList();
        }

        // Jadwal istirahat satu tanggal langsung dari database (untuk program tanpa Entity Framework, mis. Plclogger)
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
            var modes = new List<string>();
            if (!custom.Any(b => IsValidCustom(b.StartTime, b.EndTime, b.Reason)))
            {
                using var cmd = new SqlCommand(ShiftModeSql, conn);
                cmd.Parameters.Add("@date", System.Data.SqlDbType.DateTime).Value = date.Date;
                using var r = cmd.ExecuteReader();
                while (r.Read()) if (!r.IsDBNull(0)) modes.Add(r.GetString(0));
            }
            return Build(DateOnly.FromDateTime(date), custom, modes);
        }

        // Jam istirahat sebagai waktu nyata untuk satu HARI PRODUKSI (07:00 s/d 07:00 besoknya):
        // jam 00:00-06:59 = malam hari produksi itu (tanggal kalender berikutnya); istirahat yang melewati 00:00 ikut tersambung.
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

        // Menit rentang [start, end) yang jatuh di jam istirahat
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
