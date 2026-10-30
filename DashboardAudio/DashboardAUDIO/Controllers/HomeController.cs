using DashboardAUDIO.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DashboardAUDIO.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;

        public HomeController(IConfiguration configuration, ILogger<HomeController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public IActionResult Index() { return View(); }

        private readonly List<(TimeSpan Start, TimeSpan End)> RegularDayBreakTimes = new()
        {
            (new TimeSpan(9, 30, 0),  new TimeSpan(9, 35, 0)),
            (new TimeSpan(11, 40, 0), new TimeSpan(12, 25, 0)),
            (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0))
        };
        private readonly List<(TimeSpan Start, TimeSpan End)> FridayBreakTimes = new()
        {
            (new TimeSpan(9, 30, 0),  new TimeSpan(9, 35, 0)),
            (new TimeSpan(11, 50, 0), new TimeSpan(13, 15, 0)),
            (new TimeSpan(14, 30, 0), new TimeSpan(14, 35, 0))
        };

        // ════════════════════════════════════════════════════════════
        // GET DASHBOARD DATA
        // Mengambil data murni dari database produksi Audio
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetDashboardData()
        {
            var viewModel = new DashboardViewModel();

            try
            {
                var (connection, isFallback, hostInfo) = await CreateWorkingConnectionAsync();
                viewModel.IsFallback = isFallback;
                viewModel.DataSource = hostInfo;
                viewModel.ConnectionStatusText = isFallback 
                    ? "Hotspot / Luar Kantor (Localhost Fallback)" 
                    : "Wi-Fi Internal Pabrik (137.40.93.165)";

                if (connection == null)
                {
                    PopulateEmptyLines(viewModel);
                    return Json(viewModel);
                }

                using (connection)
                {
                    // ── Assembly Achievement: 11 Line Final Assembly Audio ──
                int totalActualAll = 0, totalPlanAll = 0, dailyPlanAll = 0;

                for (int i = 1; i <= 11; i++)
                {
                    int lineActual = 0;
                    int linePlan = 0;
                    int lineDailyPlan = 0;

                    try
                    {
                        // Hari produksi 07.00 - 07.00. Actual/Target = jumlah kenaikan counter
                        // antar snapshot (aturan sama dengan Production Achievement): snapshot
                        // pembanding diambil lintas hari sehingga carryover kemarin bernilai 0,
                        // dan counter turun dianggap reset (nilai baru dihitung dari 0).
                        var sql = $@"
                            DECLARE @from DATETIME2 = DATEADD(hour, 7, CAST(CAST(DATEADD(hour, -7, GETDATE()) AS DATE) AS DATETIME2));
                            DECLARE @to DATETIME2 = DATEADD(day, 1, @from);

                            SELECT
                                SUM(CASE
                                    WHEN PrevActual IS NULL THEN 0
                                    WHEN Actual >= PrevActual THEN Actual - PrevActual
                                    ELSE Actual
                                END) AS Actual,
                                SUM(CASE
                                    WHEN PrevTarget IS NULL THEN 0
                                    WHEN Target >= PrevTarget THEN Target - PrevTarget
                                    ELSE Target
                                END) AS Target,
                                (SELECT TOP 1 DailyPlan FROM dbo.FINAL{i}
                                 WHERE DateTime >= @from AND DateTime < @to
                                 ORDER BY DateTime DESC, ID DESC) AS DailyPlan
                            FROM (
                                SELECT DateTime, Actual, Target,
                                       LAG(Actual) OVER (ORDER BY DateTime, ID) AS PrevActual,
                                       LAG(Target) OVER (ORDER BY DateTime, ID) AS PrevTarget
                                FROM (
                                    SELECT ID, DateTime, Actual, Target FROM dbo.FINAL{i}
                                    WHERE Actual IS NOT NULL AND DateTime >= @from AND DateTime < @to
                                    UNION ALL
                                    SELECT * FROM (
                                        SELECT TOP 1 ID, DateTime, Actual, Target FROM dbo.FINAL{i}
                                        WHERE Actual IS NOT NULL AND DateTime < @from
                                        ORDER BY DateTime DESC, ID DESC
                                    ) lastBefore
                                ) windowed
                            ) snapshots
                            WHERE DateTime >= @from";

                        var row = await connection.QuerySingleOrDefaultAsync<dynamic>(sql);
                        if (row != null)
                        {
                            lineActual = row.Actual != null ? (int)row.Actual : 0;
                            linePlan = row.Target != null ? (int)row.Target : 0;
                            lineDailyPlan = row.DailyPlan != null ? (int)row.DailyPlan : 0;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error query FINAL{Index}", i);
                    }

                    totalActualAll += lineActual;
                    totalPlanAll += linePlan;
                    dailyPlanAll += lineDailyPlan;

                    double speed = linePlan > 0 ? Math.Round((double)lineActual / linePlan * 100.0, 1) : 0.0;
                    
                    // Logika warna: < 80% merah, < 85% kuning, >= 85% hijau
                    string statusColor = "red";
                    string statusText = linePlan > 0 ? "Critical (<80%)" : "0% (Belum Ada Target/Actual)";

                    if (speed >= 85.0)
                    {
                        statusColor = "green";
                        statusText = "Good (≥85%)";
                    }
                    else if (speed >= 80.0)
                    {
                        statusColor = "yellow";
                        statusText = "Warning (<85%)";
                    }

                    viewModel.LineStatusList.Add(new LineStatusItem
                    {
                        LineNumber = i,
                        LineCode = $"FINAL{i}",
                        Name = $"Line {i}",
                        Actual = lineActual,
                        Plan = linePlan,
                        DailyPlan = lineDailyPlan,
                        Speed = speed,
                        StatusColor = statusColor,
                        StatusText = statusText
                    });
                }

                viewModel.TotalAll = new LineData
                {
                    TotalActual = totalActualAll,
                    TotalPlan = totalPlanAll,
                    DailyPlan = dailyPlanAll
                };

                // ── Defect by Category ──
                try
                {
                    var defectQuery = @"
                        SELECT 
                            Station AS Category, 
                            SUM(Quantity) AS Count
                        FROM dbo.DEFECTT
                        WHERE CONVERT(date, DateTime) = CONVERT(date, GETDATE())
                            AND Station IS NOT NULL
                            AND Station <> ''
                        GROUP BY Station
                        ORDER BY SUM(Quantity) DESC";

                    var defects = await connection.QueryAsync<dynamic>(defectQuery);
                    viewModel.DefectsByCategory = defects.Select(d => new DefectCategory
                    {
                        Category = (string)d.Category,
                        Count = (int)d.Count
                    }).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error query DEFECTT");
                }

                // ── Loss Time ──
                var machineCodes = new[] { "FINAL1", "FINAL2", "FINAL3", "FINAL4", "FINAL5", "FINAL6", "FINAL7", "FINAL8", "FINAL9", "FINAL10", "FINAL11" };
                var allLossTimes = new List<LossTimeData>();

                foreach (var mc in machineCodes)
                {
                    try
                    {
                        var lt = await GetLossTimeDataAsync(connection, mc);
                        allLossTimes.Add(lt);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error GetLossTime untuk {MachineCode}", mc);
                    }
                }

                if (allLossTimes.Any())
                {
                    int totalLoss = allLossTimes.Sum(l => l.LossTime);
                    int workingTime = allLossTimes.Max(l => l.WorkingTime);

                    viewModel.LossTimeCU = new LossTimeData
                    {
                        WorkingTime = workingTime,
                        LossTime = totalLoss,
                        LoadTime = Math.Max(0, workingTime - totalLoss),
                        BreakTimes = allLossTimes.First().BreakTimes,
                        HourlyEvents = allLossTimes
                            .SelectMany(l => l.HourlyEvents)
                            .GroupBy(kv => kv.Key)
                            .ToDictionary(
                                g => g.Key,
                                g => g.SelectMany(kv => kv.Value).ToList()
                            )
                    };
                }
                else
                {
                    viewModel.LossTimeCU = await GetLossTimeDataAsync(connection, "FINAL1");
                }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Koneksi database gagal atau tidak tersedia: {Message}", ex.Message);
                PopulateEmptyLines(viewModel);
            }

            return Json(viewModel);
        }

        private static bool? _isServerAvailable = null;
        private static DateTime _lastServerProbe = DateTime.MinValue;

        private async Task<(SqlConnection? Connection, bool IsFallback, string HostInfo)> CreateWorkingConnectionAsync()
        {
            string serverConn = _configuration.GetConnectionString("ServerConnection") 
                                ?? _configuration.GetConnectionString("DefaultConnection") 
                                ?? "Server=137.40.93.165;Database=AUDIO_PRODUCTION;User Id=sa;Password=sa;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False;Connection Timeout=5;";

            string localConn = _configuration.GetConnectionString("LocalConnection") 
                               ?? "Server=localhost\\SQLEXPRESS;Database=AUDIO_PRODUCTION;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connection Timeout=5;";

            serverConn = EnsureTimeout(serverConn, 5);
            localConn = EnsureTimeout(localConn, 5);

            // Cek apakah server pabrik perlu di-probe (jika offline, re-probe setiap 60 detik)
            bool shouldProbeServer = true;
            if (_isServerAvailable == false && (DateTime.UtcNow - _lastServerProbe).TotalSeconds < 60)
            {
                shouldProbeServer = false;
            }

            if (shouldProbeServer)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var conn = new SqlConnection(serverConn);
                    await conn.OpenAsync(cts.Token);

                    _isServerAvailable = true;
                    _lastServerProbe = DateTime.UtcNow;
                    _logger.LogInformation("[SMART FALLBACK] Tersambung ke Wi-Fi Internal Pabrik. Menggunakan database server: {Conn}", MaskConnectionString(serverConn));
                    return (conn, false, "137.40.93.165 (Wi-Fi Pabrik)");
                }
                catch (Exception ex)
                {
                    _isServerAvailable = false;
                    _lastServerProbe = DateTime.UtcNow;
                    _logger.LogWarning("[SMART FALLBACK] Server 137.40.93.165 tidak terjangkau dalam 5 detik ({Msg}). Otomatis beralih ke database lokal localhost\\SQLEXPRESS...", ex.Message);
                }
            }

            // Fallback ke Local SQLEXPRESS
            if (!string.IsNullOrEmpty(localConn))
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var conn = new SqlConnection(localConn);
                    await conn.OpenAsync(cts.Token);

                    _logger.LogInformation("[SMART FALLBACK] Berhasil menggunakan database lokal localhost\\SQLEXPRESS (Smart Fallback)");
                    return (conn, true, "Localhost SQLEXPRESS (Smart Fallback)");
                }
                catch (Exception ex)
                {
                    _logger.LogError("[SMART FALLBACK] Database lokal fallback juga gagal dihubungi: {Msg}", ex.Message);
                }
            }

            return (null, true, "Offline (Tidak Terhubung)");
        }

        private static string EnsureTimeout(string connStr, int timeoutSeconds)
        {
            if (string.IsNullOrWhiteSpace(connStr)) return connStr;
            if (connStr.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase) || 
                connStr.Contains("Connection Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return connStr;
            }
            return connStr.TrimEnd(';') + $";Connection Timeout={timeoutSeconds};";
        }

        private static void PopulateEmptyLines(DashboardViewModel viewModel)
        {
            if (viewModel.LineStatusList.Count == 0)
            {
                for (int i = 1; i <= 11; i++)
                {
                    viewModel.LineStatusList.Add(new LineStatusItem
                    {
                        LineNumber = i,
                        LineCode = $"FINAL{i}",
                        Name = $"Line {i}",
                        Actual = 0,
                        Plan = 0,
                        DailyPlan = 0,
                        Speed = 0.0,
                        StatusColor = "red",
                        StatusText = "0% (Kosong)"
                    });
                }
            }
        }

        private static string MaskConnectionString(string connStr)
        {
            return System.Text.RegularExpressions.Regex.Replace(connStr, @"Password=[^;]+", "Password=***");
        }

        // ════════════════════════════════════════════════════════════
        // GET MACHINE EFFICIENCY FROM DB
        // ════════════════════════════════════════════════════════════
        [HttpGet]
        public IActionResult GetMachineEfficiencyFromDB([FromQuery] int? month, [FromQuery] int? year)
        {
            try
            {
                var connStr = _configuration.GetConnectionString("PwkMachineConnection");
                if (string.IsNullOrEmpty(connStr))
                    return Ok(new { data = new List<object>() });

                connStr = EnsureTimeout(connStr, 5);

                // Jika server utama sedang offline (fallback mode), langsung lewati atau gunakan lokal agar tidak lag
                if (_isServerAvailable == false)
                {
                    return Ok(new { data = new List<object>(), note = "Server offline (Fallback mode active)" });
                }

                using var conn = new SqlConnection(connStr);
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    conn.Open();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("GetMachineEfficiencyFromDB: Koneksi ke machine DB ({Conn}) tidak tersedia: {Message}", MaskConnectionString(connStr), ex.Message);
                    return Ok(new { data = new List<object>() });
                }

                var query = @"
                    SELECT DISTINCT
                        [Machine] AS MachineName
                    FROM [dbo].[Reports]
                    WHERE [ReportDate] = CONVERT(date, GETDATE())
                    ORDER BY [Machine]";

                using var cmd = new SqlCommand(query, conn);
                cmd.CommandTimeout = 30;
                using var reader = cmd.ExecuteReader();

                var result = new List<object>();
                while (reader.Read())
                {
                    result.Add(new
                    {
                        machineName = reader["MachineName"]?.ToString() ?? "-",
                        oEE = (double?)null,
                        operatingRatio = (double?)null,
                        ability = (double?)null,
                        quality = (double?)null
                    });
                }

                return Ok(new { data = result, rowCount = result.Count });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetMachineEfficiencyFromDB: {Message}", ex.Message);
                return Ok(new { data = new List<object>() });
            }
        }

        // ════════════════════════════════════════════════════════════
        // PRIVATE: GET LOSS TIME DATA
        // ════════════════════════════════════════════════════════════
        private async Task<LossTimeData> GetLossTimeDataAsync(SqlConnection connection, string machineCode)
        {
            var data = new LossTimeData();

            TimeSpan workDayStart = new TimeSpan(7, 7, 0);
            TimeSpan workDayEnd = new TimeSpan(15, 55, 0);

            var today = DateTime.Now;
            var breakTimes = today.DayOfWeek == DayOfWeek.Friday ? FridayBreakTimes : RegularDayBreakTimes;

            data.BreakTimes = breakTimes.Select(b => new BreakTime
            {
                Start = b.Start,
                End = b.End
            }).ToList();

            var currentTime = today.TimeOfDay;
            if (currentTime > workDayStart)
            {
                var effectiveEnd = currentTime > workDayEnd ? workDayEnd : currentTime;
                int totalMinutes = (int)(effectiveEnd - workDayStart).TotalMinutes;
                int totalRest = breakTimes.Sum(b =>
                {
                    if (effectiveEnd <= b.Start || workDayStart >= b.End) return 0;
                    var rs = b.Start > workDayStart ? b.Start : workDayStart;
                    var re = b.End < effectiveEnd ? b.End : effectiveEnd;
                    return (int)Math.Max(0, (re - rs).TotalMinutes);
                });
                data.WorkingTime = Math.Max(0, totalMinutes - totalRest);
            }

            for (int h = 7; h < 16; h++)
                data.HourlyEvents[h] = new List<LossEvent>();

            int totalLossSeconds = 0;

            var lossQuery = @"
                SELECT
                    DATEPART(hour,   [Time]) AS Hour,
                    DATEPART(minute, [Time]) AS StartMinute,
                    [LossTime]               AS DurationSeconds
                FROM dbo.AssemblyLossTime
                WHERE CONVERT(date, [Time]) = CONVERT(date, GETDATE())
                  AND [MachineCode] = @MachineCode";

            var allLossEvents = await connection.QueryAsync<dynamic>(
                lossQuery, new { MachineCode = machineCode });

            foreach (var ev in allLossEvents)
            {
                var eventTime = new TimeSpan((int)ev.Hour, (int)ev.StartMinute, 0);
                bool isDuringBreak = breakTimes.Any(b => eventTime >= b.Start && eventTime < b.End);
                if (isDuringBreak) continue;

                int hour = (int)ev.Hour;
                if (data.HourlyEvents.ContainsKey(hour))
                    data.HourlyEvents[hour].Add(new LossEvent
                    {
                        StartMinute = (int)ev.StartMinute,
                        DurationMinutes = (int)Math.Ceiling((decimal)ev.DurationSeconds / 60)
                    });

                totalLossSeconds += (int)ev.DurationSeconds;
            }

            data.LossTime = totalLossSeconds / 60;
            data.LoadTime = Math.Max(0, data.WorkingTime - data.LossTime);

            return data;
        }
    }
}
