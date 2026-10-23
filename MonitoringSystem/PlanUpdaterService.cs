using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonitoringSystem.Data;
using MonitoringSystem.Models;

public class PlanUpdaterService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly string[] AudioLines = new[]
    {
        "FINAL1", "FINAL2", "FINAL3", "FINAL4", "FINAL5",
        "FINAL6", "FINAL7", "FINAL8", "FINAL9", "FINAL10", "FINAL11"
    };

    public PlanUpdaterService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(30000, stoppingToken);  // Interval 30 detik untuk efisiensi
                UpdatePlanData();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error occurred: {ex.Message}");
            }
        }
    }

    private void UpdatePlanData()
    {
        var currentTime = DateTime.Now;
        string startTime;
        string endTime;

        if (currentTime.Hour >= 7 && currentTime.Hour < 16)
        {
            startTime = "07:00";
            endTime = "16:00";
        }
        else if (currentTime.Hour >= 16 && currentTime.Hour < 23)
        {
            startTime = "16:00";
            endTime = "23:45";
        }
        else
        {
            startTime = "23:45";
            endTime = "07:00";
        }

        foreach (var line in AudioLines)
        {
            InsertOrReplacePlan(startTime, endTime, line);
        }
    }

    private void InsertOrReplacePlan(string startTime, string endTime, string machineCode)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var existingRecord = context.HourlyPlanData
                .FirstOrDefault(x => x.SelectedDate == DateTime.Today && x.MachineCode == machineCode);

            if (existingRecord != null)
            {
                existingRecord.TotalPlan = CalculatePlan();
                existingRecord.UpdatedAt = DateTime.Now;
                context.Update(existingRecord);
            }
            else
            {
                context.HourlyPlanData.Add(new HourlyPlanData
                {
                    MachineCode = machineCode, 
                    SelectedDate = DateTime.Today,
                    TotalPlan = CalculatePlan(),
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }

            context.SaveChanges();
        }
    }

    private int CalculatePlan()
    {
        return 10;
    }
}
