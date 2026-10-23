-- =====================================================================================
-- SCRIPT PEMBUATAN & SINKRONISASI TABEL USER & ROLES (SERVER 137.40.93.165)
-- Database Target: AUDIO_PRODUCTION
-- Dijalankan melalui: SQL Server Management Studio (SSMS)
-- =====================================================================================

USE [AUDIO_PRODUCTION];
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- =====================================================================================
-- 1. TABEL UTAMA: dbo.Roles (Panamon / Promosys Core Role)
-- =====================================================================================
IF OBJECT_ID('dbo.Roles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Roles
    (
        RoleId      INT NOT NULL,
        RoleName    NVARCHAR(MAX) NOT NULL,
        CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (RoleId ASC)
    );
    PRINT 'Tabel dbo.Roles berhasil dibuat.';
END
ELSE
BEGIN
    PRINT 'Tabel dbo.Roles sudah ada.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 1)
    INSERT INTO dbo.Roles (RoleId, RoleName) VALUES (1, N'Admin');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 2)
    INSERT INTO dbo.Roles (RoleId, RoleName) VALUES (2, N'User');
IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleId = 3)
    INSERT INTO dbo.Roles (RoleId, RoleName) VALUES (3, N'Manajemen');
GO

-- =====================================================================================
-- 2. TABEL UTAMA: dbo.Users (Panamon / Promosys Core Users)
-- =====================================================================================
IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        UserId          INT IDENTITY(1,1) NOT NULL,
        Username        NVARCHAR(MAX) NOT NULL,
        PasswordHash    NVARCHAR(MAX) NOT NULL,
        FullName        NVARCHAR(MAX) NOT NULL,
        RoleId          INT NOT NULL,
        IsActive        BIT NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT ((1)),
        LastPeriodId    INT NULL,
        Theme           NVARCHAR(MAX) NOT NULL CONSTRAINT DF_Users_Theme DEFAULT (N'light'),
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (UserId ASC),
        CONSTRAINT FK_Users_Roles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.Roles (RoleId)
    );
    PRINT 'Tabel dbo.Users berhasil dibuat.';
END
ELSE
BEGIN
    PRINT 'Tabel dbo.Users sudah ada.';
END
GO

-- Seed Data Akun Bawaan dbo.Users (Password BCrypt)
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'admin')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, RoleId, IsActive, Theme, CreatedAt)
    VALUES (N'admin', N'$2a$11$o/uAq8YP2ogcAxgr5QAjh.9jtqe2lJ9CZeA7bd0a049JAP49khcJS', N'Administrator Audio', 1, 1, N'light', GETDATE());
    PRINT 'User admin berhasil dimasukkan ke dbo.Users.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'operator')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, RoleId, IsActive, Theme, CreatedAt)
    VALUES (N'operator', N'$2a$11$YPu3f5XXBTAno87GspNBPOP.Aa38/IyGH1/REO/0rnsFg9k7lW/sS', N'Operator Audio', 2, 1, N'light', GETDATE());
    PRINT 'User operator berhasil dimasukkan ke dbo.Users.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = 'manajemen')
BEGIN
    INSERT INTO dbo.Users (Username, PasswordHash, FullName, RoleId, IsActive, Theme, CreatedAt)
    VALUES (N'manajemen', N'$2a$11$ZU/Df/V2uH6.0KvmaecBv.CPM3lSg9eqCWhKkvHIbNESJPcBjBLqu', N'Manajemen Audio', 3, 1, N'light', GETDATE());
    PRINT 'User manajemen berhasil dimasukkan ke dbo.Users.';
END
GO

-- =====================================================================================
-- 3. TABEL ASP.NET CORE IDENTITY: dbo.AspNetRoles
-- =====================================================================================
IF OBJECT_ID('dbo.AspNetRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoles
    (
        Id                  NVARCHAR(450) NOT NULL,
        Name                NVARCHAR(256) NULL,
        NormalizedName      NVARCHAR(256) NULL,
        ConcurrencyStamp    NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoles PRIMARY KEY CLUSTERED (Id ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName ASC) WHERE [NormalizedName] IS NOT NULL;
    PRINT 'Tabel dbo.AspNetRoles berhasil dibuat.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'ADMINISTRATOR')
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'b2cdcb33-2bfb-4bda-a887-fa74c206013a', N'Administrator', N'ADMINISTRATOR', NEWID());
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'SUPERVISOR')
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'cb31ad0c-b26a-4ee1-b9da-8b2b73bc36fc', N'Supervisor', N'SUPERVISOR', NEWID());
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'OPERATOR')
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'8a79feae-62df-4c86-8208-d21f8aefef4b', N'Operator', N'OPERATOR', NEWID());
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'USER')
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'e0b1f246-857e-402a-9f4c-1d07267eb4e2', N'User', N'USER', NEWID());
IF NOT EXISTS (SELECT 1 FROM dbo.AspNetRoles WHERE NormalizedName = 'MANAJEMEN')
    INSERT INTO dbo.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) VALUES (N'e02bb4ea-e2fb-4d0f-a367-27b0eb2023a8', N'Manajemen', N'MANAJEMEN', NEWID());
GO

-- =====================================================================================
-- 4. TABEL ASP.NET CORE IDENTITY: dbo.AspNetUsers (Pembuatan / Penambahan Kolom Kustom)
-- =====================================================================================
IF OBJECT_ID('dbo.AspNetUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUsers
    (
        Id                      NVARCHAR(450) NOT NULL,
        UserName                NVARCHAR(256) NULL,
        NormalizedUserName      NVARCHAR(256) NULL,
        Email                   NVARCHAR(256) NULL,
        NormalizedEmail         NVARCHAR(256) NULL,
        EmailConfirmed          BIT NOT NULL CONSTRAINT DF_AspNetUsers_EmailConfirmed DEFAULT ((0)),
        PasswordHash            NVARCHAR(MAX) NULL,
        SecurityStamp           NVARCHAR(MAX) NULL,
        ConcurrencyStamp        NVARCHAR(MAX) NULL,
        PhoneNumber             NVARCHAR(MAX) NULL,
        PhoneNumberConfirmed    BIT NOT NULL CONSTRAINT DF_AspNetUsers_PhoneNumberConfirmed DEFAULT ((0)),
        TwoFactorEnabled        BIT NOT NULL CONSTRAINT DF_AspNetUsers_TwoFactorEnabled DEFAULT ((0)),
        LockoutEnd              DATETIMEOFFSET(7) NULL,
        LockoutEnabled          BIT NOT NULL CONSTRAINT DF_AspNetUsers_LockoutEnabled DEFAULT ((1)),
        AccessFailedCount       INT NOT NULL CONSTRAINT DF_AspNetUsers_AccessFailedCount DEFAULT ((0)),
        CreatedAt               DATETIME2(7) NOT NULL CONSTRAINT DF_AspNetUsers_CreatedAt DEFAULT (GETDATE()),
        FullName                NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AspNetUsers_FullName DEFAULT (N''),
        IsActive                BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsActive DEFAULT ((1)),
        LastPeriodId            INT NULL,
        Theme                   NVARCHAR(MAX) NOT NULL CONSTRAINT DF_AspNetUsers_Theme DEFAULT (N'light'),
        CONSTRAINT PK_AspNetUsers PRIMARY KEY CLUSTERED (Id ASC)
    );
    CREATE NONCLUSTERED INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail ASC);
    CREATE UNIQUE NONCLUSTERED INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName ASC) WHERE [NormalizedUserName] IS NOT NULL;
    PRINT 'Tabel dbo.AspNetUsers baru berhasil dibuat.';
END
GO

-- Jika dbo.AspNetUsers sudah ada sebelumnya tetapi belum punya kolom kustom:
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

-- =====================================================================================
-- 5. SEED DATA AKUN DEFAULT KE dbo.AspNetUsers
-- =====================================================================================
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

-- =====================================================================================
-- 6. TABEL ASP.NET CORE IDENTITY: dbo.AspNetUserRoles
-- =====================================================================================
IF OBJECT_ID('dbo.AspNetUserRoles', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserRoles
    (
        UserId      NVARCHAR(450) NOT NULL,
        RoleId      NVARCHAR(450) NOT NULL,
        CONSTRAINT PK_AspNetUserRoles PRIMARY KEY CLUSTERED (UserId ASC, RoleId ASC),
        CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE,
        CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId ASC);
    PRINT 'Tabel dbo.AspNetUserRoles berhasil dibuat.';
END
GO

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

-- =====================================================================================
-- 7. TABEL PENDUKUNG ASP.NET CORE IDENTITY
-- =====================================================================================
-- dbo.AspNetRoleClaims
IF OBJECT_ID('dbo.AspNetRoleClaims', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetRoleClaims
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        RoleId      NVARCHAR(450) NOT NULL,
        ClaimType   NVARCHAR(MAX) NULL,
        ClaimValue  NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) REFERENCES dbo.AspNetRoles (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId ASC);
    PRINT 'Tabel dbo.AspNetRoleClaims berhasil dibuat.';
END
GO

-- dbo.AspNetUserClaims
IF OBJECT_ID('dbo.AspNetUserClaims', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserClaims
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        UserId      NVARCHAR(450) NOT NULL,
        ClaimType   NVARCHAR(MAX) NULL,
        ClaimValue  NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserClaims PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId ASC);
    PRINT 'Tabel dbo.AspNetUserClaims berhasil dibuat.';
END
GO

-- dbo.AspNetUserLogins
IF OBJECT_ID('dbo.AspNetUserLogins', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserLogins
    (
        LoginProvider       NVARCHAR(128) NOT NULL,
        ProviderKey         NVARCHAR(128) NOT NULL,
        ProviderDisplayName NVARCHAR(MAX) NULL,
        UserId              NVARCHAR(450) NOT NULL,
        CONSTRAINT PK_AspNetUserLogins PRIMARY KEY CLUSTERED (LoginProvider ASC, ProviderKey ASC),
        CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId ASC);
    PRINT 'Tabel dbo.AspNetUserLogins berhasil dibuat.';
END
GO

-- dbo.AspNetUserTokens
IF OBJECT_ID('dbo.AspNetUserTokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AspNetUserTokens
    (
        UserId          NVARCHAR(450) NOT NULL,
        LoginProvider   NVARCHAR(128) NOT NULL,
        Name            NVARCHAR(128) NOT NULL,
        Value           NVARCHAR(MAX) NULL,
        CONSTRAINT PK_AspNetUserTokens PRIMARY KEY CLUSTERED (UserId ASC, LoginProvider ASC, Name ASC),
        CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) REFERENCES dbo.AspNetUsers (Id) ON DELETE CASCADE
    );
    PRINT 'Tabel dbo.AspNetUserTokens berhasil dibuat.';
END
GO

-- =====================================================================================
-- 8. TABEL PENGGUNA & EMPLOYEE (DROPDOWN PIC & DIVISION PANAMON)
-- =====================================================================================
IF OBJECT_ID('dbo.Employee', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employee
    (
        Id              INT IDENTITY(1,1) NOT NULL,
        EmployeeName    NVARCHAR(MAX) NULL,
        Division        NVARCHAR(MAX) NULL,
        CONSTRAINT PK_Employee PRIMARY KEY CLUSTERED (Id ASC)
    );
    PRINT 'Tabel dbo.Employee berhasil dibuat.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.Employee)
BEGIN
    INSERT INTO dbo.Employee (EmployeeName, Division) VALUES (N'Lead Audio', N'Lead');
    INSERT INTO dbo.Employee (EmployeeName, Division) VALUES (N'PIC Final Assembly', N'PIC');
END
GO

IF OBJECT_ID('dbo.pengguna', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.pengguna
    (
        Id          INT IDENTITY(1,1) NOT NULL,
        Username    NVARCHAR(MAX) NULL,
        Password    NVARCHAR(MAX) NULL,
        CONSTRAINT PK_pengguna PRIMARY KEY CLUSTERED (Id ASC)
    );
    PRINT 'Tabel dbo.pengguna berhasil dibuat.';
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.pengguna)
BEGIN
    INSERT INTO dbo.pengguna (Username, Password) VALUES (N'Administrator', N'administrator');
    INSERT INTO dbo.pengguna (Username, Password) VALUES (N'Operator', N'operator');
END
GO

PRINT '=== SELURUH SKEMA TABEL USER & ROLES BERHASIL DIKONFIGURASI LENGKAP ===';
GO
