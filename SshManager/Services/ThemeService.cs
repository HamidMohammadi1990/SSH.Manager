using System.Windows;
using System.Windows.Media;
using SshManager.Models;

namespace SshManager.Services;

public static class ThemeService
{
    private static readonly string[] ColorKeys =
    [
        "BgDark", "BgMedium", "BgLight", "BgCard",
        "Accent", "AccentLight", "TextPrimary", "TextSecondary", "Border",
        "Success", "Error", "Warning"
    ];

    private static readonly string[] BrushKeys =
    [
        "BgDarkBrush", "BgMediumBrush", "BgLightBrush", "BgCardBrush",
        "AccentBrush", "AccentLightBrush", "TextPrimaryBrush", "TextSecondaryBrush", "BorderBrush",
        "SuccessBrush", "ErrorBrush", "WarningBrush"
    ];

    private static readonly (ResourceKey Key, string ColorKey)[] SystemBrushKeys =
    [
        (SystemColors.MenuBrushKey, "BgMedium"),
        (SystemColors.MenuTextBrushKey, "TextPrimary"),
        (SystemColors.MenuBarBrushKey, "BgMedium"),
        (SystemColors.ControlBrushKey, "BgMedium"),
        (SystemColors.ControlTextBrushKey, "TextPrimary"),
        (SystemColors.HighlightBrushKey, "BgLight"),
        (SystemColors.HighlightTextBrushKey, "TextPrimary"),
        (SystemColors.WindowBrushKey, "BgCard"),
        (SystemColors.WindowTextBrushKey, "TextPrimary"),
        (SystemColors.ControlDarkBrushKey, "Border")
    ];

    private static readonly IReadOnlyDictionary<string, Color> DarkPalette = new Dictionary<string, Color>
    {
        ["BgDark"] = ColorFromHex("#121212"),
        ["BgMedium"] = ColorFromHex("#1E1E1E"),
        ["BgLight"] = ColorFromHex("#2D2D2D"),
        ["BgCard"] = ColorFromHex("#252526"),
        ["Accent"] = ColorFromHex("#0078D4"),
        ["AccentLight"] = ColorFromHex("#4CA3E8"),
        ["TextPrimary"] = ColorFromHex("#E8E8E8"),
        ["TextSecondary"] = ColorFromHex("#9E9E9E"),
        ["Border"] = ColorFromHex("#3E3E3E"),
        ["Success"] = ColorFromHex("#4CAF50"),
        ["Error"] = ColorFromHex("#F44336"),
        ["Warning"] = ColorFromHex("#FFC107")
    };

    private static readonly IReadOnlyDictionary<string, Color> LightPalette = new Dictionary<string, Color>
    {
        ["BgDark"] = ColorFromHex("#F5F5F8"),
        ["BgMedium"] = ColorFromHex("#FFFFFF"),
        ["BgLight"] = ColorFromHex("#ECECF2"),
        ["BgCard"] = ColorFromHex("#E4E4EC"),
        ["Accent"] = ColorFromHex("#5E35B1"),
        ["AccentLight"] = ColorFromHex("#7E57C2"),
        ["TextPrimary"] = ColorFromHex("#1A1A2E"),
        ["TextSecondary"] = ColorFromHex("#5C5C72"),
        ["Border"] = ColorFromHex("#D0D0DC"),
        ["Success"] = ColorFromHex("#388E3C"),
        ["Error"] = ColorFromHex("#D32F2F"),
        ["Warning"] = ColorFromHex("#F9A825")
    };

    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static void Apply(AppTheme theme)
    {
        if (Application.Current == null)
            return;

        var palette = theme == AppTheme.Light ? LightPalette : DarkPalette;
        var colorDictionary = FindColorDictionary();
        if (colorDictionary == null)
            return;

        ApplyPalette(colorDictionary, palette);
        Current = theme;
    }

    private static void ApplyPalette(ResourceDictionary resources, IReadOnlyDictionary<string, Color> palette)
    {
        foreach (var key in ColorKeys)
        {
            if (palette.TryGetValue(key, out var color))
                resources[key] = color;
        }

        foreach (var brushKey in BrushKeys)
        {
            var colorKey = brushKey.Replace("Brush", string.Empty, StringComparison.Ordinal);
            if (palette.TryGetValue(colorKey, out var color))
                resources[brushKey] = CreateBrush(color);
        }

        foreach (var (key, colorKey) in SystemBrushKeys)
            resources[key] = CreateBrush(palette[colorKey]);
    }

    private static ResourceDictionary? FindColorDictionary()
    {
        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
        {
            if (dictionary.Contains("BgDarkBrush"))
                return dictionary;
        }

        return null;
    }

    private static SolidColorBrush CreateBrush(Color color) => new(color);

    private static Color ColorFromHex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}
