using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace ESEMS.Web.Services.Export;

/// <summary>
/// QuestPDF font bootstrap. QuestPDF's bundled font (Lato) has no Arabic glyphs,
/// so the Arabic ("Name (AR)") columns in PDF exports render as empty boxes
/// (tofu) — see audit finding F-002. This registers an Arabic-capable TrueType
/// font under the logical family name exposed by <see cref="Family"/>; every PDF
/// <c>DefaultTextStyle</c> should use that family.
///
/// On the Windows IIS deploy (EC2) the fonts live in C:\Windows\Fonts. On a host
/// where none of the candidates are found, <see cref="Family"/> stays "Lato"
/// (English renders fine; Arabic degrades) rather than throwing.
/// </summary>
public static class PdfFonts
{
    /// <summary>Font family for PDF text — Arabic-capable when a font was found.</summary>
    public static string Family { get; }

    static PdfFonts()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        Family = "Lato"; // QuestPDF's bundled default (no Arabic glyphs)

        // Arabic-capable TrueType fonts in preference order. Tahoma and Arial
        // both carry full Arabic on Windows; the Noto paths cover Linux hosts.
        string[] candidates =
        {
            @"C:\Windows\Fonts\tahoma.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\segoeui.ttf",
            @"C:\Windows\Fonts\trado.ttf",
            "/usr/share/fonts/truetype/noto/NotoSansArabic-Regular.ttf",
            "/usr/share/fonts/noto/NotoSansArabic-Regular.ttf"
        };

        foreach (var path in candidates)
        {
            try
            {
                if (!File.Exists(path)) continue;
                using var fs = File.OpenRead(path);
                FontManager.RegisterFontWithCustomName("AppArabic", fs);
                Family = "AppArabic";
                break;
            }
            catch
            {
                // Unreadable/locked font file — try the next candidate.
            }
        }
    }
}
