-- ============================================================================
-- CleanupJunkData_Extended.sql
--
-- Extends CleanupJunkData.sql + CleanupServiceJunkData.sql (commit 5331c54)
-- to cover the remaining junk surfaced in the 2026-05-16 QA pass.
--
-- Targets:
--   - Process names left over from manual test runs (budget, Retest Link Process)
--   - Auto-generated Processes / ProcessGroups from the Visio importer
--   - "Auto-Imported from Visio" Category placeholder
--   - ChangeRequests with the auto-code stub (CR-001 "CR Autocode 1776501038")
--
-- USAGE
--   1. The script runs in a transaction and ROLLS BACK by default.
--   2. Run it once, inspect the preview SELECTs and the summary tally.
--   3. Flip the trailing ROLLBACK to COMMIT and re-run to persist.
--   4. Re-run once more (with ROLLBACK) to confirm the preview SELECTs return
--      zero rows — proving every targeted record is now soft-deleted.
-- ============================================================================

SET NOCOUNT ON;

BEGIN TRAN CleanupJunkData_Extended;

-- ───────────────────────────────────────────────────────────────────────────
-- 1. Process names (manual test residue) + auto-generated Visio processes
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'Processes — manual test residue + AUTO-* codes' AS Category,
       Id, Code, NameEn, NameAr, IsDeleted
FROM Processes
WHERE IsDeleted = 0
  AND (
        NameEn IN (N'budget', N'Retest Link Process')
     OR NameAr IN (N'budget', N'Retest Link Process')
     OR Code LIKE N'AUTO-%'
      );

UPDATE Processes
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        NameEn IN (N'budget', N'Retest Link Process')
     OR NameAr IN (N'budget', N'Retest Link Process')
     OR Code LIKE N'AUTO-%'
      );

-- ───────────────────────────────────────────────────────────────────────────
-- 2. ChangeRequests — CR-001 with auto-code title
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'ChangeRequests — CR-001 auto-code stub' AS Category,
       Id, Code, Title, Status, CreatedAt, IsDeleted
FROM ChangeRequests
WHERE IsDeleted = 0
  AND (
        Code = N'CR-001'
     OR Title LIKE N'CR Autocode%'
      );

UPDATE ChangeRequests
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        Code = N'CR-001'
     OR Title LIKE N'CR Autocode%'
      );

-- ───────────────────────────────────────────────────────────────────────────
-- 3. Categories — "Auto-Imported from Visio" placeholder
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'Categories — Auto-Imported from Visio' AS Category,
       Id, Code, NameEn, NameAr, IsDeleted
FROM Categories
WHERE IsDeleted = 0
  AND NameEn = N'Auto-Imported from Visio';

UPDATE Categories
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND NameEn = N'Auto-Imported from Visio';

-- ───────────────────────────────────────────────────────────────────────────
-- 4. ProcessGroups — Visio import remnants
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'ProcessGroups — Auto-Imported / AUTO-* code' AS Category,
       Id, Code, NameEn, NameAr, IsDeleted
FROM ProcessGroups
WHERE IsDeleted = 0
  AND (
        NameEn = N'Auto-Imported from Visio'
     OR Code LIKE N'AUTO-%'
      );

UPDATE ProcessGroups
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        NameEn = N'Auto-Imported from Visio'
     OR Code LIKE N'AUTO-%'
      );

-- ───────────────────────────────────────────────────────────────────────────
-- 5. QA test residue (2026-05-16 Chrome-extension run)
--    The Chrome extension's automated tester left behind scenarios + assets
--    that survived the cascade-delete / soft-cap guards because they were
--    created BEFORE the guards shipped. Sweep them now.
-- ───────────────────────────────────────────────────────────────────────────
-- Workload soft-cap probes (FTE 99,999 etc.)
SELECT 'WorkloadScenarios — cap probes' AS Category,
       Id, Code, NameEn, IsDeleted
FROM WorkloadScenarios
WHERE IsDeleted = 0
  AND (
        NameEn LIKE N'%cap%probe%' OR NameEn LIKE N'%99999%'
     OR NameAr LIKE N'%99999%' OR NameAr LIKE N'%سقف%'
      );

UPDATE WorkloadScenarios
SET IsDeleted = 1, DeletedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        NameEn LIKE N'%cap%probe%' OR NameEn LIKE N'%99999%'
     OR NameAr LIKE N'%99999%' OR NameAr LIKE N'%سقف%'
      );

-- Chrome-extension test assets (Villa Ahmed / Beneficiary Master DB)
SELECT 'Assets — Chrome-ext test residue' AS Category,
       Id, AssetTag, NameEn, IsDeleted
FROM Assets
WHERE IsDeleted = 0
  AND (
        NameEn LIKE N'%Ahmed Al Maktoum%'
     OR NameEn LIKE N'%Beneficiary Master DB%'
     OR NameEn LIKE N'%FTE Cap Probe%'
      );

UPDATE Assets
SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME()
WHERE IsDeleted = 0
  AND (
        NameEn LIKE N'%Ahmed Al Maktoum%'
     OR NameEn LIKE N'%Beneficiary Master DB%'
     OR NameEn LIKE N'%FTE Cap Probe%'
      );

-- ───────────────────────────────────────────────────────────────────────────
-- 6. Notifications — collapse legacy duplicates of the stall-digest alerts
--    that piled up before NotificationService.SendAsync grew its DedupKey
--    guard. Keep the most recent unread copy per (UserId, TitleEn, EntityType);
--    mark older unread copies as read so the bell drops back to one per band.
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'Notifications — unread duplicates' AS Category,
       UserId, TitleEn, RelatedEntityType, COUNT(*) AS DupCount,
       MAX(CreatedAt) AS LatestAt
FROM Notifications
WHERE IsRead = 0
  AND RelatedEntityType IS NOT NULL
GROUP BY UserId, TitleEn, RelatedEntityType
HAVING COUNT(*) > 1;

-- Soft-collapse, not hard-delete. Audit trail of past notifications stays
-- intact; the bell just stops showing them.
;WITH ranked AS (
    SELECT Id,
           ROW_NUMBER() OVER (
               PARTITION BY UserId, TitleEn, RelatedEntityType
               ORDER BY CreatedAt DESC
           ) AS rn
    FROM Notifications
    WHERE IsRead = 0
      AND RelatedEntityType IS NOT NULL
)
UPDATE n
SET IsRead = 1, ReadAt = SYSUTCDATETIME()
FROM Notifications n
INNER JOIN ranked r ON n.Id = r.Id
WHERE r.rn > 1;

-- Also collapse the pre-DedupKey legacy entries: now that SendAsync stamps
-- a DedupKey on every stall sweep, the canonical post-fix entry is the one
-- with a non-null key. Anything older (DedupKey IS NULL) is a one-time
-- artifact that should drop off the bell.
UPDATE Notifications
SET IsRead = 1, ReadAt = SYSUTCDATETIME()
WHERE IsRead = 0
  AND DedupKey IS NULL
  AND RelatedEntityType = N'Improvement'
  AND (TitleEn LIKE N'%initiatives idle%' OR TitleEn LIKE N'%Critical stall%');

-- ───────────────────────────────────────────────────────────────────────────
-- Summary tally — counts AFTER this script + the earlier two cleanup scripts
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'Processes — soft-deleted total'             AS Metric, COUNT(*) AS N FROM Processes             WHERE IsDeleted = 1
UNION ALL SELECT 'ChangeRequests — soft-deleted total',          COUNT(*)        FROM ChangeRequests        WHERE IsDeleted = 1
UNION ALL SELECT 'Categories — soft-deleted total',              COUNT(*)        FROM Categories            WHERE IsDeleted = 1
UNION ALL SELECT 'ProcessGroups — soft-deleted total',           COUNT(*)        FROM ProcessGroups         WHERE IsDeleted = 1
UNION ALL SELECT 'Services — soft-deleted (from prior script)',  COUNT(*)        FROM Services              WHERE IsDeleted = 1
UNION ALL SELECT 'ImprovementInitiatives — soft-deleted total',  COUNT(*)        FROM ImprovementInitiatives WHERE IsDeleted = 1
UNION ALL SELECT 'WorkloadScenarios — soft-deleted total',       COUNT(*)        FROM WorkloadScenarios     WHERE IsDeleted = 1;

-- Default: ROLLBACK. Flip to COMMIT after inspecting the preview SELECTs.
ROLLBACK TRAN CleanupJunkData_Extended;
-- COMMIT TRAN CleanupJunkData_Extended;
