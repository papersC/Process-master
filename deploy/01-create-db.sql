-- =============================================================
-- ejraa360 — database creation
-- Run on the EC2 SQL Server as `sa` (or any sysadmin).
-- The app's startup code (Database.Migrate + SeedData.SeedAsync)
-- builds the schema on first boot, so we just need an empty DB.
-- =============================================================

IF DB_ID('ejraa360') IS NULL
BEGIN
    CREATE DATABASE [ejraa360];
    PRINT 'Database [ejraa360] created.';
END
ELSE
BEGIN
    PRINT 'Database [ejraa360] already exists — skipped.';
END
GO

-- Sanity check: confirm the legacy shared `sa` login can reach it.
USE [ejraa360];
SELECT
    DB_NAME()              AS CurrentDb,
    SUSER_SNAME()          AS LoginName,
    IS_SRVROLEMEMBER('sysadmin') AS IsSysAdmin;
GO
