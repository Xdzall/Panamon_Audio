-- ==============================================================================================
-- SKRIP VERIFIKASI AUDIT INVENTORY, SERIAL NUMBER, DAN SUMBER TRANSAKSI PANAMON AUDIO
-- Database: localhost\SQLEXPRESS.AUDIO_PRODUCTION
--
-- Tujuan Audit:
-- 1. Membuktikan secara matematis apakah dbo.FINAL*.SerialNumber merupakan catatan per unit.
-- 2. Memeriksa apakah terdapat unit serial yang hilang (missing) antar snapshot periodik.
-- 3. Membedah definisi VIEW dbo.OEESN yang selama ini disalahartikan sebagai data scan transaksi.
-- 4. Memverifikasi ketiadaan tabel transaksi stok, WIP, mutasi masuk/keluar, dan fisik opname.
-- ==============================================================================================

USE [AUDIO_PRODUCTION];
GO

PRINT '==============================================================================================';
PRINT '1. AUDIT dbo.FINAL1.SerialNumber PADA 21 OKTOBER 2026: SNAPSHOT VS ACTUAL OUTPUT RIIL';
PRINT '==============================================================================================';

SELECT 
    'FINAL1 (2026-10-21)' AS AreaAudit,
    COUNT(*) AS TotalSnapshot,
    MIN(Actual) AS MinActual,
    MAX(Actual) AS MaxActual,
    MAX(Actual) - MIN(Actual) AS DeltaActualOutput,
    COUNT(DISTINCT SerialNumber) AS SerialTercatatDiSnapshot,
    (MAX(Actual) - MIN(Actual)) - COUNT(DISTINCT SerialNumber) AS SerialHilang_TidakTercatat,
    ROUND(((MAX(Actual) - MIN(Actual)) - COUNT(DISTINCT SerialNumber)) * 100.0 / NULLIF((MAX(Actual) - MIN(Actual)), 0), 2) AS PersentaseSerialHilang_Pct
FROM dbo.FINAL1
WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00';
GO

PRINT '==============================================================================================';
PRINT '2. BUKTI CUPLIKAN SNAPSHOT PERIODIK: ACTUAL MELOMPAT TAPI HANYA 1 SERIAL TERCATAT';
PRINT '==============================================================================================';

SELECT TOP 15
    ID,
    DateTime,
    Model,
    Target,
    Actual,
    Actual - LAG(Actual, 1, Actual) OVER (ORDER BY ID) AS DeltaActual,
    SerialNumber,
    DATEDIFF(SECOND, LAG(DateTime, 1, DateTime) OVER (ORDER BY ID), DateTime) AS IntervalDetik
FROM dbo.FINAL1
WHERE DateTime >= '2026-10-21 14:30:00' AND DateTime <= '2026-10-21 15:55:00'
ORDER BY ID ASC;
GO

PRINT '==============================================================================================';
PRINT '3. REKAPITULASI SERIAL NUMBER SELURUH LINI FINAL ASSEMBLY (FINAL1 s/d FINAL10)';
PRINT '==============================================================================================';

SELECT 
    'FINAL1' AS LineCode, COUNT(*) AS TotalSnapshots, COUNT(DISTINCT SerialNumber) AS DistinctSerial,
    SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END) AS EmptySerial,
    MIN(DateTime) AS MinDate, MAX(DateTime) AS MaxDate FROM dbo.FINAL1
UNION ALL
SELECT 'FINAL2', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL2
UNION ALL
SELECT 'FINAL3', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL3
UNION ALL
SELECT 'FINAL4', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL4
UNION ALL
SELECT 'FINAL5', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL5
UNION ALL
SELECT 'FINAL7', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL7
UNION ALL
SELECT 'FINAL8', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL8
UNION ALL
SELECT 'FINAL9', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL9
UNION ALL
SELECT 'FINAL10', COUNT(*), COUNT(DISTINCT SerialNumber), SUM(CASE WHEN SerialNumber IS NULL OR LTRIM(RTRIM(SerialNumber)) = '' THEN 1 ELSE 0 END), MIN(DateTime), MAX(DateTime) FROM dbo.FINAL10;
GO

PRINT '==============================================================================================';
PRINT '4. BUKTI STRUKTUR VIEW dbo.OEESN (BUKAN TABEL FISIK SCAN / TRANSAKSI STOK)';
PRINT '==============================================================================================';

SELECT 
    o.name AS ObjectName,
    o.type_desc AS ObjectType,
    create_date AS CreatedDate,
    modify_date AS ModifiedDate
FROM sys.objects o
WHERE o.name IN ('OEESN', 'OEETrans', 'Inventory', 'StockTransaction');
GO

PRINT '==============================================================================================';
PRINT '5. AUDIT TABEL PWK, SMT, DAN PRODUCTION RECORDS';
PRINT '==============================================================================================';

SELECT 
    'PWK_ACTUAL' AS TableName, COUNT(*) AS RowCountResult FROM dbo.PWK_ACTUAL
UNION ALL
SELECT 'PWK_ACTUAL_ProductionDetail', COUNT(*) FROM dbo.PWK_ACTUAL_ProductionDetail
UNION ALL
SELECT 'PwkManualData', COUNT(*) FROM dbo.PwkManualData
UNION ALL
SELECT 'SMT1', COUNT(*) FROM dbo.SMT1
UNION ALL
SELECT 'SMT2', COUNT(*) FROM dbo.SMT2
UNION ALL
SELECT 'SMT3', COUNT(*) FROM dbo.SMT3
UNION ALL
SELECT 'ProductionRecords', COUNT(*) FROM dbo.ProductionRecords;
GO
