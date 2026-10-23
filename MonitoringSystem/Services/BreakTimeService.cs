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
    // Jadwal istirahat per tanggal. Aturannya ada di BreakSchedule untuk loss time dan efisiensi.
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

            // 2. Default to standard Audio Shift 1 / Non-shift schedule
            return BreakSchedule.Build(dateOnly, custom, new[] { "SHIFT 1" });
        }
    }
}
