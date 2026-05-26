-- ============================================================================
-- SeedInformationAssetCategories.sql
--
-- Backfills the 5 information-asset AssetCategory rows on an EXISTING
-- database that pre-dates the AddInformationAssetFields migration. Fresh
-- installs pick these up via SeedData.CreateAssetCategories(); this script
-- is only needed for tenants whose seed ran before the info-asset block was
-- added (e.g. ejraa360.com/App).
--
-- USAGE
--   1. Run as-is to preview which categories are missing (ROLLBACK by default).
--   2. Each INSERT is idempotent — guarded with NOT EXISTS on the Code key.
--   3. Flip the trailing ROLLBACK to COMMIT and re-run to persist.
--   4. Re-run with ROLLBACK to confirm AFTER snapshot shows all 5 rows.
-- ============================================================================

SET NOCOUNT ON;

BEGIN TRAN SeedInformationAssetCategories;

-- Snapshot what's already there
SELECT 'BEFORE — existing AST-INFO* categories' AS Stage,
       Code, NameEn, NameAr, ParentCategoryId, IsDeleted
FROM AssetCategories
WHERE Code LIKE N'AST-INFO%';

-- ───────────────────────────────────────────────────────────────────────────
-- 1. Information Asset root
-- ───────────────────────────────────────────────────────────────────────────
DECLARE @InfoId NVARCHAR(450);
SELECT @InfoId = Id FROM AssetCategories WHERE Code = N'AST-INFO' AND IsDeleted = 0;

IF @InfoId IS NULL
BEGIN
    SET @InfoId = LOWER(CONVERT(NVARCHAR(36), NEWID()));
    INSERT INTO AssetCategories
        (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr,
         DefaultDepreciationRate, DefaultUsefulLifeYears,
         IsDeleted, CreatedAt, UpdatedAt, Version)
    VALUES
        (@InfoId, N'AST-INFO', N'Information Asset', N'أصل معلوماتي',
         N'Data, documents, datasets and other information records under ISO 27001 / PDPL',
         N'البيانات والمستندات والسجلات المعلوماتية وفق ISO 27001 / قانون حماية البيانات',
         0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
END

-- ───────────────────────────────────────────────────────────────────────────
-- 2. Children: Database / Document set / Dataset / Application data
-- ───────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-INFO-DB')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-INFO-DB',
     N'Database', N'قاعدة بيانات',
     N'Structured database holding business records (SQL / NoSQL)',
     N'قاعدة بيانات منظمة تحتوي على سجلات الأعمال (SQL / NoSQL)',
     @InfoId, 0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-INFO-DOC')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-INFO-DOC',
     N'Document set', N'مجموعة مستندات',
     N'A managed collection of documents (policies, contracts, records)',
     N'مجموعة مستندات مُدارة (السياسات والعقود والسجلات)',
     @InfoId, 0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-INFO-DATA')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-INFO-DATA',
     N'Dataset', N'مجموعة بيانات',
     N'Analytical dataset (CSV / parquet / JSONL) for reporting or ML',
     N'مجموعة بيانات تحليلية للتقارير أو الذكاء الاصطناعي',
     @InfoId, 0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-INFO-APP')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-INFO-APP',
     N'Application data', N'بيانات تطبيقية',
     N'Data owned by a specific application (logs, config, audit trails)',
     N'بيانات يملكها تطبيق محدد (السجلات والإعدادات ومسارات التدقيق)',
     @InfoId, 0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

-- ───────────────────────────────────────────────────────────────────────────
-- AFTER snapshot — should show 5 rows (AST-INFO + 4 children)
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'AFTER — AST-INFO* categories' AS Stage,
       Code, NameEn, NameAr, ParentCategoryId, DefaultUsefulLifeYears, IsDeleted
FROM AssetCategories
WHERE Code LIKE N'AST-INFO%'
ORDER BY Code;

-- Default: roll back so you can inspect. Flip to COMMIT and re-run to persist.
ROLLBACK TRAN SeedInformationAssetCategories;
-- COMMIT TRAN SeedInformationAssetCategories;
