using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MonitoringSystem.Models;

namespace MonitoringSystem.Pages.Inventory
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class indexModel : PageModel
    {
        private readonly ScaffoldedDbContext _context;
        private readonly ILogger<indexModel> _logger;

        public indexModel(ScaffoldedDbContext context, ILogger<indexModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ── Filter Bulan & Tahun terpisah ────────────────────────
        [BindProperty(SupportsGet = true)]
        public int? FilterBulan { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? FilterTahun { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterMachineLine { get; set; }

        // Bulan & tahun yang benar-benar dipakai (fallback ke bulan/tahun sekarang)
        public int ActiveBulan { get; set; }
        public int ActiveYear { get; set; }

        public List<InventoryData> listData { get; set; } = new();

        // ─── GET ───────────────────────────────────────────────
        public async Task OnGetAsync()
        {
            listData = new List<InventoryData>();

            try
            {
                // ── Tentukan bulan & tahun aktif ──────────────────
                var now = DateTime.Now;
                ActiveBulan = (FilterBulan.HasValue && FilterBulan.Value >= 1 && FilterBulan.Value <= 12)
                    ? FilterBulan.Value
                    : now.Month;

                ActiveYear = (FilterTahun.HasValue && FilterTahun.Value >= 2000 && FilterTahun.Value <= 2100)
                    ? FilterTahun.Value
                    : now.Year;

                var startDate = new DateTime(ActiveYear, ActiveBulan, 1);
                var endDate = startDate.AddMonths(1);

                var audioLines = new List<string> { "FINAL1", "FINAL2", "FINAL3", "FINAL4", "FINAL5", "FINAL7", "FINAL8", "FINAL9", "FINAL10" };
                if (!string.IsNullOrEmpty(FilterMachineLine) && FilterMachineLine != "All")
                {
                    audioLines = audioLines.Where(l => l.Equals(FilterMachineLine, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (audioLines.Count == 0)
                {
                    listData = new List<InventoryData>();
                    return;
                }

                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();

                var lineQueries = new List<string>();
                foreach (var line in audioLines)
                {
                    lineQueries.Add($@"
                        SELECT 
                            RTRIM(Model) AS Data_Id,
                            RTRIM(Model) AS Model,
                            '{line}' AS MachineCode,
                            CASE 
                                WHEN CAST(DateTime AS TIME) < '07:00:00' 
                                THEN DAY(DATEADD(day, -1, CAST(DateTime AS DATE)))
                                ELSE DAY(CAST(DateTime AS DATE))
                            END AS DayNum,
                            ISNULL(MAX(Actual) - MIN(Actual), 0) AS Actual
                        FROM dbo.[{line}]
                        WHERE DateTime >= DATEADD(hour, 7, @startDate)
                          AND DateTime < DATEADD(hour, 7, @endDate)
                          AND Model IS NOT NULL AND LTRIM(RTRIM(Model)) <> ''
                        GROUP BY 
                            Model,
                            CASE 
                                WHEN CAST(DateTime AS TIME) < '07:00:00' 
                                THEN DAY(DATEADD(day, -1, CAST(DateTime AS DATE)))
                                ELSE DAY(CAST(DateTime AS DATE))
                            END");
                }

                cmd.CommandText = $@"
                    WITH CombinedAudioOutput AS (
                        {string.Join(" UNION ALL ", lineQueries)}
                    )
                    SELECT 
                        Data_Id,
                        Model,
                        MachineCode,
                        DayNum,
                        SUM(Actual) AS Actual
                    FROM CombinedAudioOutput
                    GROUP BY Data_Id, Model, MachineCode, DayNum
                    ORDER BY MachineCode, Model, DayNum";

                var pStart = cmd.CreateParameter();
                pStart.ParameterName = "@startDate";
                pStart.Value = startDate;
                cmd.Parameters.Add(pStart);

                var pEnd = cmd.CreateParameter();
                pEnd.ParameterName = "@endDate";
                pEnd.Value = endDate;
                cmd.Parameters.Add(pEnd);

                // ── Group hasil query per Product_Id + MachineCode ──
                var dataMap = new Dictionary<string, InventoryData>();

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var dataId = reader["Data_Id"]?.ToString();
                    var model = reader["Model"]?.ToString();
                    var machineLine = reader["MachineCode"]?.ToString();
                    var dayNum = Convert.ToInt32(reader["DayNum"]);
                    decimal? actual = reader["Actual"] == DBNull.Value ? null : Convert.ToDecimal(reader["Actual"]);

                    var key = $"{dataId}|{machineLine}";

                    if (!dataMap.TryGetValue(key, out var invData))
                    {
                        invData = new InventoryData
                        {
                            Data_Id = dataId,
                            Model = model,
                            MachineLine = machineLine,
                            DailyValues = new Dictionary<int, decimal?>()
                        };
                        dataMap[key] = invData;
                    }

                    invData.DailyValues[dayNum] = actual;
                }

                await conn.CloseAsync();

                listData = dataMap.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error loading Inventory data: {ex.Message}");
                TempData["StatusMessage"] = "error";
                TempData["Message"] = $"Error loading data: {ex.Message}";

                // Fallback biar view tetap bisa render walau query gagal
                if (ActiveBulan == 0) ActiveBulan = DateTime.Now.Month;
                if (ActiveYear == 0) ActiveYear = DateTime.Now.Year;
            }
        }
    }

    // ─── DATA MODEL ─────────────────────────────────────────────
    public class InventoryData
    {
        public string Data_Id { get; set; }
        public string Model { get; set; }
        public string MachineLine { get; set; }
        public Dictionary<int, decimal?> DailyValues { get; set; } = new();
    }
}
