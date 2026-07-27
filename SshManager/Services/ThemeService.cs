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

    private static readonly IReadOnlyDictionary<string, Color> DarkPalette = new Dictionary<string, Color>
    {
        ["BgDark"] = ColorFromHex("#1E1E2E"),
        ["BgMedium"] = ColorFromHex("#252536"),
        ["BgLight"] = ColorFromHex("#2D2D44"),
        ["BgCard"] = ColorFromHex("#313145"),
        ["Accent"] = ColorFromHex("#7C4DFF"),
        ["AccentLight"] = ColorFromHex("#B388FF"),
        ["TextPrimary"] = ColorFromHex("#E8E8F0"),
        ["TextSecondary"] = ColorFromHex("#A0A0B8"),
        ["Border"] = ColorFromHex("#3D3D55"),
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

        if (colorDictionary != null)
            ApplyPalette(colorDictionary, palette);

        foreach (var dictionary in Application.Current.Resources.MergedDictionaries)
        {
            if (ReferenceEquals(dictionary, colorDictionary))
                continue;

            ApplyPalette(dictionary, palette, brushesOnly: true);
        }

        Current = theme;
    }

    private static void ApplyPalette(ResourceDictionary resources, IReadOnlyDictionary<string, Color> palette, bool brushesOnly = false)
    {
        if (!brushesOnly)
        {
            foreach (var key in ColorKeys)
            {
                if (palette.TryGetValue(key, out var color))
                    resources[key] = color;
            }
        }

        foreach (var brushKey in BrushKeys)
        {
            var colorKey = brushKey.Replace("Brush", string.Empty, StringComparison.Ordinal);
            if (resources[brushKey] is SolidColorBrush brush && palette.TryGetValue(colorKey, out var color))
                brush.Color = color;
        }

        UpdateSystemBrush(resources, SystemColors.MenuBrushKey, palette["BgMedium"]);
        UpdateSystemBrush(resources, SystemColors.MenuTextBrushKey, palette["TextPrimary"]);
        UpdateSystemBrush(resources, SystemColors.MenuBarBrushKey, palette["BgMedium"]);
        UpdateSystemBrush(resources, SystemColors.ControlBrushKey, palette["BgMedium"]);
        UpdateSystemBrush(resources, SystemColors.ControlTextBrushKey, palette["TextPrimary"]);
        UpdateSystemBrush(resources, SystemColors.HighlightBrushKey, palette["BgLight"]);
        UpdateSystemBrush(resources, SystemColors.HighlightTextBrushKey, palette["TextPrimary"]);
        UpdateSystemBrush(resources, SystemColors.WindowBrushKey, palette["BgCard"]);
        UpdateSystemBrush(resources, SystemColors.WindowTextBrushKey, palette["TextPrimary"]);
        UpdateSystemBrush(resources, SystemColors.ControlDarkBrushKey, palette["Border"]);
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

    private static void UpdateSystemBrush(ResourceDictionary resources, ResourceKey key, Color color)
    {
        if (resources[key] is SolidColorBrush brush)
            brush.Color = color;
    }

    private static Color ColorFromHex(string hex) =>
        (Color)ColorConverter.ConvertFromString(hex)!;
}
