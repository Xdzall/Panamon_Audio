using System;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MonitoringSystem.Data
{
    public static class SmartConnectionResolver
    {
        private static string? _resolvedConnectionString = null;
        private static bool _isFallback = false;
        private static string _activeHost = "localhost\\SQLEXPRESS";
        private static string _statusText = "Localhost (Fallback)";
        private static readonly object _lock = new();

        public static bool IsFallback => _isFallback;
        public static string ActiveHost => _activeHost;
        public static string StatusText => _statusText;
        public static string ResolvedConnectionString => _resolvedConnectionString ?? "";

        public static string ResolveConnectionString(IConfiguration configuration, ILogger? logger = null)
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(_resolvedConnectionString))
                {
                    return _resolvedConnectionString;
                }

                string serverConn = configuration.GetConnectionString("ServerConnection")
                    ?? "Server=137.40.93.165;Database=AUDIO_PRODUCTION;User Id=sa;Password=sa;Trusted_Connection=False;TrustServerCertificate=True;Encrypt=False;Connection Timeout=5;";

                string localConn = configuration.GetConnectionString("LocalConnection")
                    ?? configuration.GetConnectionString("DefaultConnection")
                    ?? "Server=localhost\\SQLEXPRESS;Database=AUDIO_PRODUCTION;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;Connection Timeout=5;";

                serverConn = EnsureTimeout(serverConn, 5);
                localConn = EnsureTimeout(localConn, 5);

                // 1. Coba sambungkan ke Server Pabrik (137.40.93.165) dengan timeout ketat 5 detik
                try
                {
                    logger?.LogInformation("[SMART FALLBACK] Memeriksa koneksi Wi-Fi Pabrik ke 137.40.93.165 (Batas timeout: 5 detik)...");
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    using var conn = new SqlConnection(serverConn);
                    
                    var openTask = conn.OpenAsync(cts.Token);
                    openTask.GetAwaiter().GetResult();

                    _resolvedConnectionString = serverConn;
                    _isFallback = false;
                    _activeHost = "137.40.93.165 (Wi-Fi Pabrik)";
                    _statusText = "Server (137.40.93.165)";
                    logger?.LogInformation("[SMART FALLBACK] Tersambung ke Wi-Fi Internal Pabrik! Menggunakan server database: 137.40.93.165");
                    return _resolvedConnectionString;
                }
                catch (Exception ex)
                {
                    _resolvedConnectionString = localConn;
                    _isFallback = true;
                    _activeHost = "Localhost SQLEXPRESS (Smart Fallback)";
                    _statusText = "Localhost (Fallback)";
                    logger?.LogWarning("[SMART FALLBACK] Server 137.40.93.165 tidak terjangkau dalam 5 detik ({Message}). Otomatis beralih ke database lokal localhost\\SQLEXPRESS tanpa error!", ex.Message);
                    return _resolvedConnectionString;
                }
            }
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
    }
}
