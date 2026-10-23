using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonitoringSystem.Data;
using MonitoringSystem.Filters;
using MonitoringSystem.Hubs;
using MonitoringSystem.Models;
using MonitoringSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// 1. DATABASE CONTEXTS (AUDIO_PRODUCTION FORK)
// ============================================================================
// Menggunakan Smart Connection Resolver: Wi-Fi Pabrik (137.40.93.165) atau Smart Fallback (localhost\SQLEXPRESS)
var defaultConnectionString = SmartConnectionResolver.ResolveConnectionString(builder.Configuration);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(defaultConnectionString));

builder.Services.AddDbContext<ScaffoldedDbContext>(options =>
    options.UseSqlServer(defaultConnectionString));

// ============================================================================
// 2. IDENTITY & ROLE MANAGEMENT
// ============================================================================
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(12);
});

// ============================================================================
// 3. DATA PROTECTION & UPLOAD FOLDERS SETUP
// ============================================================================
var dataProtectionKeysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeysDir);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir))
    .SetApplicationName("Panamon-Audio");

var uploadDir = Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads", "LossTimeAttachments");
Directory.CreateDirectory(uploadDir);

var logsDir = Path.Combine(builder.Environment.ContentRootPath, "Logs");
Directory.CreateDirectory(logsDir);

// ============================================================================
// 4. HEALTH CHECKS
// ============================================================================
builder.Services.AddHealthChecks()
    .AddAsyncCheck("Audio_Database", async () =>
    {
        try
        {
            await using var connection = new Microsoft.Data.SqlClient.SqlConnection(defaultConnectionString);
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Audio Database connection successful.");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"Audio Database connection failed: {ex.Message}");
        }
    }, tags: new[] { "db", "audio", "sql" });

// ============================================================================
// 5. APPLICATION SERVICES & BACKGROUND HOSTED SERVICES
// ============================================================================
builder.Services.AddControllers();
builder.Services.AddScoped<BreakTimeService>();

builder.Services.Configure<HostOptions>(options =>
    options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

builder.Services.AddSignalR();

builder.Services.AddRazorPages(options =>
{
    // SubPanamon (/SubPanamon: PWK Actual, Machine Efficiency, PWK) dibuka tanpa login Panamon
    options.Conventions.AllowAnonymousToFolder("/SubPanamon");
})
.AddMvcOptions(options =>
{
    options.Filters.Add<AuthorizeFilter>();
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
    });
}

var app = builder.Build();

// ============================================================================
// 6. STARTUP DATABASE INITIALIZATION & AUDIO SEEDING
// ============================================================================
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Auto-apply pending migrations if any
    await db.Database.MigrateAsync();
    await db.Database.ExecuteSqlRawAsync("SELECT 1");

    // Pastikan tabel detail koreksi PWK Actual ada
    string createPwKProductionDetailSql = @"
    IF OBJECT_ID('dbo.PWK_ACTUAL_ProductionDetail', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PWK_ACTUAL_ProductionDetail
        (
            Id                  BIGINT IDENTITY(1,1) NOT NULL
                CONSTRAINT PK_PWK_ACTUAL_ProductionDetail PRIMARY KEY,
            PWKActualId         INT NOT NULL,
            SlotStart           DATETIME2(0) NOT NULL,
            ProductId           NVARCHAR(50) NOT NULL,
            SourceModel         NVARCHAR(100) NOT NULL,
            SourceSerialStart   NVARCHAR(100) NULL,
            SourceSerialEnd     NVARCHAR(100) NULL,
            SourceDailyPlan     INT NOT NULL,
            SourceDailyActual   INT NOT NULL,
            SourceDefect        INT NOT NULL,
            Model               NVARCHAR(100) NOT NULL,
            SerialStart         NVARCHAR(100) NULL,
            SerialEnd           NVARCHAR(100) NULL,
            DailyPlan           INT NOT NULL,
            DailyActual         INT NOT NULL,
            Defect              INT NOT NULL,
            Keterangan          NVARCHAR(1000) NULL,
            CreatedBy           NVARCHAR(100) NULL,
            CreatedAt           DATETIME2(0) NOT NULL
                CONSTRAINT DF_PWK_ACTUAL_ProductionDetail_CreatedAt DEFAULT GETDATE(),
            UpdatedBy           NVARCHAR(100) NULL,
            UpdatedAt           DATETIME2(0) NULL,
            CONSTRAINT FK_PWK_ACTUAL_ProductionDetail_Header
                FOREIGN KEY (PWKActualId) REFERENCES dbo.PWK_ACTUAL(Id) ON DELETE CASCADE,
            CONSTRAINT UQ_PWK_ACTUAL_ProductionDetail
                UNIQUE (PWKActualId, SlotStart, ProductId)
        );
    END;

    IF COL_LENGTH('dbo.PWK_ACTUAL_ProductionDetail', 'DisplayStart') IS NULL
        ALTER TABLE dbo.PWK_ACTUAL_ProductionDetail ADD DisplayStart TIME(0) NULL;

    IF COL_LENGTH('dbo.PWK_ACTUAL_ProductionDetail', 'DisplayEnd') IS NULL
        ALTER TABLE dbo.PWK_ACTUAL_ProductionDetail ADD DisplayEnd TIME(0) NULL;";
    await db.Database.ExecuteSqlRawAsync(createPwKProductionDetailSql);

    // Seed Audio Roles & Default Users
    await AudioIdentitySeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
    app.Logger.LogInformation("Database AUDIO_PRODUCTION startup check & identity seeding completed.");
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "DB warm-up/seeding warning: {Message}", ex.Message);
}

// ============================================================================
// 7. HTTP REQUEST PIPELINE
// ============================================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseResponseCompression();
}

app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800";
    }
});

app.UseRouting();
app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoint
app.MapHealthChecks("/health");

// Legacy /Audio redirects directly to native Panamon pages
app.MapGet("/Audio", () => Results.Redirect("/ProductionReport/index"));
app.MapGet("/Audio/{**slug}", () => Results.Redirect("/ProductionReport/index"));

// SignalR & Razor Pages
app.MapHub<LossTimeHub>("/dataHub");
app.MapControllers();
app.MapRazorPages();

app.Run();
