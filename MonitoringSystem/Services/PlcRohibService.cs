using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MonitoringSystem.Services
{
    /// <summary>
    /// PLC Service dummy untuk backward compatibility (dinonaktifkan pada Panamon Audio).
    /// </summary>
    public class PlcRohibLauncher : IHostedService
    {
        public PlcRohibLauncher(IConfiguration cfg, ILogger<PlcRohibLauncher> logger)
        {
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Nonaktif untuk Audio
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    public static class PlcRohibProxy
    {
        public const string PathPrefix = "/plcrohib-app";

        /// <summary>
        /// Endpoint ditutup (404) untuk Panamon Audio.
        /// </summary>
        public static void MapPlcRohibProxy(this WebApplication app)
        {
            app.Map(PathPrefix + "/{**path}", (HttpContext ctx) =>
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return Results.NotFound();
            });
        }
    }
}
