using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Models.DocumentManagement;

namespace ESEMS.Web.Data;

/// <summary>
/// Seeds the DocumentCategories and DocumentTypes lookup tables.
/// Values are sourced from the Process Catalog reference sheet
/// (full.xlsx → Sheet17 → "فئة الوثيقة" and "نوع الوثيقة").
/// Idempotent — skips entries that already exist by Code.
/// </summary>
public static class DocumentLookupSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        await SeedCategoriesAsync(context);
        await SeedTypesAsync(context);
    }

    private static async Task SeedCategoriesAsync(ApplicationDbContext context)
    {
        var items = new (string Code, string En, string Ar)[]
        {
            ("IS",  "Information Security", "أمن المعلومات"),
            ("BC",  "Business Continuity",  "استمرارية الأعمال"),
            ("QM",  "Quality Management",   "إدارة الجودة"),
            ("HS",  "Health and Safety",    "الصحة والسلامة"),
            ("RM",  "Risk Management",      "إدارة المخاطر"),
            ("CG",  "Corporate Governance", "الحوكمة المؤسسية"),
            ("INN", "Innovation",           "الابتكار"),
            ("ENV", "Environment",          "البيئة"),
            ("SR",  "Social Responsibility","المسؤولية المجتمعية"),
        };

        var existing = await context.DocumentCategories
            .Select(c => c.Code)
            .ToListAsync();
        var order = 0;
        foreach (var (code, en, ar) in items)
        {
            order++;
            if (existing.Contains(code)) continue;
            context.DocumentCategories.Add(new DocumentCategory
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

    private static async Task SeedTypesAsync(ApplicationDbContext context)
    {
        var items = new (string Code, string En, string Ar)[]
        {
            ("POL",  "Policy",             "سياسة"),
            ("PROC", "Procedure",          "إجراء"),
            ("STD",  "Standard",           "معيار"),
            ("GUID", "Guideline",          "مبادئ توجيهية"),
            ("MAN",  "Manual",             "دليل"),
            ("PLAN", "Plan",               "خطة"),
            ("LIST", "List",               "قائمة"),
            ("FORM", "Form",               "استمارة"),
            ("TMPL", "Template",           "نموذج"),
            ("RPT",  "Report",             "تقرير"),
            ("METH", "Methodology",        "منهجية"),
            ("PRES", "Presentation",       "عرض"),
            ("CHRT", "Charter",            "ميثاق"),
            ("MIN",  "Meeting Minutes",    "محضر اجتماع"),
            ("FILE", "File",               "ملف"),
            ("STMT", "Statement",          "بيان/تصريح"),
            ("MOU",  "Memorandum of Understanding", "اتفاقية تفاهم"),
            ("RES",  "Research",           "بحث"),
            ("LTR",  "Letter",             "رسالة"),
            ("MTX",  "Matrix",             "مصفوفة"),
            ("DEC",  "Decision/Circular",  "قرار/تعميم"),
            ("CON",  "Contract",           "عقد"),
            ("ANX",  "Annex",              "ملحق"),
            ("REC",  "Record",             "سجل"),
        };

        var existing = await context.DocumentTypes
            .Select(t => t.Code)
            .ToListAsync();
        var order = 0;
        foreach (var (code, en, ar) in items)
        {
            order++;
            if (existing.Contains(code)) continue;
            context.DocumentTypes.Add(new DocumentType
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
