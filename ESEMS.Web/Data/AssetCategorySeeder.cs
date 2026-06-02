using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Data;

/// <summary>
/// Seeds the canonical AssetCategory tree (reusing <see cref="SeedData.CreateAssetCategories"/>).
/// Extracted from the now-disabled demo seeder because the MBRHE asset-register
/// import (<c>ImportMbrheAssetRegisterAsync</c>) hard-fails without the
/// <c>AST-RE-PROJ</c> and <c>AST-INFO-APP</c> categories. These are reference
/// lookups, not demo data, so they must survive the seeder removal.
///
/// All-or-nothing idempotency: seeds the full tree only when the table is empty,
/// so re-runs (and DBs already carrying asset categories) are left untouched and
/// no dangling parent FKs can be created.
/// </summary>
public static class AssetCategorySeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (await context.AssetCategories.AnyAsync()) return;

        await context.AssetCategories.AddRangeAsync(SeedData.CreateAssetCategories());
        await context.SaveChangesAsync();
    }
}
