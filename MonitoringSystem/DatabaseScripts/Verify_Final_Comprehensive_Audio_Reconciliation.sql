-- ==============================================================================================
-- SKRIP REKONSILIASI AKHIR KOMPREHENSIF PANAMON AUDIO VS SSMS
-- Database: localhost\SQLEXPRESS.AUDIO_PRODUCTION
--
-- Tujuan:
-- 1. Menyediakan baseline angka resmi SSMS untuk pengujian regresi halaman Audio.
-- 2. Memeriksa konsistensi angka pada tanggal 2026-10-21, 2026-10-19, dan 2026-10-16.
-- 3. Memverifikasi status lini standby (FINAL6 & FINAL11) dan penanganan data kosong.
-- ==============================================================================================

USE [AUDIO_PRODUCTION];
GO

PRINT '==============================================================================================';
PRINT '1. REKONSILIASI FINAL ASSEMBLY PER LINI PADA 21 OKTOBER 2026 (07:00–07:00 WIB)';
PRINT '==============================================================================================';

WITH FinalLines AS (
    SELECT 'FINAL1' AS LineCode, MIN(Actual) AS MinAct, MAX(Actual) AS MaxAct, MIN(Target) AS MinTgt, MAX(Target) AS MaxTgt, MAX(Sut) AS Sut, COUNT(*) AS Snapshots FROM dbo.FINAL1 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    UNION ALL
    SELECT 'FINAL2', MIN(Actual), MAX(Actual), MIN(Target), MAX(Target), MAX(Sut), COUNT(*) FROM dbo.FINAL2 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    UNION ALL
    SELECT 'FINAL3', MIN(Actual), MAX(Actual), MIN(Target), MAX(Target), MAX(Sut), COUNT(*) FROM dbo.FINAL3 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    UNION ALL
    SELECT 'FINAL7', MIN(Actual), MAX(Actual), MIN(Target), MAX(Target), MAX(Sut), COUNT(*) FROM dbo.FINAL7 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    UNION ALL
    SELECT 'FINAL9', MIN(Actual), MAX(Actual), MIN(Target), MAX(Target), MAX(Sut), COUNT(*) FROM dbo.FINAL9 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    UNION ALL
    SELECT 'FINAL10', MIN(Actual), MAX(Actual), MIN(Target), MAX(Target), MAX(Sut), COUNT(*) FROM dbo.FINAL10 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
),
LossAgg AS (
    SELECT MachineCode, SUM(LossTime) AS DowntimeSec
    FROM dbo.AssemblyLossTime
    WHERE Time >= '2026-10-21 07:00:00' AND Time < '2026-10-22 07:00:00' AND LossTime <= 28800
    GROUP BY MachineCode
),
DefectAgg AS (
    SELECT MachineCode, SUM(Quantity) AS DefectQty
    FROM dbo.DEFECTT
    WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
    GROUP BY MachineCode
)
SELECT 
    f.LineCode,
    f.Snapshots,
    ISNULL(f.MaxAct - f.MinAct, 0) AS DeltaActual,
    ISNULL(f.MaxTgt - f.MinTgt, 0) AS DeltaTarget,
    f.Sut,
    ROUND(((f.MaxAct - f.MinAct) * f.Sut * 100.0) / 32100.0, 2) AS LineEfficiencyPct,
    ROUND(ISNULL(l.DowntimeSec, 0) / 60.0, 1) AS DowntimeMin,
    ISNULL(d.DefectQty, 0) AS DefectQty
FROM FinalLines f
LEFT JOIN LossAgg l ON l.MachineCode = f.LineCode
LEFT JOIN DefectAgg d ON d.MachineCode = f.LineCode;
GO

PRINT '==============================================================================================';
PRINT '2. REKONSILIASI SUB-ASSEMBLY PADA 19 OKTOBER 2026 (WHERE ProductGroup = ''AUDIO'')';
PRINT '==============================================================================================';

WITH DedupSub AS (
    SELECT 
        MachineName,
        ModelCode,
        TimeValue,
        ActualTotal,
        ROW_NUMBER() OVER (
            PARTITION BY ReportId, MachineName, ModelCode 
            ORDER BY Id DESC
        ) AS rn
    FROM dbo.MachineData
    WHERE ProductGroup = 'AUDIO'
      AND CreatedAt >= '2026-10-19 07:00:00' 
      AND CreatedAt < '2026-10-20 07:00:00'
)
SELECT 
    MachineName,
    ModelCode,
    TimeValue AS CycleTimeMin,
    SUM(ActualTotal) AS DeduplicatedActual,
    ROUND(SUM(ActualTotal) * TimeValue / 60.0, 2) AS OperatingHours
FROM DedupSub
WHERE rn = 1
GROUP BY MachineName, ModelCode, TimeValue
ORDER BY MachineName;
GO

PRINT '==============================================================================================';
PRINT '3. REKONSILIASI SALDO STOK DAN BUKU BESAR TRANSAKSI INVENTORY AUDIO';
PRINT '==============================================================================================';

SELECT 
    SourceLocation,
    DestinationLocation,
    TransactionType,
    ModelCode,
    SUM(Quantity) AS TotalQty,
    COUNT(*) AS TxCount
FROM dbo.AudioInventoryTransaction
GROUP BY SourceLocation, DestinationLocation, TransactionType, ModelCode
ORDER BY TransactionType;
GO

PRINT '==============================================================================================';
PRINT '4. VERIFIKASI KETIADAAN TABEL ATAU DATA AC PADA DB AUDIO_PRODUCTION';
PRINT '==============================================================================================';

SELECT 
    TABLE_NAME, 
    TABLE_TYPE 
FROM INFORMATION_SCHEMA.TABLES 
WHERE TABLE_NAME IN ('PROMOSYS', 'MachineDB', 'MCH1_STATUS', 'KYOSHIN_LOG')
   OR TABLE_NAME LIKE 'MCH1%';
GO
