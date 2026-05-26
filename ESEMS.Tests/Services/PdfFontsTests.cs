using ESEMS.Web.Services.Export;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace ESEMS.Tests.Services;

/// <summary>
/// F-002: QuestPDF's bundled font (Lato) has no Arabic glyphs, so Arabic PDF
/// columns rendered as empty boxes (tofu). PdfFonts registers an Arabic-capable
/// TrueType font and exposes it via <see cref="PdfFonts.Family"/>.
/// </summary>
public class PdfFontsTests
{
    [Fact]
    public void Family_IsResolved_AndArabicCapableOnWindows()
    {
        Assert.False(string.IsNullOrWhiteSpace(PdfFonts.Family));

        // On the Windows IIS host (and any dev box) C:\Windows\Fonts\tahoma.ttf
        // exists, so the registration must succeed and Family must NOT be the
        // glyph-less "Lato" fallback. (On a font-less Linux CI box it may stay
        // "Lato" by design — English still renders — so the strong assertion is
        // Windows-scoped.)
        if (OperatingSystem.IsWindows())
            Assert.Equal("AppArabic", PdfFonts.Family);
    }

    [Fact]
    public void RendersArabicText_WithRegisteredFont_ProducesValidPdf()
    {
        // Touching PdfFonts.Family runs the static ctor, which sets the QuestPDF
        // Community license and registers the font — required before GeneratePdf.
        var family = PdfFonts.Family;

        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(t => t.FontSize(12).FontFamily(family));
                page.Content().Text("اسم المخاطرة — Arabic / English mixed line");
            });
        }).GeneratePdf();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, $"PDF should be non-trivial; got {bytes.Length} bytes.");
        // %PDF header — proves a real document was produced without throwing.
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
