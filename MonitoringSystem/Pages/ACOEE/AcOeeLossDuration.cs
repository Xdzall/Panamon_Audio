using Microsoft.Data.SqlClient;

namespace MonitoringSystem.Pages.ACOEE
{
    // Durasi loss mesin (dbo.PlcKyoshinLossEvent) yang dipakai halaman AC OEE: tanpa jam istirahat.
    // NetDurationMin diisi Plclogger (DurationMin R103 dikurangi irisan jadwal istirahat, lihat Services/BreakSchedule.cs).
    // Kolom belum ada / belum terisi = DurationMin asli.
    public static class AcOeeLossDuration
    {
        // Ekspresi SQL durasi (menit), alias = alias tabel PlcKyoshinLossEvent di query ("" tanpa alias)
        public static string Expr(SqlConnection conn, string alias = "")
        {
            var p = alias.Length > 0 ? alias + "." : "";
            using var cmd = new SqlCommand("SELECT COL_LENGTH(N'dbo.PlcKyoshinLossEvent', N'NetDurationMin')", conn);
            return cmd.ExecuteScalar() is DBNull or null ? $"{p}DurationMin" : $"ISNULL({p}NetDurationMin, {p}DurationMin)";
        }
    }
}
