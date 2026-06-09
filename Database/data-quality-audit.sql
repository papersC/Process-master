/*
  data-quality-audit.sql  (READ-ONLY)
  -----------------------------------
  Surfaces the two data-quality issues found in the ESEMS catalog after the
  2026-06-02 bulk import. Safe to run on ANY environment (prod included) — it
  only SELECTs. Use it to confirm whether prod has the same problems before
  applying any remediation.

  Issue 1 — Duplicate ProcessGroups: the same group entered twice under one
            Category (identical NameAr), one copy malformed (NameEn = NameAr).
            Remediation is environment-specific (the GUIDs differ per DB): move
            the malformed twin's processes into the clean canonical group — via
            the app's re-parent so codes re-stamp correctly — then soft-delete
            the twin. See Database/fix-malformed-english-names.sql for the
            English cleanup, and the session notes for the dev merge that was run.

  Issue 2 — Malformed English: NameEn holds the Arabic string. Detect below;
            fix with Database/fix-malformed-english-names.sql (guarded/idempotent).
*/
SET NOCOUNT ON;

PRINT '======================================================================';
PRINT ' ISSUE 1: ProcessGroups sharing an identical Arabic name in one Category';
PRINT '======================================================================';
-- Each row = one (NameAr, Category) that has >1 live group. Copies>1 means a duplicate.
SELECT  c.Code                                   AS CategoryCode,
        c.NameEn                                 AS CategoryEn,
        COUNT(*)                                 AS Copies,
        COUNT(DISTINCT pg.Code)                  AS DistinctGroupCodes,
        SUM(CASE WHEN pg.NameEn = pg.NameAr THEN 1 ELSE 0 END) AS MalformedCopies
FROM        ProcessGroups pg
LEFT JOIN   Categories     c ON c.Id = pg.CategoryId
WHERE       pg.IsDeleted = 0
GROUP BY    pg.NameAr, pg.CategoryId, c.Code, c.NameEn
HAVING      COUNT(*) > 1
ORDER BY    Copies DESC, CategoryCode;

PRINT '';
PRINT '-- Drill-down: the actual rows behind any duplicates above --';
SELECT  pg.Code AS GroupCode, c.Code AS CategoryCode,
        pg.NameEn, pg.NameAr,
        CASE WHEN pg.NameEn = pg.NameAr THEN 'MALFORMED-TWIN' ELSE 'canonical' END AS Role,
        (SELECT COUNT(*) FROM Processes p WHERE p.ProcessGroupId = pg.Id AND p.IsDeleted = 0) AS LiveProcesses
FROM        ProcessGroups pg
LEFT JOIN   Categories     c ON c.Id = pg.CategoryId
WHERE       pg.IsDeleted = 0
AND         EXISTS (SELECT 1 FROM ProcessGroups d
                    WHERE d.IsDeleted = 0 AND d.Id <> pg.Id
                      AND d.NameAr = pg.NameAr AND d.CategoryId = pg.CategoryId)
ORDER BY    c.Code, pg.NameAr, pg.Code;

PRINT '';
PRINT '======================================================================';
PRINT ' ISSUE 2: Malformed English (NameEn = NameAr) — counts by entity';
PRINT '======================================================================';
SELECT 'Categories'    AS Entity, COUNT(*) AS Total, SUM(CASE WHEN NameEn = NameAr THEN 1 ELSE 0 END) AS Malformed FROM Categories    WHERE IsDeleted = 0
UNION ALL
SELECT 'ProcessGroups',          COUNT(*),           SUM(CASE WHEN NameEn = NameAr THEN 1 ELSE 0 END)            FROM ProcessGroups WHERE IsDeleted = 0
UNION ALL
SELECT 'Processes',              COUNT(*),           SUM(CASE WHEN NameEn = NameAr THEN 1 ELSE 0 END)            FROM Processes     WHERE IsDeleted = 0;

PRINT '';
PRINT '-- The malformed Process rows (Code + Arabic that needs an English name) --';
SELECT Code, NameAr FROM Processes WHERE IsDeleted = 0 AND NameEn = NameAr ORDER BY Code;
