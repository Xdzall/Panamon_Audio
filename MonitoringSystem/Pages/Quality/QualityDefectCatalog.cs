using System.Text.RegularExpressions;

namespace MonitoringSystem.Pages.Quality
{
    /// <summary>
    /// Pengelompokan data defect untuk Panamon Audio.
    /// Sumber data: dbo.DEFECTT / dbo.NG_RPTS
    /// Station: PCB, FINAL, PART, DESIGN
    /// Line: FINAL 1 s/d FINAL 11
    /// </summary>
    public static class QualityDefectCatalog
    {
        public const string OthersKey = "OTHERS";
        public const string OthersLabel = "Others";

        public record StationGroup(string Key, string Label, string[] Keywords);
        public record DefectCategory(string Key, string Label, string[] Keywords);

        public class LineDefinition
        {
            public string Code { get; init; } = "";
            public string MachineCode { get; init; } = "";
            public List<StationGroup> Stations { get; init; } = new();
            public List<DefectCategory> Categories { get; init; } = new();
        }

        private static List<StationGroup> CreateAudioStations() => new()
        {
            new("PCB", "PCB Inspection", new[] { "pcb" }),
            new("FINAL", "Final Assembly", new[] { "final", "fnl", "assy" }),
            new("PART", "Part / Trafo", new[] { "part", "trafo", "component" }),
            new("DESIGN", "Design Inspection", new[] { "design" }),
        };

        private static List<DefectCategory> CreateAudioCategories() => new()
        {
            new("ELECTRICAL", "Electrical / Function", new[] {
                "mati", "lemah", "noise", "redup", "bunyi", "suara", "short", "volt",
                "solder", "ic", "baterai", "esd", "earphone", "ac mati", "led", "am_mw", "fm"
            }),
            new("APPEARANCE", "Appearance / Cosmetic", new[] {
                "baret", "kotor", "cacat", "pecah", "retak", "gores", "miring", "longgar",
                "panel", "cabinet", "front cabinet", "label"
            }),
            new("MECHANICAL", "Mechanical / Assembly", new[] {
                "lepas", "kurang", "salah", "terbalik", "terjepit", "baut", "screw", "knob", "jack", "antenna"
            }),
            new("OTHERS", "Other Defects", Array.Empty<string>())
        };

        public static readonly LineDefinition Final1 = new() { Code = "FINAL 1", MachineCode = "FINAL1", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final2 = new() { Code = "FINAL 2", MachineCode = "FINAL2", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final3 = new() { Code = "FINAL 3", MachineCode = "FINAL3", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final4 = new() { Code = "FINAL 4", MachineCode = "FINAL4", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final5 = new() { Code = "FINAL 5", MachineCode = "FINAL5", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final6 = new() { Code = "FINAL 6", MachineCode = "FINAL6", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final7 = new() { Code = "FINAL 7", MachineCode = "FINAL7", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final8 = new() { Code = "FINAL 8", MachineCode = "FINAL8", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final9 = new() { Code = "FINAL 9", MachineCode = "FINAL9", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final10 = new() { Code = "FINAL 10", MachineCode = "FINAL10", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };
        public static readonly LineDefinition Final11 = new() { Code = "FINAL 11", MachineCode = "FINAL11", Stations = CreateAudioStations(), Categories = CreateAudioCategories() };

        public static readonly IReadOnlyList<LineDefinition> Lines = new[]
        {
            Final1, Final2, Final3, Final4, Final5,
            Final6, Final7, Final8, Final9, Final10, Final11
        };

        public static LineDefinition ResolveLine(string? lineOrMachineCode)
        {
            var value = (lineOrMachineCode ?? "").Trim().Replace(" ", "").ToUpperInvariant();
            if (string.IsNullOrEmpty(value)) return Final1;

            return Lines.FirstOrDefault(l =>
                       l.MachineCode.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                       l.Code.Replace(" ", "").Equals(value, StringComparison.OrdinalIgnoreCase))
                   ?? Final1;
        }

        public static StationGroup ResolveStation(LineDefinition line, string? rawStation)
        {
            var text = Normalize(rawStation);
            if (text.Length > 0)
            {
                foreach (var station in line.Stations)
                {
                    if (station.Keywords.Any(k => text.Contains(k)))
                    {
                        return station;
                    }
                }
            }

            return new StationGroup(OthersKey, OthersLabel, Array.Empty<string>());
        }

        public static DefectCategory ResolveCategory(LineDefinition line, string? cause, string? detail)
        {
            var text = Normalize($"{cause} {detail}");
            if (text.Length > 0)
            {
                foreach (var cat in line.Categories)
                {
                    if (cat.Keywords.Length > 0 && cat.Keywords.Any(k => text.Contains(k)))
                    {
                        return cat;
                    }
                }
            }

            return line.Categories.FirstOrDefault(c => c.Keywords.Length == 0)
                   ?? new DefectCategory(OthersKey, OthersLabel, Array.Empty<string>());
        }

        public static string ItemLabel(string? primary, string? fallback)
        {
            var p = (primary ?? "").Trim();
            if (!string.IsNullOrEmpty(p)) return p;
            var f = (fallback ?? "").Trim();
            return !string.IsNullOrEmpty(f) ? f : "-";
        }

        private static string Normalize(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            return Regex.Replace(s.ToLowerInvariant(), @"\s+", " ").Trim();
        }

        public static int FiscalYearOf(DateTime date) => date.Month < 4 ? date.Year - 1 : date.Year;

        public static List<DateTime> FiscalMonths(int fiscalYear)
        {
            var list = new List<DateTime>();
            for (int m = 4; m <= 12; m++) list.Add(new DateTime(fiscalYear, m, 1));
            for (int m = 1; m <= 3; m++) list.Add(new DateTime(fiscalYear + 1, m, 1));
            return list;
        }

        public static bool IsDispose(string? actionDefect)
        {
            var text = Normalize(actionDefect);
            return text.Contains("dispose") || text.Contains("change product") || text.Contains("scrap");
        }

        public static bool IsRepair(string? actionDefect)
        {
            var text = Normalize(actionDefect);
            return text.Contains("repair") || text.Contains("rework") || text.Contains("perbaiki");
        }
    }
}
