-- ============================================================================
-- SCRIPT VERIFIKASI SSMS: PROFILING SATUAN & DURASI ASSEMBLY LOSS TIME (AUDIO)
-- Database: AUDIO_PRODUCTION
-- Dokumen: Verifikasi Satuan LossTime, Kejadian Ekstrem, Shift, dan Rekonsiliasi Jendela
-- ============================================================================

USE [AUDIO_PRODUCTION];
GO

PRINT '============================================================================';
PRINT '1. PROFILING UMUM TABEL AssemblyLossTime';
PRINT '============================================================================';

SELECT 
    COUNT(*) AS TotalBaris,
    MIN([Date]) AS MinDate,
    MAX([Date]) AS MaxDate,
    MIN([Time]) AS MinTime,
    MAX([Time]) AS MaxTime,
    MIN([EndDateTime]) AS MinEndDateTime,
    MAX([EndDateTime]) AS MaxEndDateTime,
    MIN(LossTime) AS MinLossTimeSec,
    MAX(LossTime) AS MaxLossTimeSec,
    AVG(LossTime) AS AvgLossTimeSec,
    SUM(CASE WHEN Reason IS NOT NULL AND RTRIM(Reason) <> '' THEN 1 ELSE 0 END) AS NonEmptyReason,
    SUM(CASE WHEN DetailedReason IS NOT NULL AND RTRIM(DetailedReason) <> '' THEN 1 ELSE 0 END) AS NonEmptyDetailedReason
FROM dbo.AssemblyLossTime;
GO

PRINT '============================================================================';
PRINT '2. BUKTI SATUAN & HUBUNGAN DENGAN SNAPSHOT SUT (Standard Unit Time)';
PRINT 'Formula: LossTime = DATEDIFF(SECOND, Time, EndDateTime) - SUT';
PRINT '============================================================================';

-- Contoh FINAL3: SUT = 118 detik, Delta Waktu = ~193 detik, LossTime = ~75 detik (193 - 118)
SELECT TOP 5
    Id,
    MachineCode,
    [Time] AS StartSnapshot,
    EndDateTime AS EndSnapshot,
    LossTime AS RecordedLossTimeSec,
    DATEDIFF(SECOND, [Time], EndDateTime) AS ElapsedSec,
    DATEDIFF(SECOND, [Time], EndDateTime) - LossTime AS InferredSutSec
FROM dbo.AssemblyLossTime
WHERE MachineCode = 'FINAL3' AND CAST([Time] AS DATE) = '2026-10-21'
ORDER BY Id DESC;

-- Contoh FINAL1: SUT = 30 detik, Delta Waktu = ~194 detik, LossTime = ~164 detik (194 - 30)
SELECT TOP 5
    Id,
    MachineCode,
    [Time] AS StartSnapshot,
    EndDateTime AS EndSnapshot,
    LossTime AS RecordedLossTimeSec,
    DATEDIFF(SECOND, [Time], EndDateTime) AS ElapsedSec,
    DATEDIFF(SECOND, [Time], EndDateTime) - LossTime AS InferredSutSec
FROM dbo.AssemblyLossTime
WHERE MachineCode = 'FINAL1' AND CAST([Time] AS DATE) = '2026-10-21'
ORDER BY Id DESC;

-- Contoh FINAL7: SUT = 0 detik, Delta Waktu = ~193 detik, LossTime = ~193 detik (193 - 0)
SELECT TOP 5
    Id,
    MachineCode,
    [Time] AS StartSnapshot,
    EndDateTime AS EndSnapshot,
    LossTime AS RecordedLossTimeSec,
    DATEDIFF(SECOND, [Time], EndDateTime) AS ElapsedSec,
    DATEDIFF(SECOND, [Time], EndDateTime) - LossTime AS InferredSutSec
FROM dbo.AssemblyLossTime
WHERE MachineCode = 'FINAL7' AND CAST([Time] AS DATE) = '2026-10-21'
ORDER BY Id DESC;
GO

PRINT '============================================================================';
PRINT '3. ANALISIS NILAI EKSTREM (> 8 JAM / 28,800 DETIK) & JEDA DORMAN/SHUTDOWN';
PRINT '============================================================================';

SELECT 
    Id,
    MachineCode,
    [Date] AS StampedDate,
    [Time] AS EventStartTime,
    EndDateTime AS EventEndTime,
    LossTime AS RawLossTimeSec,
    ROUND(LossTime / 3600.0, 2) AS RawLossHours,
    DATEDIFF(DAY, [Time], EndDateTime) AS SpanDays,
    CASE 
        WHEN LossTime > 259200 THEN 'Dorman / Shutdown Panjang (> 3 Hari)'
        WHEN LossTime > 86400 THEN 'Libur Akhir Pekan / Shutdown Multi-hari (> 24 Jam)'
        WHEN LossTime > 28800 THEN 'Jeda Antar-Shift / Overnight (> 8 Jam)'
        ELSE 'Normal Production Loss'
    END AS KlasifikasiAnomali
FROM dbo.AssemblyLossTime
WHERE LossTime > 28800
ORDER BY LossTime DESC;
GO

PRINT '============================================================================';
PRINT '4. KOMPARASI REKONSILIASI JENDELA PRODUKSI (07:00-07:00) VS RAW DATE';
PRINT 'Tanggal Evaluasi: 2026-10-21';
PRINT '============================================================================';

DECLARE @TargetDate DATE = '2026-10-21';
DECLARE @WinStart DATETIME2 = '2026-10-21 07:00:00';
DECLARE @WinEnd   DATETIME2 = '2026-10-22 07:00:00';

SELECT 
    m.MachineCode,
    -- Agregasi Salah: Blind Filter by CAST(Date AS DATE)
    ISNULL(rawD.BlindCount, 0) AS BlindEventCount,
    ISNULL(rawD.BlindRawHours, 0) AS BlindRawHours,
    
    -- Agregasi Benar: Filter Jendela Produksi 07:00 s/d 07:00 (Hanya Kejadian Operasional)
    ISNULL(winD.OperationalEvents, 0) AS OperationalEvents,
    ISNULL(winD.OperationalLossSec, 0) AS OperationalLossSec,
    ISNULL(winD.OperationalLossMin, 0) AS OperationalLossMin,
    ISNULL(winD.OperationalLossHours, 0) AS OperationalLossHours
FROM (
    SELECT DISTINCT MachineCode FROM dbo.AssemblyLossTime
    UNION SELECT 'FINAL6' UNION SELECT 'FINAL11'
) m
LEFT JOIN (
    SELECT 
        MachineCode,
        COUNT(*) AS BlindCount,
        ROUND(SUM(LossTime) / 3600.0, 2) AS BlindRawHours
    FROM dbo.AssemblyLossTime
    WHERE CAST([Date] AS DATE) = @TargetDate
    GROUP BY MachineCode
) rawD ON m.MachineCode = rawD.MachineCode
LEFT JOIN (
    SELECT 
        MachineCode,
        COUNT(*) AS OperationalEvents,
        SUM(LossTime) AS OperationalLossSec,
        ROUND(SUM(LossTime) / 60.0, 2) AS OperationalLossMin,
        ROUND(SUM(LossTime) / 3600.0, 2) AS OperationalLossHours
    FROM dbo.AssemblyLossTime
    WHERE [Time] >= @WinStart AND [Time] < @WinEnd
      AND LossTime <= 28800
    GROUP BY MachineCode
) winD ON m.MachineCode = winD.MachineCode
ORDER BY m.MachineCode;
GO
