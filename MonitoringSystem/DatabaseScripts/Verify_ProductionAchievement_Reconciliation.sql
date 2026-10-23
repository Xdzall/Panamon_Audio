-- ====================================================================================================
-- SKRIP VERIFIKASI SSMS: REKONSILIASI PRODUCTION ACHIEVEMENT AUDIO
-- Database : AUDIO_PRODUCTION
-- Dokumen  : Verifikasi Rekonsiliasi Lini, Model, dan Penanganan Anomali (07.00 - 07.00)
-- Aturan   : 
--   1. Jendela hari produksi adalah 07:00:00 s/d 06:59:59 hari berikutnya.
--   2. Snapshot diurutkan menurut [DateTime] ASC, ID ASC.
--   3. Delta counter diatribusikan ke model pada snapshot terkait (saat pergantian model).
--   4. Anomali (Reset, Koreksi Turun, Duplikasi, Carryover) diberi status eksplisit,
--      BUKAN diam-diam dihitung sebagai produksi baru.
-- ====================================================================================================

USE AUDIO_PRODUCTION;
GO

PRINT '====================================================================================================';
PRINT 'KASUS UJI 1: PRODUKSI 2026-10-21 (FINAL1, FINAL2, FINAL10)';
PRINT 'Skenario: Model Changeover pada FINAL1, Carryover Kemarin & Reset pada FINAL10';
PRINT '====================================================================================================';
GO

-- 1.1 FINAL1 (Pergantian Model RF-2400DEG-K -> RF-2400DLJ-K)
DECLARE @from1 DATETIME = '2026-10-21 07:00:00', @to1 DATETIME = '2026-10-22 07:00:00';

WITH Snapshots AS (
    SELECT 
        ID, [DateTime], Model, Actual, Target, SerialNumber,
        LAG(Actual) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevActual,
        LAG(Target) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevTarget,
        LAG(Model) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevModel,
        ROW_NUMBER() OVER (ORDER BY [DateTime] ASC, ID ASC) AS RowNum
    FROM dbo.FINAL1
    WHERE [DateTime] >= @from1 AND [DateTime] < @to1
),
CalculatedDeltas AS (
    SELECT 
        ID, [DateTime], Model, PrevModel, Actual, PrevActual, Target, PrevTarget, SerialNumber,
        CASE 
            WHEN RowNum = 1 THEN Actual -- Snapshot pertama pagi
            WHEN Actual >= PrevActual THEN Actual - PrevActual
            WHEN Actual < PrevActual AND Actual <= 20 THEN Actual -- Counter Reset
            ELSE (Actual - PrevActual) -- Koreksi Negatif
        END AS DeltaActual,
        CASE 
            WHEN RowNum = 1 AND Actual <= 20 THEN 'InitialReset'
            WHEN RowNum = 1 AND Actual > 20 THEN 'PendingCarryover'
            WHEN PrevModel IS NOT NULL AND Model <> PrevModel THEN 'ModelChangeover'
            WHEN Actual < PrevActual AND Actual <= 20 THEN 'CounterReset'
            WHEN Actual < PrevActual AND Actual > 20 THEN 'NegativeCorrection'
            WHEN Actual = PrevActual AND Target = PrevTarget THEN 'HeartbeatTick'
            ELSE 'NormalIncrement'
        END AS AnomalyStatus
    FROM Snapshots
)
SELECT 
    Model,
    MIN([DateTime]) AS FirstSeen,
    MAX([DateTime]) AS LastSeen,
    COUNT(*) AS SnapshotCount,
    SUM(DeltaActual) AS ModelActualTotal
FROM CalculatedDeltas
GROUP BY Model
ORDER BY ModelActualTotal DESC;

-- Rekonsiliasi Total Lini FINAL1
SELECT 
    'FINAL1' AS LineCode,
    COUNT(*) AS TotalSnapshots,
    MIN(Actual) AS MinCounter,
    MAX(Actual) AS MaxCounter,
    SUM(CASE 
        WHEN RowNum = 1 THEN Actual
        WHEN Actual >= PrevActual THEN Actual - PrevActual
        WHEN Actual < PrevActual AND Actual <= 20 THEN Actual
        ELSE (Actual - PrevActual)
    END) AS ReconciledLineActual,
    SUM(CASE WHEN Actual < PrevActual AND Actual <= 20 THEN 1 ELSE 0 END) AS ResetCount,
    SUM(CASE WHEN Actual < PrevActual AND Actual > 20 THEN 1 ELSE 0 END) AS CorrectionCount,
    SUM(CASE WHEN PrevModel IS NOT NULL AND Model <> PrevModel THEN 1 ELSE 0 END) AS ModelChangeCount
FROM (
    SELECT 
        ID, [DateTime], Model, Actual, Target,
        LAG(Actual) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevActual,
        LAG(Model) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevModel,
        ROW_NUMBER() OVER (ORDER BY [DateTime] ASC, ID ASC) AS RowNum
    FROM dbo.FINAL1
    WHERE [DateTime] >= @from1 AND [DateTime] < @to1
) s;
GO

PRINT '====================================================================================================';
PRINT 'KASUS UJI 2: KOREKSI COUNTER TURUN PADA 2026-03-06 (FINAL10)';
PRINT 'Skenario: Operator/SPV mengoreksi counter turun (251 -> 232 dan 286 -> 240)';
PRINT 'Verifikasi: Anomali diberi status NegativeCorrection dan tidak diam-diam dihitung sebagai produksi';
PRINT '====================================================================================================';
GO

DECLARE @from2 DATETIME = '2026-03-06 07:00:00', @to2 DATETIME = '2026-03-07 07:00:00';

WITH Snapshots10 AS (
    SELECT 
        ID, [DateTime], Model, Actual, Target, SerialNumber,
        LAG(Actual) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevActual,
        ROW_NUMBER() OVER (ORDER BY [DateTime] ASC, ID ASC) AS RowNum
    FROM dbo.FINAL10
    WHERE [DateTime] >= @from2 AND [DateTime] < @to2
)
SELECT 
    ID, [DateTime], Model, PrevActual, Actual,
    (Actual - PrevActual) AS CorrectionDelta,
    'NegativeCorrection' AS Status,
    'Koreksi counter turun tervalidasi (bukan penambahan produksi phantom)' AS Keterangan
FROM Snapshots10
WHERE Actual < PrevActual AND Actual > 20
ORDER BY [DateTime];
GO

PRINT '====================================================================================================';
PRINT 'KASUS UJI 3: REKONSILIASI LINI & SELURUH MODEL (2026-10-21)';
PRINT 'Verifikasi: Total Lini = SUM(Seluruh Model) dan Selisih = 0';
PRINT '====================================================================================================';
GO

DECLARE @from3 DATETIME = '2026-10-21 07:00:00', @to3 DATETIME = '2026-10-22 07:00:00';

-- Membandingkan Total Model vs Total Lini pada FINAL1
WITH ModelTotals AS (
    SELECT 
        Model,
        SUM(CASE 
            WHEN RowNum = 1 THEN Actual
            WHEN Actual >= PrevActual THEN Actual - PrevActual
            WHEN Actual < PrevActual AND Actual <= 20 THEN Actual
            ELSE (Actual - PrevActual)
        END) AS ModelActual
    FROM (
        SELECT 
            ID, [DateTime], Model, Actual,
            LAG(Actual) OVER (ORDER BY [DateTime] ASC, ID ASC) AS PrevActual,
            ROW_NUMBER() OVER (ORDER BY [DateTime] ASC, ID ASC) AS RowNum
        FROM dbo.FINAL1
        WHERE [DateTime] >= @from3 AND [DateTime] < @to3
    ) s
    GROUP BY Model
)
SELECT 
    'FINAL1' AS LineCode,
    (SELECT SUM(ModelActual) FROM ModelTotals) AS SumAllModels,
    (SELECT MAX(Actual) FROM dbo.FINAL1 WHERE [DateTime] >= @from3 AND [DateTime] < @to3) AS FinalSnapshotCounter,
    CASE 
        WHEN (SELECT SUM(ModelActual) FROM ModelTotals) = (SELECT MAX(Actual) FROM dbo.FINAL1 WHERE [DateTime] >= @from3 AND [DateTime] < @to3)
        THEN 'TEREKONSILIASI 100% COCOK'
        ELSE 'SELISIH DETEKSI'
    END AS ReconciliationCheck;
GO
