/*
====================================================================================================
PANAMON AUDIO - DATABASE RESTORE & ROLLBACK SCRIPT
====================================================================================================
Target Database : AUDIO_PRODUCTION
Keterangan      : Mengembalikan (rollback/restore) database AUDIO_PRODUCTION dari file .bak.
                  Memutuskan seluruh koneksi aktif (SINGLE_USER WITH ROLLBACK IMMEDIATE)
                  sebelum menimpa database (WITH REPLACE).
====================================================================================================
*/

USE [master];
GO

SET NOCOUNT ON;
GO

-- Ganti path file .bak ini sesuai file backup yang ingin di-restore
DECLARE @BackupFile NVARCHAR(500) = N'C:\Backup\Panamon_Audio\AUDIO_PRODUCTION_LATEST.bak';
DECLARE @DatabaseName NVARCHAR(128) = N'AUDIO_PRODUCTION';

PRINT 'Memutus semua koneksi aktif ke database ' + @DatabaseName + '...';

-- Putuskan koneksi aktif agar tidak terjadi blocking saat restore
ALTER DATABASE [AUDIO_PRODUCTION] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

PRINT 'Melakukan restore database ' + @DatabaseName + ' dari ' + @BackupFile + '...';

RESTORE DATABASE [AUDIO_PRODUCTION]
FROM DISK = @BackupFile
WITH 
    REPLACE,
    RECOVERY,
    STATS = 10;

-- Kembalikan akses ke multi user
ALTER DATABASE [AUDIO_PRODUCTION] SET MULTI_USER;

PRINT 'Restore dan rollback database AUDIO_PRODUCTION selesai dengan sukses.';
GO
