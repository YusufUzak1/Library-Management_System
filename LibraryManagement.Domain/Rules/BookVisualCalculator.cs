using LibraryManagement.Domain.Entities;
using LibraryManagement.Domain.Enums;

namespace LibraryManagement.Domain.Rules;

public sealed record BookVisualMetrics(
    BookCategoryTheme Theme,
    string PrimaryCoverColorHex,
    string SecondaryCoverColorHex,
    string SpineColorHex,
    string AccentFoilColorHex,
    string PageEdgeColorHex,
    double WidthPx,
    double HeightPx,
    double SpineThicknessPx,
    double CornerRadiusPx,
    string MotifCode,
    string CategoryBadgeLabel,
    bool HasRaisedSpineBands,
    int SpineBandCount,
    bool HasGoldCornerOrnaments);

public static class BookVisualCalculator
{
    public static BookVisualMetrics Compute(Book book)
    {
        ArgumentNullException.ThrowIfNull(book);

        int effectivePages = book.PageCount > 0 ? book.PageCount : GetDefaultPages(book.CategoryTheme);

        // Thickness scaled naturally from page count (between 14px for thin poetry and 46px for thick fantasy/tomes)
        double spineThickness = Math.Clamp(12.0 + (effectivePages / 22.0), 14.0, 48.0);

        // Format determines width and height
        (double baseWidth, double baseHeight) = book.PhysicalFormat switch
        {
            BookPhysicalFormat.PocketPaperback => (124.0, 174.0),
            BookPhysicalFormat.StandardHardcover => (142.0, 202.0),
            BookPhysicalFormat.RoyalOctavo => (156.0, 224.0),
            BookPhysicalFormat.LargeQuartoFolio => (172.0, 240.0),
            _ => (142.0, 202.0)
        };

        // Category-specific default geometry adjustments
        if (book.CategoryTheme == BookCategoryTheme.Childrens && book.PhysicalFormat == BookPhysicalFormat.StandardHardcover)
        {
            baseWidth = 136.0;
            baseHeight = 180.0;
        }
        else if (book.CategoryTheme == BookCategoryTheme.Poetry && book.PageCount <= 220)
        {
            spineThickness = Math.Clamp(spineThickness * 0.78, 13.0, 24.0);
        }
        else if (book.CategoryTheme == BookCategoryTheme.Fantasy && effectivePages >= 450)
        {
            spineThickness = Math.Clamp(spineThickness * 1.12, 28.0, 48.0);
        }

        var profile = GetDefaultPalette(book.CategoryTheme);

        string primaryHex = !string.IsNullOrWhiteSpace(book.PrimaryCoverHex)
            ? book.PrimaryCoverHex
            : profile.PrimaryHex;

        string accentHex = !string.IsNullOrWhiteSpace(book.AccentFoilHex)
            ? book.AccentFoilHex
            : profile.AccentHex;

        int spineBands = book.CategoryTheme is BookCategoryTheme.Fantasy or BookCategoryTheme.ClassicLiterature or BookCategoryTheme.HistoryAndPhilosophy
            ? (effectivePages > 450 ? 5 : 4)
            : 2;

        return new BookVisualMetrics(
            Theme: book.CategoryTheme,
            PrimaryCoverColorHex: primaryHex,
            SecondaryCoverColorHex: profile.SecondaryHex,
            SpineColorHex: profile.SpineHex,
            AccentFoilColorHex: accentHex,
            PageEdgeColorHex: profile.PageEdgeHex,
            WidthPx: baseWidth,
            HeightPx: baseHeight,
            SpineThicknessPx: Math.Round(spineThickness, 1),
            CornerRadiusPx: book.CategoryTheme == BookCategoryTheme.Childrens ? 8.0 : 4.5,
            MotifCode: profile.MotifCode,
            CategoryBadgeLabel: string.IsNullOrWhiteSpace(book.CategoryName) ? profile.Label : book.CategoryName,
            HasRaisedSpineBands: profile.HasRaisedBands,
            SpineBandCount: spineBands,
            HasGoldCornerOrnaments: profile.HasCornerOrnaments);
    }

    private static int GetDefaultPages(BookCategoryTheme theme) => theme switch
    {
        BookCategoryTheme.Poetry => 140,
        BookCategoryTheme.Childrens => 96,
        BookCategoryTheme.Fantasy => 540,
        BookCategoryTheme.Academic => 460,
        BookCategoryTheme.ScienceFiction => 380,
        _ => 320
    };

    private static (string PrimaryHex, string SecondaryHex, string SpineHex, string AccentHex, string PageEdgeHex, string MotifCode, string Label, bool HasRaisedBands, bool HasCornerOrnaments)
        GetDefaultPalette(BookCategoryTheme theme) => theme switch
        {
            BookCategoryTheme.Fantasy => (
                "#183A2F", "#0E241D", "#102820", "#D9A84E", "#EFE4CE",
                "CELESTIAL_CREST", "Fantastik Kurgu", true, true),

            BookCategoryTheme.Academic => (
                "#243242", "#1A2533", "#16202B", "#C5CED8", "#F4F1EA",
                "STRUCTURAL_GRID", "Akademik & Bilim", false, false),

            BookCategoryTheme.ScienceFiction => (
                "#152238", "#0B1324", "#0E182B", "#4FD1C5", "#E8EEF2",
                "ORBITAL_HEX", "Bilim Kurgu", false, false),

            BookCategoryTheme.Poetry => (
                "#4A2E35", "#351F25", "#2D1A1F", "#E6C79C", "#F6EFE2",
                "BOTANICAL_LINE", "Şiir & Edebiyat", false, true),

            BookCategoryTheme.Childrens => (
                "#C85A32", "#9E3F1E", "#8A3416", "#F7D060", "#FFF8EB",
                "STARBURST_PLAY", "Çocuk Kitaplığı", false, false),

            BookCategoryTheme.HistoryAndPhilosophy => (
                "#4E2A1E", "#361B12", "#2B150E", "#D4A359", "#EDE0C8",
                "CLASSICAL_COLUMN", "Tarih & Felsefe", true, true),

            BookCategoryTheme.ArtAndArchitecture => (
                "#2C3531", "#1E2522", "#181E1B", "#C99E67", "#F3EEE6",
                "ARCH_FRAME", "Sanat & Mimarlık", false, true),

            _ => (
                "#1F3B34", "#142823", "#11221E", "#D2A353", "#F1E7D3",
                "CLASSIC_MEDALLION", "Klasik Edebiyat", true, true)
        };
}

