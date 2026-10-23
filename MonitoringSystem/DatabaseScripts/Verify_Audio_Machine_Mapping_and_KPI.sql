-- ============================================================================
-- SCRIPT VERIFIKASI SSMS: PEMETAAN MESIN AUDIO, SUMBER DATA & FORMULA KPI
-- Database: AUDIO_PRODUCTION
-- Dokumen: Pemetaan Sub-assembly (MachineData AUDIO), Final Assembly,
--          Deduplikasi Update Berulang, Validasi Satuan, dan Rumus KPI Audio
-- ============================================================================

USE [AUDIO_PRODUCTION];
GO

PRINT '============================================================================';
PRINT '1. VALIDASI FILTER KETAT MachineData: HANYA ProductGroup = ''AUDIO''';
PRINT '============================================================================';

SELECT 
    ProductGroup,
    COUNT(*) AS TotalBaris,
    COUNT(DISTINCT MachineName) AS JumlahMesin,
    MIN(CreatedAt) AS MinCreatedAt,
    MAX(CreatedAt) AS MaxCreatedAt
FROM dbo.MachineData
GROUP BY ProductGroup
ORDER BY ProductGroup;
GO

PRINT '============================================================================';
PRINT '2. DAFTAR MESIN AUDIO (SUB-ASSEMBLY) PADA MachineData (ProductGroup = ''AUDIO'')';
PRINT '============================================================================';

SELECT 
    MachineName,
    COUNT(*) AS TotalBaris,
    COUNT(DISTINCT ReportId) AS JumlahBatchReport,
    COUNT(DISTINCT ModelCode) AS JumlahModel,
    ROUND(MIN(TimeValue), 4) AS MinCycleTimeMin,
    ROUND(MAX(TimeValue), 4) AS MaxCycleTimeMin,
    ROUND(AVG(TimeValue), 4) AS AvgCycleTimeMin,
    SUM(PlanValue) AS TotalPlanSemuaUpdate,
    SUM(ActualTotal) AS TotalActualSemuaUpdate
FROM dbo.MachineData
WHERE ProductGroup = 'AUDIO'
GROUP BY MachineName
ORDER BY MachineName;
GO

PRINT '============================================================================';
PRINT '3. BUKTI UPDATE BERULANG & DEDUPLIKASI BATCH PADA MachineData';
PRINT 'Mengambil Snapshot Terakhir (MAX Id) per (ReportId, MachineName, ModelCode)';
PRINT '============================================================================';

-- Contoh Kasus Nyata: JVK 03 (ReportId: 2f9cbc43422843a4af67b1bea31810b1)
SELECT 
    Id,
    ReportId,
    MachineName,
    ModelCode,
    TimeValue,
    PlanValue,
    ActualTotal,
    CreatedAt,
    SourceUpdatedAt
FROM dbo.MachineData
WHERE ProductGroup = 'AUDIO' 
  AND ReportId = '2f9cbc43422843a4af67b1bea31810b1'
ORDER BY Id ASC;

-- Perbandingan Total Aktual: SUM Naif vs SUM Terdeduplikasi
WITH DeduplicatedMachineData AS (
    SELECT 
        Id,
        ReportId,
        MachineName,
        ModelCode,
        TimeValue,
        PlanValue,
        ActualTotal,
        CreatedAt,
        ROW_NUMBER() OVER (
            PARTITION BY ReportId, MachineName, ModelCode 
            ORDER BY Id DESC
        ) AS rn
    FROM dbo.MachineData
    WHERE ProductGroup = 'AUDIO'
)
SELECT 
    'SEMUA DATA HISTORIS AUDIO' AS Scope,
    SUM(m.ActualTotal) AS NaiveSumActual,
    SUM(CASE WHEN d.rn = 1 THEN d.ActualTotal ELSE 0 END) AS DeduplicatedSumActual,
    SUM(m.ActualTotal) - SUM(CASE WHEN d.rn = 1 THEN d.ActualTotal ELSE 0 END) AS SelisihDoubleCount
FROM dbo.MachineData m
LEFT JOIN DeduplicatedMachineData d ON m.Id = d.Id;
GO

PRINT '============================================================================';
PRINT '4. KOMPARASI KINERJA MESIN SUB-ASSEMBLY AUDIO BEBERAPA HARI';
PRINT 'Hari Evaluasi: 2026-10-20, 2026-10-19, 2026-10-16';
PRINT '============================================================================';

WITH Dedup AS (
    SELECT 
        Id,
        CAST(CreatedAt AS DATE) AS ProdDate,
        MachineName,
        ModelCode,
        TimeValue,
        PlanValue,
        ActualTotal,
        ROW_NUMBER() OVER (
            PARTITION BY ReportId, MachineName, ModelCode 
            ORDER BY Id DESC
        ) AS rn
    FROM dbo.MachineData
    WHERE ProductGroup = 'AUDIO'
)
SELECT 
    ProdDate,
    MachineName,
    COUNT(*) AS JumlahBatch,
    SUM(PlanValue) AS TotalPlan,
    SUM(ActualTotal) AS TotalActual,
    ROUND(SUM(ActualTotal * TimeValue), 2) AS StandardOperatingMinutes,
    ROUND(SUM(ActualTotal * TimeValue) / 60.0, 2) AS StandardOperatingHours,
    CASE 
        WHEN SUM(PlanValue) > 0 THEN ROUND((SUM(ActualTotal) * 1.0 / SUM(PlanValue)) * 100.0, 1)
        ELSE NULL 
    END AS AchievementPct
FROM Dedup
WHERE rn = 1 
  AND ProdDate IN ('2026-10-21', '2026-10-20', '2026-10-19', '2026-10-16')
GROUP BY ProdDate, MachineName
ORDER BY ProdDate DESC, MachineName;
GO

PRINT '============================================================================';
PRINT '5. KOMPARASI KPI FINAL ASSEMBLY AUDIO (2026-10-21)';
PRINT 'Formula:';
PRINT 'Availability = (LoadingSec - DowntimeSec) / LoadingSec * 100%';
PRINT 'Performance  = (DeltaActual * Sut) / OperatingSec * 100%';
PRINT 'Quality      = (DeltaActual - DefectQty) / DeltaActual * 100%';
PRINT 'OEE          = Availability * Performance * Quality';
PRINT '============================================================================';

DECLARE @WinStart DATETIME2 = '2026-10-21 07:00:00';
DECLARE @WinEnd   DATETIME2 = '2026-10-22 07:00:00';
DECLARE @Shift1LoadingSec INT = 32100; -- 8 jam 55 menit = 535 menit * 60 detik

-- Data Produksi Snapshot FINAL1 (Model: RF-2400DEG-K & RF-2400DLJ-K)
;WITH ProdData AS (
    SELECT 
        'FINAL1' AS LineCode,
        MAX(Actual) - MIN(Actual) AS DeltaActual,
        MAX(Target) - MIN(Target) AS DeltaTarget,
        MAX(Sut) AS SutSec
    FROM dbo.FINAL1
    WHERE DateTime >= @WinStart AND DateTime < @WinEnd
),
LossData AS (
    SELECT 
        MachineCode AS LineCode,
        SUM(LossTime) AS DowntimeSec
    FROM dbo.AssemblyLossTime
    WHERE Time >= @WinStart AND Time < @WinEnd AND LossTime <= 28800
    GROUP BY MachineCode
),
DefectData AS (
    SELECT 
        MachineCode AS LineCode,
        SUM(ISNULL(Quantity, 0)) AS DefectQty
    FROM dbo.DEFECTT
    WHERE DateTime >= @WinStart AND DateTime < @WinEnd
    GROUP BY MachineCode
)
SELECT 
    p.LineCode,
    p.DeltaActual AS OutputActual,
    p.DeltaTarget AS OutputTarget,
    p.SutSec,
    ISNULL(l.DowntimeSec, 0) AS DowntimeSec,
    ROUND(ISNULL(l.DowntimeSec, 0) / 60.0, 1) AS DowntimeMin,
    ISNULL(d.DefectQty, 0) AS DefectQty,
    
    -- Availability %
    ROUND(((@Shift1LoadingSec - ISNULL(l.DowntimeSec, 0)) * 1.0 / @Shift1LoadingSec) * 100.0, 2) AS AvailabilityPct,
    
    -- Performance %
    ROUND(((p.DeltaActual * p.SutSec) * 1.0 / NULLIF((@Shift1LoadingSec - ISNULL(l.DowntimeSec, 0)), 0)) * 100.0, 2) AS PerformancePct,
    
    -- Quality %
    ROUND(((p.DeltaActual - ISNULL(d.DefectQty, 0)) * 1.0 / NULLIF(p.DeltaActual, 0)) * 100.0, 2) AS QualityPct,

    -- Line Efficiency % (Actual * Sut / LoadingTime)
    ROUND(((p.DeltaActual * p.SutSec) * 1.0 / @Shift1LoadingSec) * 100.0, 2) AS LineEfficiencyPct
FROM ProdData p
LEFT JOIN LossData l ON p.LineCode = l.LineCode
LEFT JOIN DefectData d ON p.LineCode = d.LineCode;
GO
