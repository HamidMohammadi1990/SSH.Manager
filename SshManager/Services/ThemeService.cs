using System.Windows;
using SshManager.Models;

namespace SshManager.Services;

public static class ThemeService
{
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        CurrentTheme = theme;
        var uri = new Uri(theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri, UriKind.Relative);
        var newTheme = new ResourceDictionary { Source = uri };

        var merged = app.Resources.MergedDictionaries;
        merged.Clear();
        merged.Add(newTheme);

        foreach (Window window in app.Windows)
            RefreshWindowTheme(window);

        ThemeChanged?.Invoke(theme);
    }

    private static void RefreshWindowTheme(Window window)
    {
        window.Background = (System.Windows.Media.Brush)window.FindResource("BgDarkBrush");
        window.Foreground = (System.Windows.Media.Brush)window.FindResource("TextPrimaryBrush");
        window.InvalidateVisual();
        window.UpdateLayout();
    }
}
