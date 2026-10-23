/*
====================================================================================================
PANAMON AUDIO - MASTER DATA & PRODUCT IMPORT & SINKRONISASI
====================================================================================================
Database    : AUDIO_PRODUCTION
Target Tabel: dbo.MasterData, dbo.Product, dbo.AudioLineMaster
Fungsi      : Melakukan impor tervalidasi master model audio, SUT, kapasitas, operator untuk lini
              FINAL1 - FINAL11, mensinkronkan tabel Product (yang sebelumnya hanya 1 baris),
              serta menandai status standby pada FINAL6 dan FINAL11.
====================================================================================================
*/

USE [AUDIO_PRODUCTION];
GO

SET NOCOUNT ON;
GO

PRINT '====================================================================================================';
PRINT 'LANGKAH 1: MEMASTIKAN TABEL MASTER LINI (dbo.AudioLineMaster) TERDAFTAR LENGKAP';
PRINT '====================================================================================================';
GO

IF OBJECT_ID('dbo.AudioLineMaster', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AudioLineMaster
    (
        LineCode            NVARCHAR(20) NOT NULL CONSTRAINT PK_AudioLineMaster PRIMARY KEY,
        DisplayName         NVARCHAR(100) NOT NULL,
        IsActive            BIT NOT NULL CONSTRAINT DF_AudioLineMaster_IsActive DEFAULT 1,
        Status              NVARCHAR(50) NOT NULL,
        DefaultCapacity     INT NOT NULL CONSTRAINT DF_AudioLineMaster_Capacity DEFAULT 1000,
        DefaultOperators    INT NOT NULL CONSTRAINT DF_AudioLineMaster_Operators DEFAULT 25,
        DefaultSutSeconds   INT NOT NULL CONSTRAINT DF_AudioLineMaster_Sut DEFAULT 30,
        Notes               NVARCHAR(500) NULL,
        CreatedAt           DATETIME2 NOT NULL CONSTRAINT DF_AudioLineMaster_CreatedAt DEFAULT GETDATE(),
        UpdatedAt           DATETIME2 NULL
    );
END

MERGE dbo.AudioLineMaster AS Target
USING (VALUES
    ('FINAL1', 'Final Assembly 1', 1, 'Active', 1000, 25, 30, 'Lini aktif utama perakitan radio portable dan audio home'),
    ('FINAL2', 'Final Assembly 2', 1, 'Active', 1000, 25, 30, 'Lini aktif perakitan seri RF-2400'),
    ('FINAL3', 'Final Assembly 3', 1, 'Active', 1000, 25, 25, 'Lini aktif perakitan seri pocket RF-P150 / RF-P50'),
    ('FINAL4', 'Final Assembly 4', 1, 'Active', 800,  28, 40, 'Lini aktif perakitan radio digital RF-D10 / RF-3500'),
    ('FINAL5', 'Final Assembly 5', 1, 'Active', 800,  28, 40, 'Lini aktif perakitan radio digital RF-D10'),
    ('FINAL6', 'Final Assembly 6', 0, 'Standby (Kosong)', 0, 0, 30, 'Belum tersedia data produksi (tabel fisik kosong) / Standby'),
    ('FINAL7', 'Final Assembly 7', 1, 'Active', 1000, 25, 30, 'Lini aktif perakitan seri RF-2400 dan Bluetooth RF-727'),
    ('FINAL8', 'Final Assembly 8', 1, 'Active', 1000, 25, 30, 'Lini aktif perakitan seri RF-2400 dan RF-U156'),
    ('FINAL9', 'Final Assembly 9', 1, 'Active', 1000, 25, 30, 'Lini aktif perakitan seri RF-562 / RF-5270'),
    ('FINAL10', 'Final Assembly 10', 1, 'Active', 1200, 22, 25, 'Lini aktif perakitan pocket radio RF-P155 / RF-P55'),
    ('FINAL11', 'Final Assembly 11', 0, 'Standby (Kosong)', 0, 0, 30, 'Belum tersedia data produksi (tabel fisik kosong) / Standby')
) AS Source (LineCode, DisplayName, IsActive, Status, DefaultCapacity, DefaultOperators, DefaultSutSeconds, Notes)
ON Target.LineCode = Source.LineCode
WHEN MATCHED THEN
    UPDATE SET 
        DisplayName = Source.DisplayName,
        IsActive = Source.IsActive,
        Status = Source.Status,
        DefaultCapacity = Source.DefaultCapacity,
        DefaultOperators = Source.DefaultOperators,
        DefaultSutSeconds = Source.DefaultSutSeconds,
        Notes = Source.Notes,
        UpdatedAt = GETDATE()
WHEN NOT MATCHED THEN
    INSERT (LineCode, DisplayName, IsActive, Status, DefaultCapacity, DefaultOperators, DefaultSutSeconds, Notes, CreatedAt)
    VALUES (Source.LineCode, Source.DisplayName, Source.IsActive, Source.Status, Source.DefaultCapacity, Source.DefaultOperators, Source.DefaultSutSeconds, Source.Notes, GETDATE());
GO

PRINT '====================================================================================================';
PRINT 'LANGKAH 2: IMPOR TERVALIDASI MASTER DATA MODEL PRODUKSI AUDIO (FINAL1 - FINAL10)';
PRINT 'Keterangan: Memasukkan model riil yang tercatat di logger snapshot produksi.';
PRINT '====================================================================================================';
GO

-- Tabel staging validasi
DECLARE @ValidModels TABLE (
    ProductName     VARCHAR(100) NOT NULL,
    MachineCode     VARCHAR(50) NOT NULL,
    Description     VARCHAR(255) NULL,
    ProdPlan        INT NOT NULL,
    SUT             INT NOT NULL,
    NoOfOperator    INT NOT NULL,
    QtyHour         INT NOT NULL,
    ProdHeadHour    INT NOT NULL,
    CycleTimeVacum  INT NOT NULL,
    WorkHour        INT NOT NULL
);

INSERT INTO @ValidModels (ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour)
VALUES
-- FINAL1 Models (Sudah ada di MasterData, diverifikasi)
('RF-2400DEG-K', 'FINAL1', 'Portable Radio FM/AM', 1000, 30, 25, 142, 4, 90, 7),
('RF-2450-S',     'FINAL1', 'Portable Radio 2-Band', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DGN-S',  'FINAL1', 'Portable Radio FM/AM Silver', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DPC-S',  'FINAL1', 'Portable Radio FM/AM Gray', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DGT-S',  'FINAL1', 'Portable Radio FM/AM Gold', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DP-S',   'FINAL1', 'Portable Radio FM/AM Standard', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DLJ-K',  'FINAL1', 'Portable Radio FM/AM Black', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DEB-K',  'FINAL1', 'Portable Radio FM/AM EU Black', 1000, 30, 25, 142, 4, 90, 7),
('R-2255-S',      'FINAL1', 'Classic Radio Receiver', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DEE-K',  'FINAL1', 'Portable Radio FM/AM EE Black', 1000, 30, 25, 142, 4, 90, 7),

-- FINAL2 Models (Sebelumnya Unregistered di MasterData)
('RF-2400DEG-K', 'FINAL2', 'Portable Radio FM/AM', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DPC-S', 'FINAL2', 'Portable Radio FM/AM Gray', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DLJ-K', 'FINAL2', 'Portable Radio FM/AM Black', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DGN-S', 'FINAL2', 'Portable Radio FM/AM Silver', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DEB-K', 'FINAL2', 'Portable Radio FM/AM EU Black', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DGT-S', 'FINAL2', 'Portable Radio FM/AM Gold', 1000, 30, 25, 142, 4, 90, 7),
('RF-2450-S',     'FINAL2', 'Portable Radio 2-Band', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DP-S',   'FINAL2', 'Portable Radio FM/AM Standard', 1000, 30, 25, 142, 4, 90, 7),
('R-2255-S',      'FINAL2', 'Classic Radio Receiver', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DEE-K',  'FINAL2', 'Portable Radio FM/AM EE Black', 1000, 30, 25, 142, 4, 90, 7),

-- FINAL3 Models (Seri Pocket RF-P150 & RF-P50)
('RF-P150DGC-S', 'FINAL3', 'Pocket Radio Silver GC', 1000, 25, 25, 142, 4, 90, 7),
('RF-P50DEG-S',  'FINAL3', 'Pocket Radio Silver EG', 1000, 25, 25, 142, 4, 90, 7),
('RF-P150DEG-S', 'FINAL3', 'Pocket Radio Silver EG', 1000, 25, 25, 142, 4, 90, 7),
('RF-P50DGC-S',  'FINAL3', 'Pocket Radio Silver GC', 1000, 25, 25, 142, 4, 90, 7),
('RF-P50DLJ-S',  'FINAL3', 'Pocket Radio Silver LJ', 1000, 25, 25, 142, 4, 90, 7),
('RF-P150DGT-S', 'FINAL3', 'Pocket Radio Silver GT', 1000, 25, 25, 142, 4, 90, 7),
('RF-P50DGC-R',  'FINAL3', 'Pocket Radio Red GC',    1000, 25, 25, 142, 4, 90, 7),
('RF-P50DPR-S',  'FINAL3', 'Pocket Radio Silver PR', 1000, 25, 25, 142, 4, 90, 7),
('RF-P50DPP-S',  'FINAL3', 'Pocket Radio Silver PP', 1000, 25, 25, 142, 4, 90, 7),
('RF-P155-S',    'FINAL3', 'Pocket Radio Silver 155',1000, 25, 25, 142, 4, 90, 7),
('RF-P150DBAGA', 'FINAL3', 'Pocket Radio BAGA Edition', 1000, 25, 25, 142, 4, 90, 7),

-- FINAL4 Models (Radio Digital & Portable)
('RF-3500E-K',   'FINAL4', 'Digital Multi-Band Radio', 800, 40, 28, 114, 4, 90, 7),
('RF-D10EB-K',   'FINAL4', 'DAB+ Digital Radio Black', 800, 40, 28, 114, 4, 90, 7),
('RF-D10EG-K',   'FINAL4', 'DAB+ Digital Radio EG Black', 800, 40, 28, 114, 4, 90, 7),
('RF-D10GN-K',   'FINAL4', 'DAB+ Digital Radio GN Black', 800, 40, 28, 114, 4, 90, 7),
('RF-D10EG-W',   'FINAL4', 'DAB+ Digital Radio White', 800, 40, 28, 114, 4, 90, 7),
('RF-D10LJ2-K',  'FINAL4', 'DAB+ Digital Radio LJ2 Black', 800, 40, 28, 114, 4, 90, 7),

-- FINAL5 Models (Radio Digital)
('RF-D10EG-K',   'FINAL5', 'DAB+ Digital Radio EG Black', 800, 40, 28, 114, 4, 90, 7),
('RF-D10GN-K',   'FINAL5', 'DAB+ Digital Radio GN Black', 800, 40, 28, 114, 4, 90, 7),
('RF-D10EG-W',   'FINAL5', 'DAB+ Digital Radio White', 800, 40, 28, 114, 4, 90, 7),
('RF-D10EB-K',   'FINAL5', 'DAB+ Digital Radio Black', 800, 40, 28, 114, 4, 90, 7),

-- FINAL7 Models
('RF-2400DEG-K', 'FINAL7', 'Portable Radio FM/AM', 1000, 30, 25, 142, 4, 90, 7),
('RF-727BTLJ-K', 'FINAL7', 'Bluetooth Audio System', 1000, 30, 25, 142, 4, 90, 7),

-- FINAL8 Models
('RF-2400DEG-K', 'FINAL8', 'Portable Radio FM/AM', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DP-K',  'FINAL8', 'Portable Radio FM/AM Black Standard', 1000, 30, 25, 142, 4, 90, 7),
('RF-U156-S',    'FINAL8', 'Classic Portable Radio Silver', 1000, 30, 25, 142, 4, 90, 7),
('RF-2400DP-S',  'FINAL8', 'Portable Radio FM/AM Standard', 1000, 30, 25, 142, 4, 90, 7),

-- FINAL9 Models
('RF-562DD2GCK', 'FINAL9', 'High Output Retro Radio', 1000, 30, 25, 142, 4, 90, 7),
('RF-5270LJ-K',  'FINAL9', 'Portable Audio Radio Black', 1000, 30, 25, 142, 4, 90, 7),
('RF-562DD2LJK', 'FINAL9', 'High Output Retro Radio LJ', 1000, 30, 25, 142, 4, 90, 7),

-- FINAL10 Models (Pocket Radio Seri P155 & P55)
('RF-P155-S',    'FINAL10', 'Pocket Radio Silver 155', 1200, 25, 22, 171, 4, 90, 7),
('RF-P55-S',     'FINAL10', 'Pocket Radio Silver 55',  1200, 25, 22, 171, 4, 90, 7),
('RF-P50DGC-S',  'FINAL10', 'Pocket Radio Silver GC',  1200, 25, 22, 171, 4, 90, 7),
('RF-P150DEG-S', 'FINAL10', 'Pocket Radio Silver EG',  1200, 25, 22, 171, 4, 90, 7),
('RF-P150DGT-S', 'FINAL10', 'Pocket Radio Silver GT',  1200, 25, 22, 171, 4, 90, 7);

-- Validasi Aturan Bisnis:
-- SUT > 0, ProdPlan > 0, NoOfOperator > 0, MachineCode di FINAL1..11
INSERT INTO MasterData (ProductName, MachineCode, Description, ProdPlan, SUT, NoOfOperator, QtyHour, ProdHeadHour, CycleTimeVacum, WorkHour)
SELECT 
    v.ProductName,
    v.MachineCode,
    v.Description,
    v.ProdPlan,
    v.SUT,
    v.NoOfOperator,
    v.QtyHour,
    v.ProdHeadHour,
    v.CycleTimeVacum,
    v.WorkHour
FROM @ValidModels v
WHERE v.SUT > 0 
  AND v.ProdPlan > 0 
  AND v.NoOfOperator > 0
  AND v.MachineCode IN ('FINAL1','FINAL2','FINAL3','FINAL4','FINAL5','FINAL6','FINAL7','FINAL8','FINAL9','FINAL10','FINAL11')
  AND NOT EXISTS (
      SELECT 1 FROM MasterData md 
      WHERE md.ProductName = v.ProductName AND md.MachineCode = v.MachineCode
  );

PRINT 'Impor data valid ke dbo.MasterData selesai.';
GO

PRINT '====================================================================================================';
PRINT 'LANGKAH 3: SINKRONISASI KE TABEL dbo.Product';
PRINT 'Keterangan: Memastikan dbo.Product terisi lengkap sesuai MasterData (bukan hanya 1 baris).';
PRINT '====================================================================================================';
GO

INSERT INTO Product (ProductName, MachineCode, Description, Marking, Product_Id)
SELECT 
    md.ProductName,
    md.MachineCode,
    md.Description,
    '1' AS Marking,
    '1' AS Product_Id
FROM MasterData md
WHERE md.ProductName IS NOT NULL 
  AND LTRIM(RTRIM(md.ProductName)) <> ''
  AND NOT EXISTS (
      SELECT 1 FROM Product p 
      WHERE p.ProductName = md.ProductName 
        AND ((p.MachineCode = md.MachineCode) OR (p.MachineCode IS NULL AND md.MachineCode IS NULL))
  );

PRINT 'Sinkronisasi ke dbo.Product selesai.';
GO

PRINT '====================================================================================================';
PRINT 'LANGKAH 4: AUDIT AKHIR & STATUS PENDAFTARAN LINI AUDIO';
PRINT '====================================================================================================';
GO

-- 1. Status Lini
SELECT 
    LineCode,
    DisplayName,
    Status,
    IsActive,
    DefaultCapacity,
    DefaultOperators,
    DefaultSutSeconds,
    Notes
FROM AudioLineMaster
ORDER BY LineCode;

-- 2. Jumlah Model Terdaftar per Lini di MasterData vs Product
SELECT 
    COALESCE(md.MachineCode, p.MachineCode, 'UNKNOWN') AS LineCode,
    COUNT(DISTINCT md.ProductName) AS MasterDataModelCount,
    COUNT(DISTINCT p.ProductName) AS ProductModelCount
FROM MasterData md
FULL OUTER JOIN Product p ON md.ProductName = p.ProductName AND md.MachineCode = p.MachineCode
GROUP BY COALESCE(md.MachineCode, p.MachineCode, 'UNKNOWN')
ORDER BY LineCode;

-- 3. Identifikasi Model yang Masih Belum Terdaftar (jika ada snapshot baru di logger)
WITH ProdModels AS (
    SELECT MachineCode, Product_Id AS Model, MAX(CycleTime) AS ObservedSut, COUNT(*) AS SnapshotCount
    FROM OEESN
    WHERE Product_Id IS NOT NULL AND LTRIM(RTRIM(Product_Id)) <> ''
    GROUP BY MachineCode, Product_Id
)
SELECT 
    pm.MachineCode,
    pm.Model,
    pm.ObservedSut,
    pm.SnapshotCount,
    'PERLU DIINPUT' AS ActionRequired
FROM ProdModels pm
LEFT JOIN MasterData md ON pm.Model = md.ProductName AND pm.MachineCode = md.MachineCode
WHERE md.ProductName IS NULL
ORDER BY pm.MachineCode, pm.SnapshotCount DESC;
GO

PRINT '====================================================================================================';
PRINT 'PROSES IMPOR & SINKRONISASI SELESAI.';
PRINT '====================================================================================================';
GO
