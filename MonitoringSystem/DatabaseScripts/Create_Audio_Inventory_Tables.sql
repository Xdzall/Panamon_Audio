-- ==============================================================================================
-- SKRIP PEMBUATAN TABEL TRANSAKSI INVENTORY, LOKASI/POSISI, DAN SALDO AUDIO
-- Database: localhost\SQLEXPRESS.AUDIO_PRODUCTION
--
-- Tujuan:
-- 1. Menyediakan sumber transaksi masuk/keluar, mutasi, koreksi (adjustment), dan opname.
-- 2. Memetakan posisi fisik (WIP, Buffer, Aging, FG Warehouse, Dispatch).
-- 3. Menghubungkan audit serial unit fisik dengan rekonsiliasi output produksi.
-- ==============================================================================================

USE [AUDIO_PRODUCTION];
GO

-- 1. MASTER LOKASI / POSISI INVENTORY AUDIO
IF OBJECT_ID(N'dbo.AudioInventoryLocation', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AudioInventoryLocation (
        LocationCode NVARCHAR(50) NOT NULL PRIMARY KEY,
        LocationName NVARCHAR(100) NOT NULL,
        LocationCategory NVARCHAR(50) NOT NULL, -- 'WIP_BUFFER', 'LINE_ASSEMBLY', 'AGING_INSPECTION', 'FG_WAREHOUSE', 'SHIPPING_DOCK'
        CapacityLimit INT NOT NULL DEFAULT 0,
        Description NVARCHAR(255) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Tabel dbo.AudioInventoryLocation berhasil dibuat.';
END
GO

-- Seed Lokasi Standar Audio
MERGE INTO dbo.AudioInventoryLocation AS target
USING (VALUES
    ('SMT_BUFFER', 'Buffer PCB Sub-assembly (SMT & Insertion)', 'WIP_BUFFER', 10000, 'Penyimpanan PCB setelah perakitan SMD & komponen insertion'),
    ('FINAL_BUFFER', 'Buffer Material Final Assembly', 'WIP_BUFFER', 5000, 'Area antrean perakitan unit di lini Final'),
    ('FINAL1_OUTPUT', 'Output Perakitan Final Line 1', 'LINE_ASSEMBLY', 2000, 'Titik akhir sensor lini FINAL1'),
    ('FINAL2_OUTPUT', 'Output Perakitan Final Line 2', 'LINE_ASSEMBLY', 2000, 'Titik akhir sensor lini FINAL2'),
    ('FINAL3_OUTPUT', 'Output Perakitan Final Line 3', 'LINE_ASSEMBLY', 2000, 'Titik akhir sensor lini FINAL3'),
    ('AGING_ROOM', 'Ruang Uji Ketahanan & Burning Audio', 'AGING_INSPECTION', 3000, 'Area pengujian kelistrikan dan audio running test'),
    ('QC_HOLD', 'Karantina Kualitas (QC Inspection / Reject)', 'AGING_INSPECTION', 1000, 'Unit tahanan perbaikan atau verifikasi QC'),
    ('FG_WAREHOUSE', 'Gudang Barang Jadi (Finished Goods Warehouse)', 'FG_WAREHOUSE', 50000, 'Penyimpanan utama barang siap kirim'),
    ('DISPATCH_DOCK', 'Dermaga Pengiriman (Shipping / Delivery Dock)', 'SHIPPING_DOCK', 10000, 'Area muat truk/kontainer untuk distribusi')
) AS source (LocationCode, LocationName, LocationCategory, CapacityLimit, Description)
ON target.LocationCode = source.LocationCode
WHEN MATCHED THEN
    UPDATE SET 
        target.LocationName = source.LocationName,
        target.LocationCategory = source.LocationCategory,
        target.CapacityLimit = source.CapacityLimit,
        target.Description = source.Description
WHEN NOT MATCHED THEN
    INSERT (LocationCode, LocationName, LocationCategory, CapacityLimit, Description)
    VALUES (source.LocationCode, source.LocationName, source.LocationCategory, source.CapacityLimit, source.Description);
GO

-- 2. TABEL BUKU BESAR TRANSAKSI INVENTORY AUDIO (TRANSACTION LEDGER)
IF OBJECT_ID(N'dbo.AudioInventoryTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AudioInventoryTransaction (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TransactionDate DATETIME2 NOT NULL,
        TransactionType NVARCHAR(50) NOT NULL, -- 'INBOUND_LINE', 'OUTBOUND_SHIP', 'TRANSFER_LOCATION', 'SCRAP_REJECT', 'STOCK_ADJUSTMENT', 'INITIAL_OPNAME'
        ModelCode NVARCHAR(100) NOT NULL,
        SourceLocation NVARCHAR(50) NOT NULL,
        DestinationLocation NVARCHAR(50) NOT NULL,
        Quantity INT NOT NULL,
        SerialNumberStart NVARCHAR(100) NULL,
        SerialNumberEnd NVARCHAR(100) NULL,
        ReferenceDoc NVARCHAR(100) NULL,
        OperatorName NVARCHAR(100) NULL,
        Remarks NVARCHAR(500) NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE()
    );

    CREATE NONCLUSTERED INDEX IX_AudioInvTrans_Date_Model 
    ON dbo.AudioInventoryTransaction (TransactionDate DESC, ModelCode ASC);

    CREATE NONCLUSTERED INDEX IX_AudioInvTrans_Locations 
    ON dbo.AudioInventoryTransaction (SourceLocation ASC, DestinationLocation ASC);

    PRINT 'Tabel dbo.AudioInventoryTransaction berhasil dibuat.';
END
GO

-- 3. SEED CONTOH TRANSAKSI AWAL & KOREKSI AGAR MODUL LANGSUNG DAPAT DIUJI & DIREKONSILIASI
IF NOT EXISTS (SELECT 1 FROM dbo.AudioInventoryTransaction)
BEGIN
    INSERT INTO dbo.AudioInventoryTransaction 
        (TransactionDate, TransactionType, ModelCode, SourceLocation, DestinationLocation, Quantity, SerialNumberStart, SerialNumberEnd, ReferenceDoc, OperatorName, Remarks)
    VALUES
        -- Saldo Awal Opname di Gudang FG
        ('2026-10-21 06:00:00', 'INITIAL_OPNAME', 'RF-2400DLJ-K', 'FG_WAREHOUSE', 'FG_WAREHOUSE', 1200, 'QA6JD000001', 'QA6JD001200', 'OPN-20261021-01', 'Admin Gudang', 'Saldo awal stock opname fisik gudang FG'),
        -- Transfer Output Line Final 1 ke Aging Room
        ('2026-10-21 16:00:00', 'INBOUND_LINE', 'RF-2400DLJ-K', 'FINAL1_OUTPUT', 'AGING_ROOM', 840, 'QA6JD001001', 'QA6JD001840', 'TRF-FINAL1-20261021', 'Supervisor Final 1', 'Transfer hasil produksi shift 1 ke ruang uji ketahanan'),
        -- Transfer dari Aging ke FG Warehouse
        ('2026-10-21 18:00:00', 'TRANSFER_LOCATION', 'RF-2400DLJ-K', 'AGING_ROOM', 'FG_WAREHOUSE', 835, 'QA6JD001001', 'QA6JD001835', 'TRF-AGING-FG-01', 'QC Leader', 'Unit lolos QC masuk rak penyimpanan FG'),
        -- Reject Unit saat uji Aging ke QC Hold
        ('2026-10-21 18:15:00', 'SCRAP_REJECT', 'RF-2400DLJ-K', 'AGING_ROOM', 'QC_HOLD', 5, 'QA6JD001836', 'QA6JD001840', 'REJ-QC-20261021', 'QC Inspector', 'Unit gagal uji suara dial / frequency noise'),
        -- Pengiriman Barang Jadi ke Ekspor / Distribusi
        ('2026-10-21 20:00:00', 'OUTBOUND_SHIP', 'RF-2400DLJ-K', 'FG_WAREHOUSE', 'DISPATCH_DOCK', 600, 'QA6JD000100', 'QA6JD000699', 'DO-EXP-20261021-99', 'Logistik Ekspor', 'Surat Jalan Pengiriman Kontainer No 8912'),
        -- Koreksi Fisik (Adjustment)
        ('2026-10-21 21:00:00', 'STOCK_ADJUSTMENT', 'RF-2400DLJ-K', 'FG_WAREHOUSE', 'FG_WAREHOUSE', -2, NULL, NULL, 'ADJ-SO-20261021', 'Inventory Control', 'Koreksi penyesuaian selisih fisik karton rusak di rak A2');

    PRINT 'Seed data transaksi inventory Audio berhasil diisikan.';
END
GO
