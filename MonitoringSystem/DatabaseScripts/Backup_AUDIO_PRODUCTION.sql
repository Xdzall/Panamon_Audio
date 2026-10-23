/*
====================================================================================================
PANAMON AUDIO - DATABASE BACKUP SCRIPT
====================================================================================================
Target Database : AUDIO_PRODUCTION
Keterangan      : Melakukan full backup database AUDIO_PRODUCTION dengan penamaan otomatis
                  berdasarkan timestamp (YYYYMMDD_HHMMSS).
====================================================================================================
*/

USE [master];
GO

SET NOCOUNT ON;
GO

DECLARE @DatabaseName NVARCHAR(128) = N'AUDIO_PRODUCTION';
DECLARE @BackupDir NVARCHAR(500) = N'C:\Backup\Panamon_Audio\';
DECLARE @BackupFileName NVARCHAR(500);
DECLARE @DateTimeStr NVARCHAR(20);

-- Format timestamp YYYYMMDD_HHMMSS
SET @DateTimeStr = REPLACE(REPLACE(REPLACE(CONVERT(NVARCHAR(19), GETDATE(), 120), '-', ''), ' ', '_'), ':', '');

-- Pastikan folder backup ada
EXEC xp_create_subdir @BackupDir;

SET @BackupFileName = @BackupDir + @DatabaseName + N'_' + @DateTimeStr + N'.bak';

PRINT 'Memulai backup database ' + @DatabaseName + ' ke file: ' + @BackupFileName;

BACKUP DATABASE @DatabaseName
TO DISK = @BackupFileName
WITH 
    FORMAT,
    INIT,
    NAME = N'AUDIO_PRODUCTION-Full Backup',
    SKIP,
    NOREWIND,
    NOUNLOAD,
    STATS = 10;

PRINT 'Backup database AUDIO_PRODUCTION berhasil disimpan ke: ' + @BackupFileName;
GO
