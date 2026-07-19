using System.Linq;
using System.Windows;
using SshManager.Models;

namespace SshManager.Services;

public static class ThemeService
{
    private const string ConvertersUri = "Themes/Converters.xaml";
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        CurrentTheme = theme;
        var themeUri = new Uri(theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri, UriKind.Relative);

        var merged = app.Resources.MergedDictionaries;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("DarkTheme", StringComparison.OrdinalIgnoreCase) ||
                source.Contains("LightTheme", StringComparison.OrdinalIgnoreCase))
            {
                merged.RemoveAt(i);
            }
        }

        merged.Add(new ResourceDictionary { Source = themeUri });

        foreach (Window window in app.Windows)
            RefreshWindowTheme(window);

        ThemeChanged?.Invoke(theme);
    }

    public static void EnsureResourcesLoaded()
    {
        var app = Application.Current;
        if (app == null) return;

        var merged = app.Resources.MergedDictionaries;
        if (merged.Any(d => d.Source?.OriginalString?.Contains("Converters", StringComparison.OrdinalIgnoreCase) == true))
            return;

        merged.Insert(0, new ResourceDictionary { Source = new Uri(ConvertersUri, UriKind.Relative) });
    }

    private static void RefreshWindowTheme(Window window)
    {
        window.Background = (System.Windows.Media.Brush)window.FindResource("BgDarkBrush");
        window.Foreground = (System.Windows.Media.Brush)window.FindResource("TextPrimaryBrush");
        window.InvalidateVisual();
        window.UpdateLayout();
    }
}
