-- ============================================================================
-- CleanupServiceJunkData.sql
--
-- One-time soft-delete for junk Service rows polluting the Linked Services
-- dropdown across Process Create / Edit and elsewhere. The retest surfaced
-- entries like "11111", "s1", "Test Service", and effectively-empty rows.
--
-- Operates on:
--   - Services
--   - ProcessServices (cascades: orphan join rows from the deleted services)
--
-- The ≥3-char rule on BilingualEntity blocks fresh garbage at the form layer;
-- this script clears the historical junk so the picker stops showing it.
--
-- USAGE
--   1. Connect to the ESEMS database under a backup-validated session.
--   2. Read the SELECTs first — they show exactly which rows the UPDATEs
--      will mark IsDeleted. Sanity-check the names before continuing.
--   3. The script wraps everything in a transaction that ROLLS BACK by
--      default. To actually persist the cleanup, change the final line
--      from `ROLLBACK TRAN` to `COMMIT TRAN`.
--   4. Re-run after committing to confirm the SELECTs return zero rows.
--
-- All edits use UPDATE … IsDeleted = 1 (soft-delete) — nothing is hard-
-- deleted. The audit trail is preserved; rows just stop appearing in the
-- default Index queries (which filter `!IsDeleted`) and in the wizard
-- dropdown (which now filters IsActive=1 AND IsDeleted=0 after this script).
-- ============================================================================

SET NOCOUNT ON;

BEGIN TRAN CleanupServiceJunkData;

-- ----------------------------------------------------------------------------
-- 1. PREVIEW — Services that match junk patterns
-- ----------------------------------------------------------------------------
-- Conditions, OR'd together:
--   • NameEn or NameAr trimmed to fewer than 3 chars
--   • NameEn or NameAr contains "test" (case-insensitive default collation)
--   • All-digit name of any length (e.g. "11111", "999", "2024")
--   • Single character repeated (e.g. "ssss", "1111", "qqq")
--   • Common throwaway codes the buyer flagged
SELECT 'Services — junk-by-name' AS Category,
       Id, Code, NameEn, NameAr, IsActive, IsDeleted, CreatedAt
FROM Services
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(NameEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(NameAr, '')))) < 3
     OR NameEn LIKE '%test%'
     OR NameAr LIKE '%test%'
     -- All-digit names of any length
     OR (NameEn NOT LIKE '%[^0-9 ]%' AND LEN(NameEn) > 0)
     OR (NameAr NOT LIKE '%[^0-9 ]%' AND LEN(NameAr) > 0)
     -- Single-character repeats (covers "ssss", "1111", "qqq")
     OR (NameEn IS NOT NULL AND LEN(NameEn) >= 3 AND REPLACE(NameEn, LEFT(NameEn, 1), '') = '')
     OR (NameAr IS NOT NULL AND LEN(NameAr) >= 3 AND REPLACE(NameAr, LEFT(NameAr, 1), '') = '')
     -- Specific codes the retest flagged
     OR Code IN (N'st01', N'req01', N's1')
  )
ORDER BY CreatedAt DESC;

-- ----------------------------------------------------------------------------
-- 2. PREVIEW — ProcessService join rows that point at junk services
-- ----------------------------------------------------------------------------
-- These M:N rows become orphans once the parent Service is soft-deleted.
-- We deactivate them in the same pass so the Service Details page on the
-- Process side stops claiming a link to a now-hidden service.
SELECT 'ProcessServices — orphans-from-junk' AS Category,
       ps.Id, ps.ProcessId, ps.ServiceId, ps.IsActive, ps.Criticality, ps.IsMandatory
FROM ProcessServices ps
INNER JOIN Services s ON s.Id = ps.ServiceId
WHERE ps.IsActive = 1
  AND s.IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(s.NameEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(s.NameAr, '')))) < 3
     OR s.NameEn LIKE '%test%'
     OR s.NameAr LIKE '%test%'
     OR (s.NameEn NOT LIKE '%[^0-9 ]%' AND LEN(s.NameEn) > 0)
     OR (s.NameAr NOT LIKE '%[^0-9 ]%' AND LEN(s.NameAr) > 0)
     OR (s.NameEn IS NOT NULL AND LEN(s.NameEn) >= 3 AND REPLACE(s.NameEn, LEFT(s.NameEn, 1), '') = '')
     OR (s.NameAr IS NOT NULL AND LEN(s.NameAr) >= 3 AND REPLACE(s.NameAr, LEFT(s.NameAr, 1), '') = '')
     OR s.Code IN (N'st01', N'req01', N's1')
  );

-- ----------------------------------------------------------------------------
-- 3. SOFT-DELETE the junk services
-- ----------------------------------------------------------------------------
UPDATE Services
SET IsDeleted = 1,
    IsActive  = 0,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(NameEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(NameAr, '')))) < 3
     OR NameEn LIKE '%test%'
     OR NameAr LIKE '%test%'
     OR (NameEn NOT LIKE '%[^0-9 ]%' AND LEN(NameEn) > 0)
     OR (NameAr NOT LIKE '%[^0-9 ]%' AND LEN(NameAr) > 0)
     OR (NameEn IS NOT NULL AND LEN(NameEn) >= 3 AND REPLACE(NameEn, LEFT(NameEn, 1), '') = '')
     OR (NameAr IS NOT NULL AND LEN(NameAr) >= 3 AND REPLACE(NameAr, LEFT(NameAr, 1), '') = '')
     OR Code IN (N'st01', N'req01', N's1')
  );

-- ----------------------------------------------------------------------------
-- 4. DEACTIVATE the orphaned ProcessService M:N rows
-- ----------------------------------------------------------------------------
-- ProcessService doesn't carry IsDeleted (audit-friendly soft delete uses
-- IsActive instead). Set IsActive=0 so the rows don't surface in the
-- "linked services" rollup on Process Details.
UPDATE ps
SET ps.IsActive = 0,
    ps.UpdatedAt = SYSUTCDATETIME()
FROM ProcessServices ps
INNER JOIN Services s ON s.Id = ps.ServiceId
WHERE ps.IsActive = 1
  AND s.IsDeleted = 1;   -- by this point the previous UPDATE has flipped these

-- ----------------------------------------------------------------------------
-- Tally — review before COMMIT
-- ----------------------------------------------------------------------------
SELECT 'Services — soft-deleted total' AS Metric, COUNT(*) AS N
FROM Services WHERE IsDeleted = 1
UNION ALL
SELECT 'Services — still active',          COUNT(*)
FROM Services WHERE IsDeleted = 0 AND IsActive = 1
UNION ALL
SELECT 'ProcessServices — deactivated',    COUNT(*)
FROM ProcessServices WHERE IsActive = 0
UNION ALL
SELECT 'ProcessServices — still active',   COUNT(*)
FROM ProcessServices WHERE IsActive = 1;

-- Default: roll back so you can re-run the SELECTs and inspect.
-- When the row sets above look right, change ROLLBACK to COMMIT and re-run.
ROLLBACK TRAN CleanupServiceJunkData;
-- COMMIT TRAN CleanupServiceJunkData;
