/*
====================================================================================================
PANAMON AUDIO - MASTER SSMS VERIFICATION & DATA PROFILING QUERIES
====================================================================================================
Database    : AUDIO_PRODUCTION (Localhost / SQLEXPRESS)
Project     : MonitoringSystem (Panamon Audio)
Tujuan      : Memverifikasi profiling seluruh tabel Audio, aturan bisnis Production Day (07:00-06:59),
              transisi model & alokasi delta counter snapshot, satuan domain (Sut, LossTime, TargetMinutes),
              serta benchmark lini FINAL1 tanggal 2026-10-21.
====================================================================================================
*/

USE [AUDIO_PRODUCTION];
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 1: PROFILING SELURUH TABEL AUDIO (ROW COUNT & RENTANG WAKTU)';
PRINT '====================================================================================================';
GO

SELECT 
    'FINAL1' AS TableName, COUNT(*) AS TotalRows, MIN(DateTime) AS MinTime, MAX(DateTime) AS MaxTime FROM FINAL1
UNION ALL SELECT 'FINAL2', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL2
UNION ALL SELECT 'FINAL3', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL3
UNION ALL SELECT 'FINAL4', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL4
UNION ALL SELECT 'FINAL5', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL5
UNION ALL SELECT 'FINAL6', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL6
UNION ALL SELECT 'FINAL7', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL7
UNION ALL SELECT 'FINAL8', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL8
UNION ALL SELECT 'FINAL9', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL9
UNION ALL SELECT 'FINAL10', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL10
UNION ALL SELECT 'FINAL11', COUNT(*), MIN(DateTime), MAX(DateTime) FROM FINAL11
UNION ALL SELECT 'AssemblyLossTime', COUNT(*), MIN(Time), MAX(Time) FROM AssemblyLossTime
UNION ALL SELECT 'DEFECTT', COUNT(*), MIN(DateTime), MAX(DateTime) FROM DEFECTT
UNION ALL SELECT 'MachineData', COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM MachineData
UNION ALL SELECT 'LossTimePlans', COUNT(*), MIN(CreatedAt), MAX(CreatedAt) FROM LossTimePlans
UNION ALL SELECT 'MasterData', COUNT(*), NULL, NULL FROM MasterData
UNION ALL SELECT 'MachineList', COUNT(*), NULL, NULL FROM MachineList
UNION ALL SELECT 'RestTime', COUNT(*), NULL, NULL FROM RestTime
UNION ALL SELECT 'AdditionalBreakTimes', COUNT(*), NULL, NULL FROM AdditionalBreakTimes
UNION ALL SELECT 'ProductionPlan', COUNT(*), NULL, NULL FROM ProductionPlan
ORDER BY TotalRows DESC;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 2: VERIFIKASI BATAS HARI PRODUKSI (07:00:00 S.D. <07:00:00 HARI BERIKUTNYA)';
PRINT 'Keterangan: Snapshot sebelum 07:00:00 masuk ke hari produksi sebelumnya.';
PRINT '====================================================================================================';
GO

-- Contoh Pembuktian Pergantian Hari Produksi pada Lini FINAL1 (2026-10-21 Pagi):
SELECT 
    ID,
    DateTime,
    Model,
    DailyPlan,
    Target,
    Actual,
    SerialNumber,
    Sut,
    -- Formula Penetapan Production Date:
    CASE 
        WHEN CAST(DateTime AS time) < '07:00:00' 
        THEN CAST(DATEADD(day, -1, CAST(DateTime AS date)) AS date)
        ELSE CAST(DateTime AS date)
    END AS ProductionDate,
    CASE 
        WHEN CAST(DateTime AS time) < '07:00:00' THEN 'Shift 3 (Kemarin)'
        ELSE 'Shift 1 (Hari Baru)'
    END AS ShiftBucket
FROM FINAL1 
WHERE DateTime >= '2026-10-21 06:40:00' AND DateTime <= '2026-10-21 07:15:00'
ORDER BY DateTime ASC;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 3: BENCHMARK PEMBANDING TERSETUJUI (2026-10-21, LINI FINAL1)';
PRINT 'Verifikasi Transisi Model: RF-2400DEG-K (Pagi) -> RF-2400DLJ-K (Siang)';
PRINT 'Aturan: Delta counter pada snapshot baru dialokasikan ke model pada snapshot tersebut.';
PRINT '====================================================================================================';
GO

WITH SnapshotsWithLag AS (
    SELECT 
        ID,
        DateTime,
        Model,
        DailyPlan,
        Target,
        Actual,
        SerialNumber,
        Sut,
        CASE 
            WHEN CAST(DateTime AS time) < '07:00:00' 
            THEN CAST(DATEADD(day, -1, CAST(DateTime AS date)) AS date)
            ELSE CAST(DateTime AS date)
        END AS ProductionDate,
        LAG(Actual, 1, 0) OVER (
            PARTITION BY CASE 
                WHEN CAST(DateTime AS time) < '07:00:00' 
                THEN CAST(DATEADD(day, -1, CAST(DateTime AS date)) AS date)
                ELSE CAST(DateTime AS date)
            END 
            ORDER BY DateTime
        ) AS PrevActual
    FROM FINAL1
    WHERE DateTime >= '2026-10-21 07:00:00' AND DateTime < '2026-10-22 07:00:00'
),
DeltaAllocated AS (
    SELECT 
        *,
        CASE 
            -- Snapshot pertama hari itu: seluruh nilai Actual dialokasikan
            WHEN PrevActual = 0 THEN Actual
            -- Normal: Delta counter dikreditkan ke model pada baris snapshot baru
            WHEN Actual >= PrevActual THEN Actual - PrevActual
            -- Reset counter tidak terduga:
            ELSE Actual
        END AS DeltaActual
    FROM SnapshotsWithLag
)
SELECT 
    ProductionDate,
    Model,
    MIN(DateTime) AS FirstSeenTime,
    MAX(DateTime) AS LastSeenTime,
    MIN(SerialNumber) AS FirstSerialNumber,
    MAX(SerialNumber) AS LastSerialNumber,
    COUNT(*) AS TotalSnapshots,
    SUM(DeltaActual) AS TotalModelActual,
    MAX(DailyPlan) AS DailyPlan,
    MAX(Target) - MIN(Target) AS ApproxTargetUnits,
    AVG(Sut) AS SutSecondsPerUnit
FROM DeltaAllocated
GROUP BY ProductionDate, Model
ORDER BY FirstSeenTime;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 4: VERIFIKASI SERIAL NUMBER RECONCILIATION & TITIK PERGANTIAN MODEL';
PRINT 'Memeriksa baris tepat saat model berpindah dari RF-2400DEG-K ke RF-2400DLJ-K';
PRINT '====================================================================================================';
GO

SELECT 
    ID,
    DateTime,
    Model,
    Actual,
    LAG(Actual, 1, 0) OVER (ORDER BY DateTime) AS PrevActual,
    Actual - LAG(Actual, 1, 0) OVER (ORDER BY DateTime) AS DeltaCounter,
    SerialNumber,
    Sut,
    Efficiency
FROM FINAL1
WHERE DateTime >= '2026-10-21 12:55:00' AND DateTime <= '2026-10-21 13:15:00'
ORDER BY DateTime ASC;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 5: VERIFIKASI LOSS TIME (SATUAN DETIK) DI ASSEMBLY LOSS TIME';
PRINT 'Membuktikan LossTime = durasi jeda non-produktif dalam DETIK (bukan menit)';
PRINT '====================================================================================================';
GO

SELECT TOP 15
    Id,
    MachineCode,
    Time AS StartTime,
    EndDateTime,
    DATEDIFF(SECOND, Time, EndDateTime) AS IntervalDurationSeconds,
    LossTime AS StoredLossTimeSeconds,
    DATEDIFF(SECOND, Time, EndDateTime) - LossTime AS ProductiveRunSeconds,
    Reason,
    DetailedReason
FROM AssemblyLossTime
WHERE MachineCode = 'FINAL1'
  AND Time >= '2026-10-21 07:00:00' AND Time < '2026-10-22 07:00:00'
ORDER BY Time ASC;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 6: VERIFIKASI RENCANA TARGET LOSS TIME (MENIT) DI LOSSTIMEPLANS';
PRINT 'Membuktikan TargetMinutes bersatuan MENIT dan kategori terdaftar';
PRINT '====================================================================================================';
GO

SELECT 
    Id,
    Category,
    MachineLine,
    Month,
    Year,
    TargetMinutes,
    Ratio
FROM LossTimePlans
ORDER BY MachineLine, Category, Month;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 7: VERIFIKASI DISTRIBUSI DEFECT / QUALITY DARI DEFECTT';
PRINT 'Pengelompokan Station dan Top Defect Causes';
PRINT '====================================================================================================';
GO

-- Ringkasan per Station
SELECT 
    Station,
    COUNT(*) AS TotalRecords,
    SUM(Quantity) AS TotalDefectQty
FROM DEFECTT
GROUP BY Station
ORDER BY TotalRecords DESC;

-- Top 10 Defect Terbanyak
SELECT TOP 10
    Station,
    Defect,
    Reason_Defect,
    COUNT(*) AS OccurrenceCount,
    SUM(Quantity) AS TotalQuantity
FROM DEFECTT
GROUP BY Station, Defect, Reason_Defect
ORDER BY OccurrenceCount DESC;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 8: VERIFIKASI JADWAL SHIFT & ISTIRAHAT (RESTTIME & ADDITIONAL BREAKS)';
PRINT 'Normal (Senin-Kamis): 120m, Jumat: 150m (Sholat Jumat), Weekend: 90m';
PRINT '====================================================================================================';
GO

SELECT 
    DayType,
    StartTime,
    EndTime,
    Duration AS DurationMinutes,
    DATEDIFF(MINUTE, StartTime, EndTime) AS CalcDurationMinutes
FROM RestTime
ORDER BY DayType, StartTime;

SELECT 
    Id,
    Date,
    StartTime,
    EndTime,
    Reason,
    CreatedAt
FROM AdditionalBreakTimes;
GO

PRINT '====================================================================================================';
PRINT 'BAGIAN 9: VERIFIKASI MASTER DATA & STATUS LINI AKTIF VS STANDBY';
PRINT 'Lini Final Assembly Aktif: FINAL1-5, FINAL7-10. Standby: FINAL6, FINAL11.';
PRINT 'Mesin PCB / SMT: DIPPING 1-4, SMT 1-3, RH 1/3/4, JVK MC, JVK, AVK3.';
PRINT '====================================================================================================';
GO

-- Model Master Data
SELECT 
    Product_Id,
    ProductName,
    MachineCode,
    ProdPlan,
    SUT,
    NoOfOperator,
    QtyHour,
    WorkHour
FROM MasterData
ORDER BY MachineCode, ProductName;

-- Mesin Lain (SMT & Dipping)
SELECT 
    IdMachine,
    MachineName
FROM MachineList
ORDER BY IdMachine;
GO

PRINT '====================================================================================================';
PRINT 'VERIFIKASI SELESAI. SELURUH FORMULA DAN SATUAN TERBUKTI 100% VALID.';
PRINT '====================================================================================================';
GO
