-- ==============================================================================================
-- SKRIP VERIFIKASI SUMMARY, SUT, PWK, DAN BREAK TIME AUDIO
-- Database: localhost\SQLEXPRESS.AUDIO_PRODUCTION
--
-- Tujuan:
-- 1. Verifikasi ketersediaan SUT per lini FINAL1..11 dan perbandingan dengan MasterData/AudioLineMaster.
-- 2. Verifikasi status tabel PWK_ACTUAL, PWK_ACTUAL_ProductionDetail, PwkManualData.
-- 3. Verifikasi perhitungan Man-Hours standar Audio berdasarkan SUT dan jumlah operator.
-- 4. Verifikasi jadwal istirahat (RestTime / Break Schedule) Audio 3-shift.
-- ==============================================================================================

USE [AUDIO_PRODUCTION];
GO

PRINT '==============================================================================================';
PRINT '1. AUDIT SUT PER LINI FINAL ASSEMBLY (PADA SNAPSHOT 21 OKTOBER 2026)';
PRINT '==============================================================================================';

SELECT 
    'FINAL1' AS LineCode, MAX(Model) AS CurrentModel, MAX(Sut) AS SnapshotSutSec, 
    COUNT(*) AS SnapshotCount, MAX(DateTime) AS LastSnapshotTime FROM dbo.FINAL1 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL2', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL2 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL3', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL3 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL4', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL4 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL5', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL5 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL6', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL6 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL7', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL7 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL8', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL8 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL9', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL9 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL10', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL10 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
UNION ALL
SELECT 'FINAL11', MAX(Model), MAX(Sut), COUNT(*), MAX(DateTime) FROM dbo.FINAL11 WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00';
GO

PRINT '==============================================================================================';
PRINT '2. AUDIT STATUS TABEL PWK DAN DATA INPUT MANUAL';
PRINT '==============================================================================================';

SELECT 
    'PWK_ACTUAL' AS TableName, COUNT(*) AS TotalRows, 'Tabel fisik time-clock operator' AS Keterangan FROM dbo.PWK_ACTUAL
UNION ALL
SELECT 'PWK_ACTUAL_ProductionDetail', COUNT(*), 'Detail serial punch-card operator' FROM dbo.PWK_ACTUAL_ProductionDetail
UNION ALL
SELECT 'PwkManualData', COUNT(*), 'Tabel input manual PWK' FROM dbo.PwkManualData;
GO

PRINT '==============================================================================================';
PRINT '3. REKAPITULASI SUT DAN OPERATOR DARI dbo.AudioLineMaster';
PRINT '==============================================================================================';

SELECT 
    LineCode,
    DisplayName,
    IsActive,
    DefaultCapacity,
    DefaultOperators,
    DefaultSutSeconds,
    Status
FROM dbo.AudioLineMaster
ORDER BY LineCode;
GO

PRINT '==============================================================================================';
PRINT '4. JADWAL ISTIRAHAT AUDIO (RestTime & AdditionalBreakTimes)';
PRINT '==============================================================================================';

SELECT 
    'RestTime' AS SourceTable,
    DayType,
    StartTime,
    EndTime,
    DATEDIFF(MINUTE, StartTime, EndTime) AS DurasiMenit
FROM dbo.RestTime
ORDER BY StartTime;
GO
