using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.PreviewSap
{
    // Preview isi tabel SapPlan: sumber bar biru (SAP Plan Normal / Overtime) di halaman Production Achievement
    public class IndexModel : PageModel
    {
        private readonly string? connectionString;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public IndexModel(IConfiguration configuration, IWebHostEnvironment env)
        {
            connectionString = configuration.GetConnectionString("DefaultConnection");
            _configuration = configuration;
            _env = env;
        }

        // Keterangan per model ("MESIN|NAMA" -> teks): model yang tidak ada di MasterData & belum punya alias
        // dihitung import SAP Plan dengan SUT dummy (lihat Services/SapPlanImport, opsi FallbackSut).
        public Dictionary<string, string> ModelNotes { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? NoteFor(string machineCode, string model) =>
            ModelNotes.TryGetValue(MonitoringSystem.Services.SapPlanImport.SapPlanCalculator.Key(model, machineCode), out var n) ? n : null;

        [BindProperty(SupportsGet = true)] public int SelectedMonth { get; set; } = DateTime.Now.Month;
        [BindProperty(SupportsGet = true)] public int SelectedYear { get; set; } = DateTime.Now.Year;
        [BindProperty(SupportsGet = true)] public int SelectedDay { get; set; } = 0; // 0 = semua tanggal
        [BindProperty(SupportsGet = true)] public string MachineLine { get; set; } = "All";
        [BindProperty(SupportsGet = true)] public string Shift { get; set; } = "All";

        public class SapPlanRow
        {
            public DateTime Date { get; set; }
            public string MachineCode { get; set; } = "";
            public string Model { get; set; } = "";
            public string Shift { get; set; } = "";
            public int QtyNormal { get; set; }
            public int QtyOvertime { get; set; }
            public int Total => QtyNormal + QtyOvertime;
        }

        public List<SapPlanRow> Rows { get; private set; } = new List<SapPlanRow>();
        public List<string> AvailableShifts { get; private set; } = new List<string>();
        public int DaysInMonth { get; private set; }
        public string? ErrorMessage { get; private set; }

        public static string LineName(string machineCode) => machineCode switch
        {
            "MCH1-01" => "CU",
            "MCH1-02" => "CS",
            _ => machineCode
        };

        public void OnGet()
        {
            if (SelectedMonth < 1 || SelectedMonth > 12) SelectedMonth = DateTime.Now.Month;
            if (SelectedYear < 2000) SelectedYear = DateTime.Now.Year;
            DaysInMonth = DateTime.DaysInMonth(SelectedYear, SelectedMonth);
            if (SelectedDay < 0 || SelectedDay > DaysInMonth) SelectedDay = 0;

            // Filter line sama dengan chart Production Achievement: "All" = CU + CS
            string lineFilter = MachineLine != "All"
                ? "AND sp.MachineCode = @MachineLine"
                : "AND sp.MachineCode IN ('MCH1-01', 'MCH1-02')";
            string shiftFilter = Shift != "All" ? "AND sp.Shift = @Shift" : "";
            string dayFilter = SelectedDay > 0 ? "AND DAY(pp.CurrentDate) = @SelectedDay" : "";

            string sql = $@"
                SELECT pp.CurrentDate,
                       sp.MachineCode,
                       ISNULL(sp.ProductName, '-') AS ProductName,
                       ISNULL(CAST(sp.Shift AS NVARCHAR(20)), '-') AS Shift,
                       ISNULL(sp.SapPlanNormal, 0) AS SapPlanNormal,
                       ISNULL(sp.SapPlanOvertime, 0) AS SapPlanOvertime
                FROM ProductionPlan pp
                INNER JOIN SapPlan sp ON pp.Id = sp.PlanId
                WHERE pp.CurrentDate >= DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1)
                  AND pp.CurrentDate < DATEADD(MONTH, 1, DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1))
                  {lineFilter}
                  {shiftFilter}
                  {dayFilter}
                ORDER BY pp.CurrentDate, sp.MachineCode, sp.Shift, sp.ProductName";

            string shiftSql = @"
                SELECT DISTINCT CAST(sp.Shift AS NVARCHAR(20)) AS Shift
                FROM ProductionPlan pp
                INNER JOIN SapPlan sp ON pp.Id = sp.PlanId
                WHERE pp.CurrentDate >= DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1)
                  AND pp.CurrentDate < DATEADD(MONTH, 1, DATEFROMPARTS(@SelectedYear, @SelectedMonth, 1))
                  AND sp.Shift IS NOT NULL
                ORDER BY Shift";

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                    cmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                    if (MachineLine != "All") cmd.Parameters.AddWithValue("@MachineLine", MachineLine);
                    if (Shift != "All") cmd.Parameters.AddWithValue("@Shift", Shift);
                    if (SelectedDay > 0) cmd.Parameters.AddWithValue("@SelectedDay", SelectedDay);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        Rows.Add(new SapPlanRow
                        {
                            Date = Convert.ToDateTime(reader["CurrentDate"]),
                            MachineCode = reader["MachineCode"].ToString() ?? "",
                            Model = reader["ProductName"].ToString() ?? "-",
                            Shift = reader["Shift"].ToString() ?? "-",
                            QtyNormal = Convert.ToInt32(reader["SapPlanNormal"]),
                            QtyOvertime = Convert.ToInt32(reader["SapPlanOvertime"])
                        });
                    }
                }

                using (var cmd = new SqlCommand(shiftSql, conn))
                {
                    cmd.Parameters.AddWithValue("@SelectedYear", SelectedYear);
                    cmd.Parameters.AddWithValue("@SelectedMonth", SelectedMonth);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read()) AvailableShifts.Add(reader["Shift"].ToString() ?? "");
                }

                LoadModelNotes(conn);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Gagal mengambil data SAP Plan: " + ex.Message;
            }

            // Pastikan pilihan shift standar selalu ada di dropdown
            foreach (var s in new[] { "1", "2", "3", "NS" })
                if (!AvailableShifts.Contains(s)) AvailableShifts.Add(s);
            AvailableShifts = AvailableShifts.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
        }

        // Model di tabel yang tidak ada di MasterData (nama + mesin persis) dan tidak ada di file alias -> SUT dummy
        private void LoadModelNotes(SqlConnection conn)
        {
            var fallbackSut = _configuration.GetValue("SapPlanImport:FallbackSut", 35);
            if (fallbackSut <= 0 || Rows.Count == 0) return;
            try
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new SqlCommand(@"SELECT LTRIM(RTRIM(ProductName)), LTRIM(RTRIM(MachineCode)) FROM MasterData
                                                  WHERE ProductName IS NOT NULL AND MachineCode IS NOT NULL AND SUT > 0", conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) known.Add(MonitoringSystem.Services.SapPlanImport.SapPlanCalculator.Key(reader.GetString(0), reader.GetString(1)));

                var aliasFile = _configuration["SapPlanImport:AliasFile"] ?? "sapplan-aliases.json";
                var aliasPath = Path.Combine(_env.ContentRootPath, aliasFile);
                if (System.IO.File.Exists(aliasPath))
                    foreach (var a in MonitoringSystem.Services.SapPlanImport.SapPlanImportRunner.LoadAliases(aliasPath))
                        known.Add(MonitoringSystem.Services.SapPlanImport.SapPlanCalculator.Key(a.ExcelName, a.MachineCode));

                foreach (var r in Rows)
                {
                    var key = MonitoringSystem.Services.SapPlanImport.SapPlanCalculator.Key(r.Model, r.MachineCode);
                    if (!known.Contains(key))
                        ModelNotes[key] = MonitoringSystem.Services.SapPlanImport.SapPlanCalculator.DummyNote(fallbackSut);
                }
            }
            catch
            {
                // Keterangan hanya pelengkap: kalau gagal dibaca, tabel tetap tampil tanpa keterangan
            }
        }
    }
}
