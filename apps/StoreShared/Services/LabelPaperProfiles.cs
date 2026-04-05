namespace TikTokOrderPrinter.Services;

public static class LabelPaperProfiles
{
    private static readonly Dictionary<string, LabelPaperProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["30x40"] = new("30x40", "30 x 40 mm", 30, 40, 18, 5.6f, 4.2f, 1.2d),
        ["100x100"] = new("100x100", "100 x 100 mm", 100, 100, 34, 8.0f, 6.0f, 2.5d),
        ["100x150"] = new("100x150", "100 x 150 mm", 100, 150, 42, 8.6f, 6.3f, 2.5d)
    };

    public static LabelPaperProfile Resolve(
        string? key,
        double? customWidthMm = null,
        double? customHeightMm = null,
        int? paperWidthCharacters = null,
        float? baseFontSize = null,
        float? minFontSize = null,
        double? marginMm = null)
    {
        var normalized = NormalizeKey(key);
        var baseProfile = normalized == "custom"
            ? CreateCustomProfile(customWidthMm, customHeightMm)
            : Profiles.TryGetValue(normalized, out var profile)
                ? profile
                : Profiles["100x150"];

        var contentWidth = paperWidthCharacters.GetValueOrDefault(baseProfile.ContentWidthCharacters);
        if (contentWidth <= 0)
        {
            contentWidth = baseProfile.ContentWidthCharacters;
        }

        var resolvedBaseFont = baseFontSize.GetValueOrDefault(baseProfile.BaseFontSize);
        var resolvedMinFont = minFontSize.GetValueOrDefault(baseProfile.MinFontSize);
        if (resolvedMinFont <= 0f)
        {
            resolvedMinFont = baseProfile.MinFontSize;
        }

        if (resolvedBaseFont < resolvedMinFont)
        {
            resolvedBaseFont = resolvedMinFont;
        }

        var resolvedMargin = marginMm.GetValueOrDefault(baseProfile.MarginMm);
        if (resolvedMargin <= 0d)
        {
            resolvedMargin = baseProfile.MarginMm;
        }

        return baseProfile with
        {
            ContentWidthCharacters = contentWidth,
            BaseFontSize = resolvedBaseFont,
            MinFontSize = resolvedMinFont,
            MarginMm = resolvedMargin
        };
    }

    public static string NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "100x150";
        }

        var normalized = key
            .Trim()
            .Replace("mm", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return string.IsNullOrWhiteSpace(normalized) ? "100x150" : normalized;
    }

    private static LabelPaperProfile CreateCustomProfile(double? customWidthMm, double? customHeightMm)
    {
        var width = customWidthMm.GetValueOrDefault(100d);
        var height = customHeightMm.GetValueOrDefault(150d);

        if (width <= 0d)
        {
            width = 100d;
        }

        if (height <= 0d)
        {
            height = 150d;
        }

        var contentWidth = Math.Max(18, (int)Math.Round(width * 0.42d, MidpointRounding.AwayFromZero));
        var baseFont = width <= 40d ? 5.6f : height >= 140d ? 8.6f : 8.0f;
        var minFont = width <= 40d ? 4.2f : height >= 140d ? 6.3f : 6.0f;
        var margin = width <= 40d ? 1.2d : 2.5d;

        return new LabelPaperProfile(
            "custom",
            $"{width:0.##} x {height:0.##} mm (Custom)",
            width,
            height,
            contentWidth,
            baseFont,
            minFont,
            margin);
    }

    public sealed record LabelPaperProfile(
        string Key,
        string DisplayName,
        double WidthMm,
        double HeightMm,
        int ContentWidthCharacters,
        float BaseFontSize,
        float MinFontSize,
        double MarginMm);
}
