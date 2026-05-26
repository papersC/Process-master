-- ============================================================================
-- CleanupJunkData.sql
--
-- One-time soft-delete script for the test tenant. Wipes records that match
-- the obvious garbage patterns surfaced by the buyer review:
--
--   "1", "111", "qqq", "qqqqq", "2333", "11111",
--   "3333333333333333333333333333333", "Wizard Test", "UX Test Process",
--   "Doc Metadata Test", WS-015 (workload scenario at +174 FTE gap), etc.
--
-- Operates on:
--   - Processes
--   - ImprovementInitiatives
--   - WorkloadScenarios
--
-- The new ≥3-char rule from FIX-1 blocks fresh garbage; this script clears
-- the historical junk so the tenant looks like one the buyer would actually
-- accept on a 2-week pilot.
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
-- default Index queries (which filter `!IsDeleted`).
-- ============================================================================

SET NOCOUNT ON;

BEGIN TRAN CleanupJunkData;

-- ----------------------------------------------------------------------------
-- 1. PROCESSES — junk titles, all-digit names, single-char repeats, test rows
-- ----------------------------------------------------------------------------
DECLARE @ProcessJunkPattern TABLE (Pattern NVARCHAR(200));
INSERT INTO @ProcessJunkPattern (Pattern) VALUES
    (N'[0-9]'),                          -- single digit
    (N'[0-9][0-9]'),                     -- two digits
    (N'[a-zA-Z]'),                       -- single letter
    (N'[a-zA-Z][a-zA-Z]');               -- two letters

-- Preview
SELECT 'Processes — junk-by-name' AS Category, Id, Code, NameEn, NameAr, IsDeleted
FROM Processes
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(NameEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(NameAr, '')))) < 3
     OR NameEn LIKE '%test%'
     OR NameAr LIKE '%test%'
     OR NameEn LIKE 'Wizard %' OR NameEn LIKE '%Doc Metadata%'
     -- All-digit names of any length
     OR (NameEn NOT LIKE '%[^0-9 ]%' AND LEN(NameEn) > 0)
     OR (NameAr NOT LIKE '%[^0-9 ]%' AND LEN(NameAr) > 0)
     -- Single character repeated (qqq, 11111, etc.)
     OR (NameEn IS NOT NULL AND LEN(NameEn) >= 3 AND REPLACE(NameEn, LEFT(NameEn, 1), '') = '')
     OR (NameAr IS NOT NULL AND LEN(NameAr) >= 3 AND REPLACE(NameAr, LEFT(NameAr, 1), '') = '')
  );

UPDATE Processes
SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(NameEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(NameAr, '')))) < 3
     OR NameEn LIKE '%test%'
     OR NameAr LIKE '%test%'
     OR NameEn LIKE 'Wizard %' OR NameEn LIKE '%Doc Metadata%'
     OR (NameEn NOT LIKE '%[^0-9 ]%' AND LEN(NameEn) > 0)
     OR (NameAr NOT LIKE '%[^0-9 ]%' AND LEN(NameAr) > 0)
     OR (NameEn IS NOT NULL AND LEN(NameEn) >= 3 AND REPLACE(NameEn, LEFT(NameEn, 1), '') = '')
     OR (NameAr IS NOT NULL AND LEN(NameAr) >= 3 AND REPLACE(NameAr, LEFT(NameAr, 1), '') = '')
  );

-- ----------------------------------------------------------------------------
-- 2. IMPROVEMENT INITIATIVES — junk titles, all-digit, single-char repeats
-- ----------------------------------------------------------------------------
SELECT 'ImprovementInitiatives — junk-by-title' AS Category, Id, Code, TitleEn, TitleAr, IsDeleted
FROM ImprovementInitiatives
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(TitleEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(TitleAr, '')))) < 3
     OR TitleEn LIKE '%test%'
     OR TitleAr LIKE '%test%'
     OR (TitleEn NOT LIKE '%[^0-9 ]%' AND LEN(TitleEn) > 0)
     OR (TitleAr NOT LIKE '%[^0-9 ]%' AND LEN(TitleAr) > 0)
     OR (TitleEn IS NOT NULL AND LEN(TitleEn) >= 3 AND REPLACE(TitleEn, LEFT(TitleEn, 1), '') = '')
     OR (TitleAr IS NOT NULL AND LEN(TitleAr) >= 3 AND REPLACE(TitleAr, LEFT(TitleAr, 1), '') = '')
  );

UPDATE ImprovementInitiatives
SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        LEN(LTRIM(RTRIM(ISNULL(TitleEn, '')))) < 3
     OR LEN(LTRIM(RTRIM(ISNULL(TitleAr, '')))) < 3
     OR TitleEn LIKE '%test%'
     OR TitleAr LIKE '%test%'
     OR (TitleEn NOT LIKE '%[^0-9 ]%' AND LEN(TitleEn) > 0)
     OR (TitleAr NOT LIKE '%[^0-9 ]%' AND LEN(TitleAr) > 0)
     OR (TitleEn IS NOT NULL AND LEN(TitleEn) >= 3 AND REPLACE(TitleEn, LEFT(TitleEn, 1), '') = '')
     OR (TitleAr IS NOT NULL AND LEN(TitleAr) >= 3 AND REPLACE(TitleAr, LEFT(TitleAr, 1), '') = '')
  );

-- ----------------------------------------------------------------------------
-- 3. WORKLOAD SCENARIOS — junk-by-name (WS-015 et al)
-- ----------------------------------------------------------------------------
SELECT 'WorkloadScenarios — junk-by-name' AS Category, Id, Code, NameEn, NameAr, IsDeleted
FROM WorkloadScenarios
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
  );

UPDATE WorkloadScenarios
SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
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
  );

-- ----------------------------------------------------------------------------
-- Tally — review before COMMIT
-- ----------------------------------------------------------------------------
SELECT 'Processes — soft-deleted total' AS Metric, COUNT(*) AS N
FROM Processes WHERE IsDeleted = 1
UNION ALL
SELECT 'ImprovementInitiatives — soft-deleted total', COUNT(*)
FROM ImprovementInitiatives WHERE IsDeleted = 1
UNION ALL
SELECT 'WorkloadScenarios — soft-deleted total', COUNT(*)
FROM WorkloadScenarios WHERE IsDeleted = 1;

-- Default: roll back so you can re-run the SELECTs and inspect.
-- When the row sets above look right, change ROLLBACK to COMMIT and re-run.
ROLLBACK TRAN CleanupJunkData;
-- COMMIT TRAN CleanupJunkData;
