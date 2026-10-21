using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using MonitoringSystem.Data;
using MonitoringSystem.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MonitoringSystem.Services
{
    // Jadwal istirahat per tanggal. Aturannya ada di BreakSchedule (dipakai bersama Plclogger untuk loss time mesin).
    public class BreakTimeService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BreakTimeService> _logger;

        public BreakTimeService(
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<BreakTimeService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<List<BreakTimeInfo>> GetBreakTimesForDateAsync(DateTime date)
        {
            var dateOnly = DateOnly.FromDateTime(date);

            // 1. Check if user saved custom break times in DB
            var customBreakStopwatch = Stopwatch.StartNew();
            var dbBreakTimes = await _context.Set<AdditionalBreakTime>()
                .Where(b => b.Date == dateOnly)
                .OrderBy(b => b.StartTime)
                .ToListAsync();
            customBreakStopwatch.Stop();

            var custom = dbBreakTimes.Select(b => new BreakTimeInfo
            {
                StartTime = b.StartTime.ToTimeSpan(),
                EndTime = b.EndTime.ToTimeSpan(),
                Reason = b.Reason ?? ""
            }).ToList();

            if (custom.Any(b => BreakSchedule.IsValidCustom(b.StartTime, b.EndTime, b.Reason)))
            {
                _logger.LogInformation(
                    "Break-time data for {Date:yyyy-MM-dd}: custom query {CustomBreakMs} ms; custom schedule used",
                    date,
                    customBreakStopwatch.ElapsedMilliseconds);
                return BreakSchedule.Build(dateOnly, custom, Array.Empty<string>());
            }

            // 2. Generate hardcoded times based on active shifts
            var shiftModes = new List<string>();
            var shiftModeStopwatch = Stopwatch.StartNew();

            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(BreakSchedule.ShiftModeSql, connection))
                    {
                        command.Parameters.AddWithValue("@date", dateOnly.ToDateTime(TimeOnly.MinValue));
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                shiftModes.Add(reader.GetString(0).ToUpper());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading active shifts for {Date:yyyy-MM-dd}", date);
            }
            finally
            {
                shiftModeStopwatch.Stop();
                _logger.LogInformation(
                    "Break-time data for {Date:yyyy-MM-dd}: custom query {CustomBreakMs} ms; shift query {ShiftModeMs} ms",
                    date,
                    customBreakStopwatch.ElapsedMilliseconds,
                    shiftModeStopwatch.ElapsedMilliseconds);
            }

            return BreakSchedule.Build(dateOnly, custom, shiftModes);
        }
    }
}
