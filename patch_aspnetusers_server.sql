-- =====================================================================================
-- SCRIPT PATCH KHUSUS TABEL dbo.AspNetUsers & dbo.AspNetUserRoles
-- Database Target: AUDIO_PRODUCTION di Server 137.40.93.165
-- =====================================================================================

USE [AUDIO_PRODUCTION];
GO

-- 1. Tambahkan kolom kustom yang belum ada ke dbo.AspNetUsers
IF COL_LENGTH('dbo.AspNetUsers', 'FullName') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD FullName NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AspNetUsers_FullName DEFAULT (N'');

IF COL_LENGTH('dbo.AspNetUsers', 'IsActive') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD IsActive BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsActive DEFAULT ((1));

IF COL_LENGTH('dbo.AspNetUsers', 'Theme') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD Theme NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AspNetUsers_Theme DEFAULT (N'light');

IF COL_LENGTH('dbo.AspNetUsers', 'CreatedAt') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt DEFAULT (GETDATE());

IF COL_LENGTH('dbo.AspNetUsers', 'LastPeriodId') IS NULL
    ALTER TABLE dbo.AspNetUsers ADD LastPeriodId INT NULL;
GO

-- 2. Pastikan Index pada dbo.AspNetUsers
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'EmailIndex' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    CREATE NONCLUSTERED INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail ASC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UserNameIndex' AND object_id = OBJECT_ID('dbo.AspNetUsers'))
    CREATE UNIQUE NONCLUSTERED INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName ASC) WHERE [NormalizedUserName] IS NOT NULL;
GO

-- 3. Masukkan / Sinkronkan Akun Default ke dbo.AspNetUsers
-- Akun 1: admin@audio.local (Password: AdminAudio123!)
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE NormalizedUserName = 'ADMIN@AUDIO.LOCAL')
BEGIN
    INSERT INTO dbo.AspNetUsers 
    (
        Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
        PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
        TwoFactorEnabled, LockoutEnabled, AccessFailedCount, CreatedAt, FullName, IsActive, Theme
    )
    VALUES
    (
        N'b37119ff-356c-4860-911e-289569fa1aa9',
        N'admin@audio.local',
        N'ADMIN@AUDIO.LOCAL',
        N'admin@audio.local',
        N'ADMIN@AUDIO.LOCAL',
        1,
        N'AQAAAAIAAYagAAAAEJdibM18HH40YALUYIFR0hubfCv5463jqiv+gB6BOD+CZ2+jmbFS8Pu6w7FGBYxMMQ==',
        N'HQCGP22C25FEX4T236T3L22UOF2J2I42',
        N'bb219f8d-4f11-4040-af80-6060c5a24ec0',
        0, 0, 1, 0, GETDATE(), N'Administrator Audio', 1, N'light'
    );
    PRINT 'User admin@audio.local berhasil dimasukkan ke dbo.AspNetUsers.';
END
ELSE
BEGIN
    UPDATE dbo.AspNetUsers 
    SET FullName = N'Administrator Audio', IsActive = 1, Theme = N'light'
    WHERE NormalizedUserName = 'ADMIN@AUDIO.LOCAL';
    PRINT 'User admin@audio.local sudah ada, data diperbarui.';
END
GO

-- Akun 2: operator@audio.local (Password: Operator123!)
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUsers WHERE NormalizedUserName = 'OPERATOR@AUDIO.LOCAL')
BEGIN
    INSERT INTO dbo.AspNetUsers 
    (
        Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed,
        PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed,
        TwoFactorEnabled, LockoutEnabled, AccessFailedCount, CreatedAt, FullName, IsActive, Theme
    )
    VALUES
    (
        N'b6045617-5e92-4cb6-ba5a-f1165fc7ce68',
        N'operator@audio.local',
        N'OPERATOR@AUDIO.LOCAL',
        N'operator@audio.local',
        N'OPERATOR@AUDIO.LOCAL',
        1,
        N'AQAAAAIAAYagAAAAEN34WBNzTjFIq7ZTBWtmMC1sgNMxIL6a4X/MXKi3YZK0WIvpOysqV1vinRWfyy9Deg==',
        N'LGYV5Y3E2HBF5ZTYH3Y3VOF2J2I42AAA',
        N'1b118f8d-4f11-4040-af80-6060c5a24ec0',
        0, 0, 1, 0, GETDATE(), N'Operator Audio', 1, N'light'
    );
    PRINT 'User operator@audio.local berhasil dimasukkan ke dbo.AspNetUsers.';
END
ELSE
BEGIN
    UPDATE dbo.AspNetUsers 
    SET FullName = N'Operator Audio', IsActive = 1, Theme = N'light'
    WHERE NormalizedUserName = 'OPERATOR@AUDIO.LOCAL';
    PRINT 'User operator@audio.local sudah ada, data diperbarui.';
END
GO

-- 4. Hubungkan User ke Role di dbo.AspNetUserRoles
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = N'b37119ff-356c-4860-911e-289569fa1aa9' AND RoleId = N'b2cdcb33-2bfb-4bda-a887-fa74c206013a')
BEGIN
    INSERT INTO dbo.AspNetUserRoles (UserId, RoleId) VALUES (N'b37119ff-356c-4860-911e-289569fa1aa9', N'b2cdcb33-2bfb-4bda-a887-fa74c206013a');
    PRINT 'admin@audio.local berhasil dikaitkan ke Role Administrator.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AspNetUserRoles WHERE UserId = N'b6045617-5e92-4cb6-ba5a-f1165fc7ce68' AND RoleId = N'8a79feae-62df-4c86-8208-d21f8aefef4b')
BEGIN
    INSERT INTO dbo.AspNetUserRoles (UserId, RoleId) VALUES (N'b6045617-5e92-4cb6-ba5a-f1165fc7ce68', N'8a79feae-62df-4c86-8208-d21f8aefef4b');
    PRINT 'operator@audio.local berhasil dikaitkan ke Role Operator.';
END
GO

PRINT '=== SELESAI: dbo.AspNetUsers & dbo.AspNetUserRoles TELAH LENGKAP 100% ===';
GO
