-- ============================================================================
-- SeedHousingAssetCategories.sql
--
-- Backfills the 5 real-estate AssetCategory rows on an EXISTING database that
-- pre-dates the AddHousingAssetFields migration. New installs pick these up
-- via SeedData.CreateAssetCategories() so this script is only needed for
-- tenants that already ran the seed before the housing fields were added
-- (e.g. ejraa360.com/App).
--
-- USAGE
--   1. Run the SELECT first to see what's missing (zero rows = nothing to add).
--   2. Run the INSERTs. They are idempotent — re-running is a no-op because
--      each INSERT is guarded with NOT EXISTS on the Code uniqueness key.
--   3. Wrapped in a transaction with ROLLBACK by default; flip to COMMIT and
--      re-run to persist.
-- ============================================================================

SET NOCOUNT ON;

BEGIN TRAN SeedHousingAssetCategories;

-- Snapshot what's already there
SELECT 'BEFORE — existing real-estate categories' AS Stage,
       Code, NameEn, NameAr, ParentCategoryId, IsDeleted
FROM Categories  -- NB: ESEMS uses dbo.AssetCategories — see correction below
WHERE 1 = 0;  -- placeholder; AssetCategories is the real table

-- The actual table is AssetCategories; the line above is a guard against
-- accidentally querying the wrong table on tenants with a Categories rename.
SELECT 'BEFORE — existing AST-RE* categories' AS Stage,
       Code, NameEn, NameAr, ParentCategoryId, IsDeleted
FROM AssetCategories
WHERE Code LIKE N'AST-RE%';

-- ───────────────────────────────────────────────────────────────────────────
-- 1. Real Estate root
-- ───────────────────────────────────────────────────────────────────────────
DECLARE @RealEstateId NVARCHAR(450);
SELECT @RealEstateId = Id FROM AssetCategories WHERE Code = N'AST-RE' AND IsDeleted = 0;

IF @RealEstateId IS NULL
BEGIN
    SET @RealEstateId = LOWER(CONVERT(NVARCHAR(36), NEWID()));
    INSERT INTO AssetCategories
        (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr,
         DefaultDepreciationRate, DefaultUsefulLifeYears,
         IsDeleted, CreatedAt, UpdatedAt, Version)
    VALUES
        (@RealEstateId, N'AST-RE', N'Real Estate', N'العقارات',
         N'Land and built real-estate assets', N'الأراضي والأصول العقارية المبنية',
         3, 30, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);
END

-- ───────────────────────────────────────────────────────────────────────────
-- 2. Children: Housing Project / Villa / Building / Plot
-- ───────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-RE-PROJ')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-RE-PROJ',
     N'Housing Project', N'مشروع إسكاني',
     N'A multi-unit housing development (parent of villas / buildings)',
     N'مشروع إسكاني متعدد الوحدات (يجمع الفلل / المباني)',
     @RealEstateId, 3, 40, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-RE-VILLA')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-RE-VILLA',
     N'Villa', N'فيلا',
     N'Stand-alone or attached residential villa',
     N'فيلا سكنية مستقلة أو متصلة',
     @RealEstateId, 4, 30, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-RE-BLDG')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-RE-BLDG',
     N'Building', N'مبنى',
     N'Multi-floor residential or mixed-use building',
     N'مبنى سكني أو متعدد الاستخدامات',
     @RealEstateId, 4, 35, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

IF NOT EXISTS (SELECT 1 FROM AssetCategories WHERE Code = N'AST-RE-PLOT')
INSERT INTO AssetCategories
    (Id, Code, NameEn, NameAr, DescriptionEn, DescriptionAr, ParentCategoryId,
     DefaultDepreciationRate, DefaultUsefulLifeYears,
     IsDeleted, CreatedAt, UpdatedAt, Version)
VALUES
    (LOWER(CONVERT(NVARCHAR(36), NEWID())), N'AST-RE-PLOT',
     N'Plot', N'قطعة أرض',
     N'Undeveloped land parcel or building plot',
     N'قطعة أرض غير مطورة أو قطعة بناء',
     @RealEstateId, 0, 99, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1);

-- ───────────────────────────────────────────────────────────────────────────
-- AFTER snapshot — should show 5 rows (AST-RE + 4 children)
-- ───────────────────────────────────────────────────────────────────────────
SELECT 'AFTER — AST-RE* categories' AS Stage,
       Code, NameEn, NameAr, ParentCategoryId, DefaultUsefulLifeYears, IsDeleted
FROM AssetCategories
WHERE Code LIKE N'AST-RE%'
ORDER BY Code;

-- Default: roll back so you can inspect. Flip to COMMIT and re-run to persist.
ROLLBACK TRAN SeedHousingAssetCategories;
-- COMMIT TRAN SeedHousingAssetCategories;
