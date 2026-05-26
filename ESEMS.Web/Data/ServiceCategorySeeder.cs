using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Data;

/// <summary>
/// Seeds the ServiceCategories lookup. Idempotent on every startup — skips
/// rows that already exist by Code.
///
/// Note: an earlier revision of this seeder also backfilled rows from the
/// legacy Service.CategoryEn/CategoryAr free-text columns and linked
/// pre-existing services to the new FK. Those columns were dropped in
/// 20260519_DropLegacyServiceCategoryColumns, so the backfill/linkage steps
/// are gone — they were one-time data movements that already ran.
/// </summary>
public static class ServiceCategorySeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await SeedDefaultsAsync(context);
    }

    /// <summary>Starter values — MBRHE housing-authority service taxonomy.</summary>
    private static async Task SeedDefaultsAsync(ApplicationDbContext context)
    {
        var defaults = new (string Code, string En, string Ar)[]
        {
            ("GRANT",  "Grant Services",         "خدمات المنح"),
            ("LOAN",   "Loan Services",          "خدمات القروض"),
            ("HOUS",   "Housing Allocation",     "تخصيص المساكن"),
            ("MAINT",  "Maintenance Services",   "خدمات الصيانة"),
            ("CC",     "Customer Care",          "خدمة المتعاملين"),
            ("LAND",   "Land Services",          "خدمات الأراضي"),
            ("FIN",    "Financial Services",     "الخدمات المالية"),
            ("ADV",    "Advisory Services",      "الخدمات الاستشارية"),
            ("OTH",    "Other",                  "أخرى"),
        };

        var existingCodes = await context.ServiceCategories
            .Select(c => c.Code)
            .ToListAsync();

        var order = 0;
        foreach (var (code, en, ar) in defaults)
        {
            order++;
            if (existingCodes.Contains(code)) continue;
            context.ServiceCategories.Add(new ServiceCategory
            {
                Id = Guid.NewGuid().ToString(),
                Code = code,
                NameEn = en,
                NameAr = ar,
                DisplayOrder = order,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await context.SaveChangesAsync();
    }
}
