using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;
using System.Data;

namespace MonitoringSystem.Pages.Summary
{
    public class RawOeeData 
    { 
        public DateTime SDate { get; set; } 
        public string MachineCode { get; set; } = ""; 
        public string ProductName { get; set; } = ""; 
        public int SUT { get; set; } 
        public int NoOfOperator { get; set; } 
        public decimal TargetUnit { get; set; } 
        public int Actual { get; set; }
        public int DailyPlan { get; set; }
    }

    public class DailyPlanQty
    {
        public decimal Quantity { get; set; }
        public string MachineCode { get; set; } = "";
    }

    public class RawNgData 
    { 
        public DateTime SDate { get; set; } 
        public string MachineCode { get; set; } = ""; 
    }

    public class RawRestTimeData 
    { 
        public string DayType { get; set; } = ""; 
        public TimeSpan StartTime { get; set; } 
        public TimeSpan EndTime { get; set; } 
    }

    public class RawLossTimeData 
    { 
        public DateTime Date { get; set; } 
        public TimeSpan StartTime { get; set; } 
        public TimeSpan EndTime { get; set; } 
        public string MachineCode { get; set; } = ""; 
    }

    public class ShiftLineSummary
    {
        public int ShiftNumber { get; set; }
        public string MachineCode { get; set; } = "";
        public string LineName { get; set; } = "";
        public long DailyPlan { get; set; }
        public long Plan { get; set; }
        public long Actual { get; set; }
        public long Difference => Actual - Plan;
        public double LossTimeMinutes { get; set; }
        public double WorkTimeMinutes { get; set; }
        public DateTime? ModelStartTime { get; set; }
        public DateTime? ModelEndTime { get; set; }
        public double OprRatio => (WorkTimeMinutes > 0) ? Math.Max(0, Math.Min(100.0, Math.Round(((WorkTimeMinutes - LossTimeMinutes) / WorkTimeMinutes) * 100, 2))) : 100.0;
        public double WorkEff => (Plan > 0) ? Math.Round(((double)Actual / Plan) * 100, 2) : (Actual > 0 ? 100.0 : 0.0);
        public long DefectQty { get; set; }
        public double DefectRatio => (Actual + DefectQty > 0) ? Math.Max(0, Math.Min(100.0, Math.Round((1.0 - ((double)DefectQty / (Actual + DefectQty))) * 100, 2))) : 100.0;
        public double TotalEff => Math.Round((OprRatio / 100.0) * (WorkEff / 100.0) * (DefectRatio / 100.0) * 100.0, 2);
        public double Pph { get; set; }
        public int ModelChanges { get; set; }
        public double ModelTimeChange { get; set; }
    }

    public class OverallLineSummary
    {
        public string MachineCode { get; set; } = "";
        public string LineName { get; set; } = "";
        public long DailyPlan { get; set; }
        public long Plan { get; set; }
        public long Actual { get; set; }
        public long Difference => Actual - Plan;
        public double LossTimeMinutes { get; set; }
        public double WorkTimeMinutes { get; set; }
        public double OprRatio => (WorkTimeMinutes > 0) ? Math.Max(0, Math.Min(100.0, Math.Round(((WorkTimeMinutes - LossTimeMinutes) / WorkTimeMinutes) * 100, 2))) : 100.0;
        public double WorkEff => (Plan > 0) ? Math.Round(((double)Actual / Plan) * 100, 2) : (Actual > 0 ? 100.0 : 0.0);
        public long DefectQty { get; set; }
        public double DefectRatio => (Actual + DefectQty > 0) ? Math.Max(0, Math.Min(100.0, Math.Round((1.0 - ((double)DefectQty / (Actual + DefectQty))) * 100, 2))) : 100.0;
        public double TotalEff => Math.Round((OprRatio / 100.0) * (WorkEff / 100.0) * (DefectRatio / 100.0) * 100.0, 2);
        public int ModelChanges { get; set; }
        public double ModelTimeChange { get; set; }
    }

    public class SummaryModel : PageModel
    {
        public static readonly string[] AudioLineCodes = 
        { 
            "FINAL1", "FINAL2", "FINAL3", "FINAL4", "FINAL5", 
            "FINAL6", "FINAL7", "FINAL8", "FINAL9", "FINAL10", "FINAL11" 
        };

        public static readonly Dictionary<string, string> AudioLineNames = new Dictionary<string, string>
        {
            { "FINAL1", "FINAL 1" },
            { "FINAL2", "FINAL 2" },
            { "FINAL3", "FINAL 3" },
            { "FINAL4", "FINAL 4" },
            { "FINAL5", "FINAL 5" },
            { "FINAL6", "FINAL 6" },
            { "FINAL7", "FINAL 7" },
            { "FINAL8", "FINAL 8" },
            { "FINAL9", "FINAL 9" },
            { "FINAL10", "FINAL 10" },
            { "FINAL11", "FINAL 11" }
        };

        public string connectionString = "Server=localhost\\SQLEXPRESS;Database=AUDIO_PRODUCTION;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False";
        public string errorMessage = "";

        // Properti publik untuk menampung semua data mentah
        public List<RawOeeData> RawOeeData { get; private set; } = new();
        public List<RawNgData> RawNgData { get; private set; } = new();
        public List<DailyPlanQty> DailyPlanData { get; private set; } = new();
        public List<PlanQty> PlanData { get; private set; } = new();
        public List<RawRestTimeData> RawRestTimeData { get; private set; } = new();
        public List<RawLossTimeData> RawLossTimeData { get; private set; } = new();

        // Koleksi summary yang sudah dihitung per lini Audio
        public List<ShiftLineSummary> Shift1Summaries { get; private set; } = new();
        public List<ShiftLineSummary> Shift2Summaries { get; private set; } = new();
        public List<ShiftLineSummary> Shift3Summaries { get; private set; } = new();
        public List<OverallLineSummary> OverallSummaries { get; private set; } = new();

        public SummaryModel()
        {
        }

        [BindProperty] public DateTime StartSelectedDate { get; set; } = DateTime.Today;
        [BindProperty] public DateTime EndSelectedDate { get; set; } = DateTime.Today;

        private void InitializePage() 
        { 
            LoadRawDataForDateRange(); 
            CalculateAllSummaries();
        }

        public void OnGet() 
        { 
            StartSelectedDate = DateTime.Today; 
            EndSelectedDate = DateTime.Today; 
            InitializePage(); 
        }

        public void OnPost() 
        { 
            if (EndSelectedDate < StartSelectedDate) 
            { 
                EndSelectedDate = StartSelectedDate; 
            } 
            InitializePage(); 
        }

        private void LoadRawDataForDateRange()
        {
            var globalStartTime = StartSelectedDate.Date;
            var globalEndTime = EndSelectedDate.Date.AddDays(1).AddHours(7);

            RawOeeData.Clear();
            RawNgData.Clear();
            PlanData.Clear();
            RawRestTimeData.Clear();
            RawLossTimeData.Clear();
            DailyPlanData.Clear();

            try
            {
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();

                    // Query 1: Data produksi & Target langsung dari seluruh 11 lini Audio (FINAL1..11)
                    string oeeSql = @"
                        SELECT DateTime AS SDate, 'FINAL1' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL1 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL2' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL2 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL3' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 25) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL3 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL4' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 40) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL4 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL5' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 40) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL5 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL6' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL6 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL7' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL7 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL8' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL8 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL9' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL9 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL10' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 25) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL10 WHERE DateTime >= @StartTime AND DateTime < @EndTime
                        UNION ALL
                        SELECT DateTime AS SDate, 'FINAL11' AS MachineCode, ISNULL(Model, 'Audio Model') AS ProductName, ISNULL(Sut, 30) AS SUT, 12 AS NoOfOperator, ISNULL(Target, 0) AS TargetUnit, ISNULL(Actual, 0) AS Actual, ISNULL(DailyPlan, 0) AS DailyPlan FROM dbo.FINAL11 WHERE DateTime >= @StartTime AND DateTime < @EndTime";

                    using (SqlCommand cmd = new SqlCommand(oeeSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@StartTime", globalStartTime);
                        cmd.Parameters.AddWithValue("@EndTime", globalEndTime);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RawOeeData.Add(new RawOeeData
                                {
                                    SDate = reader.GetDateTime(0),
                                    MachineCode = reader.GetString(1),
                                    ProductName = reader.GetString(2),
                                    SUT = reader.GetInt32(3),
                                    NoOfOperator = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                                    TargetUnit = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                                    Actual = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                                    DailyPlan = reader.IsDBNull(7) ? 0 : reader.GetInt32(7)
                                });
                            }
                        }
                    }

                    // Query 2: Defect Data dari dbo.DEFECTT (Normalisasi FNL Assy X -> FINALX)
                    string ngSql = @"
                        SELECT DateTime AS SDate, 
                               REPLACE(REPLACE(MachineCode, 'FNL Assy ', 'FINAL'), ' ', '') AS MachineCode 
                        FROM dbo.DEFECTT 
                        WHERE DateTime >= @StartTime AND DateTime < @EndTime";
                    using (SqlCommand cmd = new SqlCommand(ngSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@StartTime", globalStartTime);
                        cmd.Parameters.AddWithValue("@EndTime", globalEndTime);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RawNgData.Add(new RawNgData
                                {
                                    SDate = reader.GetDateTime(0),
                                    MachineCode = reader.GetString(1)
                                });
                            }
                        }
                    }

                    // Query 3: Waktu Istirahat
                    string restTimeSql = @"SELECT DayType, StartTime, EndTime FROM RestTime";
                    using (SqlCommand cmd = new SqlCommand(restTimeSql, connection))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RawRestTimeData.Add(new RawRestTimeData
                                {
                                    DayType = reader.GetString(0),
                                    StartTime = reader.GetTimeSpan(1),
                                    EndTime = reader.GetTimeSpan(2)
                                });
                            }
                        }
                    }

                    // Query 4: Loss Time
                    string lossTimeSql = @"SELECT Date, Time, EndDateTime, MachineCode 
                                           FROM AssemblyLossTime 
                                           WHERE Date >= @StartTime AND Date < @EndTime";
                    using (SqlCommand cmd = new SqlCommand(lossTimeSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@StartTime", globalStartTime);
                        cmd.Parameters.AddWithValue("@EndTime", globalEndTime);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RawLossTimeData.Add(new RawLossTimeData
                                {
                                    Date = reader.GetDateTime(0),
                                    StartTime = reader.GetTimeSpan(1),
                                    EndTime = reader.GetTimeSpan(2),
                                    MachineCode = reader.GetString(3)
                                });
                            }
                        }
                    }

                    // Query 5: Daily Plan dari tabel ProductionRecords + ProductionPlan
                    string dailyPlanSql = @"
                        SELECT 
                            CAST(SUM(pr.Quantity) AS DECIMAL(10,0)) AS Quantity,
                            pr.MachineCode
                        FROM ProductionRecords pr
                        JOIN ProductionPlan pp ON pr.PlanId = pp.Id
                        WHERE CAST(pp.CurrentDate AS DATE) >= @StartTime
                          AND CAST(pp.CurrentDate AS DATE) < @EndTime
                        GROUP BY pr.MachineCode;
                    ";

                    using (SqlCommand cmd = new SqlCommand(dailyPlanSql, connection))
                    {
                        cmd.Parameters.AddWithValue("@StartTime", globalStartTime);
                        cmd.Parameters.AddWithValue("@EndTime", globalEndTime);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                DailyPlanData.Add(new DailyPlanQty
                                {
                                    MachineCode = reader.GetString(1),
                                    Quantity = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Gagal memuat data Audio dari database: " + ex.Message;
                Console.WriteLine("❌ ERROR di LoadRawDataForDateRange(): " + ex.ToString());
            }
        }

        public decimal GetDailyPlanForLine(string machineCode)
        {
            var dp = DailyPlanData.FirstOrDefault(p => p.MachineCode == machineCode)?.Quantity ?? 0;
            if (dp > 0) return dp;
            var row = RawOeeData.Where(d => d.MachineCode == machineCode).OrderByDescending(d => d.SDate).FirstOrDefault();
            return row != null ? row.DailyPlan : 0;
        }

        private void CalculateAllSummaries()
        {
            Shift1Summaries.Clear();
            Shift2Summaries.Clear();
            Shift3Summaries.Clear();
            OverallSummaries.Clear();

            // Inisialisasi struktur per lini untuk setiap shift
            var shift1Map = AudioLineCodes.ToDictionary(c => c, c => new ShiftLineSummary
            {
                ShiftNumber = 1,
                MachineCode = c,
                LineName = AudioLineNames.ContainsKey(c) ? AudioLineNames[c] : c,
                DailyPlan = (long)GetDailyPlanForLine(c)
            });

            var shift2Map = AudioLineCodes.ToDictionary(c => c, c => new ShiftLineSummary
            {
                ShiftNumber = 2,
                MachineCode = c,
                LineName = AudioLineNames.ContainsKey(c) ? AudioLineNames[c] : c,
                DailyPlan = (long)GetDailyPlanForLine(c)
            });

            var shift3Map = AudioLineCodes.ToDictionary(c => c, c => new ShiftLineSummary
            {
                ShiftNumber = 3,
                MachineCode = c,
                LineName = AudioLineNames.ContainsKey(c) ? AudioLineNames[c] : c,
                DailyPlan = (long)GetDailyPlanForLine(c)
            });

            var overallMap = AudioLineCodes.ToDictionary(c => c, c => new OverallLineSummary
            {
                MachineCode = c,
                LineName = AudioLineNames.ContainsKey(c) ? AudioLineNames[c] : c,
                DailyPlan = (long)GetDailyPlanForLine(c)
            });

            var StartWorkingTimeShift_01 = new TimeSpan(7, 5, 0);
            var EndWorkingTimeShift_01 = new TimeSpan(15, 55, 0);

            var StartWorkingTimeShift_02 = new TimeSpan(16, 0, 0);
            var EndWorkingTimeShift_02 = new TimeSpan(23, 15, 0);

            var StartWorkingTimeShift_03 = new TimeSpan(23, 15, 0);
            var EndWorkingTimeShift_03 = new TimeSpan(7, 0, 0);

            for (var day = StartSelectedDate.Date; day <= EndSelectedDate.Date; day = day.AddDays(1))
            {
                var dayType = DetermineTypeOfDay(day.DayOfWeek);
                if (dayType == "WEEKEND") continue;

                var listRest = GetRestTime(dayType);

                // --- SHIFT 1 ---
                var s1Start = day + StartWorkingTimeShift_01;
                var s1End = day + EndWorkingTimeShift_01;
                var s1PlanData = GetPlanFromTargetUnit(s1Start, s1End);
                var s1ActData = GetActualData(s1Start, s1End);
                var s1DefData = GetTotalDefect(s1Start, s1End);
                var s1Elapsed = CalculateSingleDayElapsedWorkingTime(day, StartWorkingTimeShift_01, EndWorkingTimeShift_01, listRest);

                // --- SHIFT 2 ---
                var s2Start = day + StartWorkingTimeShift_02;
                var s2End = day + EndWorkingTimeShift_02;
                var s2PlanData = GetPlanFromTargetUnit(s2Start, s2End);
                var s2ActData = GetActualData(s2Start, s2End);
                var s2DefData = GetTotalDefect(s2Start, s2End);
                var s2Elapsed = CalculateSingleDayElapsedWorkingTime(day, StartWorkingTimeShift_02, EndWorkingTimeShift_02, listRest);

                // --- SHIFT 3 ---
                var s3Start = day + StartWorkingTimeShift_03;
                var s3End = day.AddDays(1) + EndWorkingTimeShift_03;
                var s3PlanData = GetPlanFromTargetUnit(s3Start, s3End);
                var s3ActData = GetActualData(s3Start, s3End);
                var s3DefData = GetTotalDefect(s3Start, s3End);
                var s3Elapsed = CalculateOvernightShiftElapsedWorkingTime(day, StartWorkingTimeShift_03, EndWorkingTimeShift_03, listRest);

                foreach (var code in AudioLineCodes)
                {
                    // Accumulate Shift 1
                    var s1Item = shift1Map[code];
                    var p1 = (long)(s1PlanData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var a1 = (long)(s1ActData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var d1 = (long)(s1DefData.FirstOrDefault(x => x.MachineCode == code)?.DefectQty ?? 0);
                    var l1 = CalculateTotalLossTime(code, s1Start, s1End, listRest);
                    var mc1 = GetTotalChangeModel(code, s1Start, s1End);
                    var mModels1 = GetStartEndModel(code, s1Start, s1End);
                    var mt1 = mModels1.Count > 1 ? mModels1.Zip(mModels1.Skip(1), (p, n) => (n.StartTime - p.EndTime).TotalMinutes).Sum() : 0.0;

                    s1Item.Plan += p1;
                    s1Item.Actual += a1;
                    s1Item.DefectQty += d1;
                    s1Item.LossTimeMinutes += l1;
                    s1Item.WorkTimeMinutes += (a1 > 0 || p1 > 0 ? s1Elapsed : (s1Elapsed > 0 && RawOeeData.Any(d => d.MachineCode == code && d.SDate >= s1Start && d.SDate < s1End) ? s1Elapsed : 0));
                    s1Item.ModelChanges += mc1;
                    s1Item.ModelTimeChange += mt1;
                    if (mModels1.Any() && s1Item.ModelStartTime == null) s1Item.ModelStartTime = mModels1.First().StartTime;
                    if (mModels1.Any()) s1Item.ModelEndTime = mModels1.Last().EndTime;

                    // Accumulate Shift 2
                    var s2Item = shift2Map[code];
                    var p2 = (long)(s2PlanData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var a2 = (long)(s2ActData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var d2 = (long)(s2DefData.FirstOrDefault(x => x.MachineCode == code)?.DefectQty ?? 0);
                    var l2 = CalculateTotalLossTime(code, s2Start, s2End, listRest);
                    var mc2 = GetTotalChangeModel(code, s2Start, s2End);
                    var mModels2 = GetStartEndModel(code, s2Start, s2End);
                    var mt2 = mModels2.Count > 1 ? mModels2.Zip(mModels2.Skip(1), (p, n) => (n.StartTime - p.EndTime).TotalMinutes).Sum() : 0.0;

                    s2Item.Plan += p2;
                    s2Item.Actual += a2;
                    s2Item.DefectQty += d2;
                    s2Item.LossTimeMinutes += l2;
                    s2Item.WorkTimeMinutes += (a2 > 0 || p2 > 0 ? s2Elapsed : (s2Elapsed > 0 && RawOeeData.Any(d => d.MachineCode == code && d.SDate >= s2Start && d.SDate < s2End) ? s2Elapsed : 0));
                    s2Item.ModelChanges += mc2;
                    s2Item.ModelTimeChange += mt2;
                    if (mModels2.Any() && s2Item.ModelStartTime == null) s2Item.ModelStartTime = mModels2.First().StartTime;
                    if (mModels2.Any()) s2Item.ModelEndTime = mModels2.Last().EndTime;

                    // Accumulate Shift 3
                    var s3Item = shift3Map[code];
                    var p3 = (long)(s3PlanData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var a3 = (long)(s3ActData.FirstOrDefault(x => x.MachineCode == code)?.Quantity ?? 0);
                    var d3 = (long)(s3DefData.FirstOrDefault(x => x.MachineCode == code)?.DefectQty ?? 0);
                    var l3 = CalculateTotalLossTime(code, s3Start, s3End, listRest);
                    var mc3 = GetTotalChangeModel(code, s3Start, s3End);
                    var mModels3 = GetStartEndModel(code, s3Start, s3End);
                    var mt3 = mModels3.Count > 1 ? mModels3.Zip(mModels3.Skip(1), (p, n) => (n.StartTime - p.EndTime).TotalMinutes).Sum() : 0.0;

                    s3Item.Plan += p3;
                    s3Item.Actual += a3;
                    s3Item.DefectQty += d3;
                    s3Item.LossTimeMinutes += l3;
                    s3Item.WorkTimeMinutes += (a3 > 0 || p3 > 0 ? s3Elapsed : (s3Elapsed > 0 && RawOeeData.Any(d => d.MachineCode == code && d.SDate >= s3Start && d.SDate < s3End) ? s3Elapsed : 0));
                    s3Item.ModelChanges += mc3;
                    s3Item.ModelTimeChange += mt3;
                    if (mModels3.Any() && s3Item.ModelStartTime == null) s3Item.ModelStartTime = mModels3.First().StartTime;
                    if (mModels3.Any()) s3Item.ModelEndTime = mModels3.Last().EndTime;
                }
            }

            // Hitung PPH untuk tiap lini pada tiap shift
            foreach (var code in AudioLineCodes)
            {
                var workers = GetAverageWorkersForPeriod(code, StartSelectedDate, EndSelectedDate.AddDays(1));
                if (workers <= 0) workers = 25;

                // Shift 1 PPH
                var s1 = shift1Map[code];
                var netMin1 = s1.WorkTimeMinutes - s1.LossTimeMinutes;
                var netHr1 = netMin1 > 0 ? netMin1 / 60.0 : 0.0;
                if (s1.Actual > 0 && workers > 0 && netHr1 > 0)
                    s1.Pph = Math.Round(s1.Actual / (double)workers / netHr1, 2);

                // Shift 2 PPH
                var s2 = shift2Map[code];
                var netMin2 = s2.WorkTimeMinutes - s2.LossTimeMinutes;
                var netHr2 = netMin2 > 0 ? netMin2 / 60.0 : 0.0;
                if (s2.Actual > 0 && workers > 0 && netHr2 > 0)
                    s2.Pph = Math.Round(s2.Actual / (double)workers / netHr2, 2);

                // Shift 3 PPH
                var s3 = shift3Map[code];
                var netMin3 = s3.WorkTimeMinutes - s3.LossTimeMinutes;
                var netHr3 = netMin3 > 0 ? netMin3 / 60.0 : 0.0;
                if (s3.Actual > 0 && workers > 0 && netHr3 > 0)
                    s3.Pph = Math.Round(s3.Actual / (double)workers / netHr3, 2);

                // Overall Accumulation
                var ov = overallMap[code];
                ov.Plan = s1.Plan + s2.Plan + s3.Plan;
                ov.Actual = s1.Actual + s2.Actual + s3.Actual;
                ov.DefectQty = s1.DefectQty + s2.DefectQty + s3.DefectQty;
                ov.LossTimeMinutes = s1.LossTimeMinutes + s2.LossTimeMinutes + s3.LossTimeMinutes;
                ov.WorkTimeMinutes = s1.WorkTimeMinutes + s2.WorkTimeMinutes + s3.WorkTimeMinutes;
                ov.ModelChanges = s1.ModelChanges + s2.ModelChanges + s3.ModelChanges;
                ov.ModelTimeChange = s1.ModelTimeChange + s2.ModelTimeChange + s3.ModelTimeChange;
            }

            Shift1Summaries = shift1Map.Values.ToList();
            Shift2Summaries = shift2Map.Values.ToList();
            Shift3Summaries = shift3Map.Values.ToList();
            OverallSummaries = overallMap.Values.ToList();
        }

        // --- Metode Perhitungan & Helper (Tetap Disediakan) ---
        public List<RestTime> GetRestTime(string dayTipe) => 
            RawRestTimeData.Where(r => r.DayType == dayTipe).Select(r => new RestTime { StartTime = r.StartTime, EndTime = r.EndTime }).ToList();

        public List<ActualQty> GetActualData(DateTime startTime, DateTime endTime) =>
            RawOeeData
                .Where(d => d.SDate >= startTime && d.SDate < endTime)
                .GroupBy(d => d.MachineCode)
                .Select(g => {
                    int max = g.Max(d => d.Actual);
                    int min = g.Min(d => d.Actual);
                    int qty = (max > min) ? (max - (min > 0 ? min - 1 : 0)) : (max > 0 ? max : g.Count());
                    return new ActualQty { MachineCode = g.Key, Quantity = qty };
                })
                .ToList();

        public List<ActualQty> GetActualData(TimeSpan startTime, TimeSpan endTime) =>
            RawOeeData
                .Where(d => d.SDate.TimeOfDay >= startTime && d.SDate.TimeOfDay < endTime)
                .GroupBy(d => d.MachineCode)
                .Select(g => {
                    int max = g.Max(d => d.Actual);
                    int min = g.Min(d => d.Actual);
                    int qty = (max > min) ? (max - (min > 0 ? min - 1 : 0)) : (max > 0 ? max : g.Count());
                    return new ActualQty { MachineCode = g.Key, Quantity = qty };
                })
                .ToList();

        public List<PlanQty> GetPlanFromTargetUnit(DateTime startTime, DateTime endTime)
        {
            return RawOeeData
                .Where(d => d.SDate >= startTime && d.SDate < endTime)
                .GroupBy(d => d.MachineCode)
                .Select(g =>
                {
                    var maxTarget = g.Max(d => d.TargetUnit);
                    var minTarget = g.Min(d => d.TargetUnit);
                    decimal qty = (maxTarget > minTarget) ? (maxTarget - (minTarget > 0 ? minTarget - 1 : 0)) : (maxTarget > 0 ? maxTarget : 0);
                    if (qty == 0)
                    {
                        qty = g.Max(d => d.DailyPlan);
                    }
                    return new PlanQty
                    {
                        MachineCode = g.Key,
                        Quantity = qty
                    };
                })
                .ToList();
        }

        public List<TotalDefect> GetTotalDefect(DateTime startTime, DateTime endTime) =>
            RawNgData
                .Where(d => d.SDate >= startTime && d.SDate < endTime)
                .GroupBy(d => d.MachineCode)
                .Select(g => new TotalDefect { MachineCode = g.Key, DefectQty = g.Count() })
                .ToList();

        public List<TotalDefect> GetTotalDefect(TimeSpan startTime, TimeSpan endTime) =>
            RawNgData
                .Where(d => d.SDate.TimeOfDay >= startTime && d.SDate.TimeOfDay < endTime)
                .GroupBy(d => d.MachineCode)
                .Select(g => new TotalDefect { MachineCode = g.Key, DefectQty = g.Count() })
                .ToList();

        public int GetTotalChangeModel(string machineCode, DateTime startTime, DateTime endTime) =>
            RawOeeData
                .Where(d => d.MachineCode == machineCode && d.SDate >= startTime && d.SDate < endTime)
                .Select(d => d.ProductName)
                .Distinct()
                .Count();

        public int GetTotalChangeModel(string machineCode, TimeSpan startTime, TimeSpan endTime) =>
            RawOeeData
                .Where(d => d.MachineCode == machineCode && d.SDate.TimeOfDay >= startTime && d.SDate.TimeOfDay < endTime)
                .Select(d => d.ProductName)
                .Distinct()
                .Count();

        public List<StartEndModel> GetStartEndModel(string machineCode, DateTime startTime, DateTime endTime)
        {
            var results = new List<StartEndModel>();
            var filteredData = RawOeeData
                .Where(d => d.MachineCode == machineCode && d.SDate >= startTime && d.SDate < endTime)
                .OrderBy(d => d.SDate)
                .ToList();

            if (!filteredData.Any()) return results;

            var currentBlock = new StartEndModel { Model = filteredData.First().ProductName, StartTime = filteredData.First().SDate, EndTime = filteredData.First().SDate };
            for (int i = 1; i < filteredData.Count; i++)
            {
                if (filteredData[i].ProductName == currentBlock.Model)
                {
                    currentBlock.EndTime = filteredData[i].SDate;
                }
                else
                {
                    results.Add(currentBlock);
                    currentBlock = new StartEndModel { Model = filteredData[i].ProductName, StartTime = filteredData[i].SDate, EndTime = filteredData[i].SDate };
                }
            }
            results.Add(currentBlock);
            return results;
        }

        public List<StartEndModel> GetStartEndModel(string machineCode, TimeSpan startTime, TimeSpan endTime)
        {
            var results = new List<StartEndModel>();
            var filteredData = RawOeeData.Where(d => d.MachineCode == machineCode && d.SDate.TimeOfDay >= startTime && d.SDate.TimeOfDay < endTime).OrderBy(d => d.SDate).ToList();
            if (!filteredData.Any()) return results;
            var currentBlock = new StartEndModel { Model = filteredData.First().ProductName, StartTime = filteredData.First().SDate, EndTime = filteredData.First().SDate };
            for (int i = 1; i < filteredData.Count; i++)
            {
                if (filteredData[i].ProductName == currentBlock.Model) { currentBlock.EndTime = filteredData[i].SDate; }
                else { results.Add(currentBlock); currentBlock = new StartEndModel { Model = filteredData[i].ProductName, StartTime = filteredData[i].SDate, EndTime = filteredData[i].SDate }; }
            }
            results.Add(currentBlock);
            return results;
        }

        public int CalculateTotalLossTime(string machineCode, DateTime shiftStart, DateTime shiftEnd, List<RestTime> breaks)
        {
            var totalLossSeconds = RawLossTimeData
                .Where(lt =>
                {
                    var lossStart = lt.Date.Date + lt.StartTime;
                    var lossEnd = lt.Date.Date + lt.EndTime;
                    if (lossEnd < lossStart) lossEnd = lossEnd.AddDays(1);

                    return lt.MachineCode == machineCode &&
                           lossStart >= shiftStart &&
                           lossEnd < shiftEnd &&
                           !breaks.Any(b => {
                               var breakStart = shiftStart.Date + b.StartTime;
                               var breakEnd = shiftStart.Date + b.EndTime;
                               if (b.StartTime > b.EndTime) breakEnd = breakEnd.AddDays(1);
                               return lossStart < breakEnd && lossEnd > breakStart;
                           });
                })
                .Sum(lt => {
                    var lossEnd = lt.Date.Date + lt.EndTime;
                    if (lossEnd < (lt.Date.Date + lt.StartTime)) lossEnd = lossEnd.AddDays(1);
                    return (lossEnd - (lt.Date.Date + lt.StartTime)).TotalSeconds;
                });

            return (int)totalLossSeconds / 60;
        }

        public int CalculateTotalLossTime(string machineCode, TimeSpan shiftStart, TimeSpan shiftEnd, List<RestTime> breaks)
        {
            var totalLossSeconds = RawLossTimeData
                .Where(lt =>
                    lt.MachineCode == machineCode &&
                    lt.StartTime >= shiftStart &&
                    lt.EndTime < shiftEnd &&
                    !breaks.Any(b => lt.StartTime < b.EndTime && lt.EndTime > b.StartTime)
                )
                .Sum(lt => (lt.EndTime - lt.StartTime).TotalSeconds);

            return (int)totalLossSeconds / 60;
        }

        public string DetermineTypeOfDay(DayOfWeek day) => day switch { DayOfWeek.Friday => "FRIDAY", DayOfWeek.Saturday or DayOfWeek.Sunday => "WEEKEND", _ => "REGULAR" };

        public int GetTotalRestTime(List<RestTime> listRestTime, TimeSpan StartTime, TimeSpan EndTime, TimeSpan CurrentTime)
        {
            int totalRestMinutes = 0;
            TimeSpan effectiveCurrentTime = (StartSelectedDate.Date == DateTime.Today) ? CurrentTime : EndTime;
            foreach (var rest in listRestTime)
            {
                var overlapStart = new TimeSpan(Math.Max(StartTime.Ticks, rest.StartTime.Ticks));
                var overlapEnd = new TimeSpan(Math.Min(effectiveCurrentTime.Ticks, rest.EndTime.Ticks));
                if (overlapEnd > overlapStart) totalRestMinutes += (int)(overlapEnd - overlapStart).TotalMinutes;
            }
            return totalRestMinutes;
        }

        public double CalculateSingleDayElapsedWorkingTime(DateTime day, TimeSpan shiftStart, TimeSpan shiftEnd, List<RestTime> shiftBreaks)
        {
            var now = DateTime.Now;

            if (day.Date > now.Date) return 0;

            if (day.Date < now.Date)
            {
                double totalShiftMinutes = (shiftEnd - shiftStart).TotalMinutes;
                double actualBreakMinutesInShift = 0;

                foreach (var rest in shiftBreaks)
                {
                    var overlapStart = new TimeSpan(Math.Max(shiftStart.Ticks, rest.StartTime.Ticks));
                    var overlapEnd = new TimeSpan(Math.Min(shiftEnd.Ticks, rest.EndTime.Ticks));
                    if (overlapEnd > overlapStart)
                    {
                        actualBreakMinutesInShift += (overlapEnd - overlapStart).TotalMinutes;
                    }
                }
                return Math.Max(0, totalShiftMinutes - actualBreakMinutesInShift);
            }

            TimeSpan effectiveEndTime = (now.TimeOfDay > shiftEnd) ? shiftEnd : now.TimeOfDay;
            if (effectiveEndTime <= shiftStart) return 0;

            double grossDurationMinutes = (effectiveEndTime - shiftStart).TotalMinutes;
            int restMinutesElapsed = GetTotalRestTime(shiftBreaks, shiftStart, shiftEnd, now.TimeOfDay);

            return Math.Max(0, grossDurationMinutes - restMinutesElapsed);
        }

        public double CalculateOvernightShiftElapsedWorkingTime(DateTime startDay, TimeSpan shiftStart, TimeSpan shiftEnd, List<RestTime> shiftBreaks)
        {
            var now = DateTime.Now;
            var shiftStartDateTime = startDay.Date + shiftStart;
            var shiftEndDateTime = startDay.Date.AddDays(1) + shiftEnd;

            if (now < shiftStartDateTime) return 0;

            if (now >= shiftEndDateTime)
            {
                double totalShiftMinutes = (shiftEndDateTime - shiftStartDateTime).TotalMinutes;
                return totalShiftMinutes;
            }

            double grossDurationMinutes = (now - shiftStartDateTime).TotalMinutes;
            return Math.Max(0, grossDurationMinutes);
        }

        public int GetAverageWorkersForPeriod(string machineCode, DateTime startTime, DateTime endTime)
        {
            var workersPerDay = RawOeeData
                .Where(d => d.MachineCode == machineCode && d.SDate >= startTime && d.SDate < endTime)
                .GroupBy(d => d.SDate.Date)
                .Select(g => g.Max(d => d.NoOfOperator)) 
                .ToList();

            if (workersPerDay.Any(w => w > 0))
            {
                return (int)Math.Round(workersPerDay.Where(w => w > 0).Average());
            }
            return 25; // Default standar operator perakitan Audio
        }

        public List<DailyPlanQty> GetDailyPlanData(DateTime startTime, DateTime endTime)
        {
            return DailyPlanData
                .Where(d => d != null)
                .GroupBy(d => d.MachineCode)
                .Select(g => new DailyPlanQty
                {
                    MachineCode = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .ToList();
        }

        public class PlanQty { public decimal Quantity { get; set; } public string MachineCode { get; set; } = ""; }
        public class ActualQty { public int Quantity { get; set; } public string MachineCode { get; set; } = ""; }
        public class SUTModel { public int SUT { get; set; } public string MachineCode { get; set; } = ""; }
        public class RestTime { public TimeSpan StartTime { get; set; } public TimeSpan EndTime { get; set; } }
        public class TotalDefect { public string MachineCode { get; set; } = ""; public int DefectQty { get; set; } }
        public class StartEndModel { public string Model { get; set; } = ""; public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } }
    }
}
