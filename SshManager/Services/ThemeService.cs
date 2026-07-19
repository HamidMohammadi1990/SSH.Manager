using System.Windows;

namespace SshManager.Services;

public static class ThemeService
{
    private const string DarkThemeUri = "Themes/DarkTheme.xaml";
    private const string LightThemeUri = "Themes/LightTheme.xaml";

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        var uri = new Uri(theme == AppTheme.Dark ? DarkThemeUri : LightThemeUri, UriKind.Relative);
        var newTheme = new ResourceDictionary { Source = uri };

        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(newTheme);
    }
}
