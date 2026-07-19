using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SshManager.Models;

namespace SshManager.Services;

public static class ThemeService
{
    private const string ConvertersUri = "Themes/Converters.xaml";
    private const string ComboBoxStylesUri = "Themes/ComboBoxStyles.xaml";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;

    public static event Action<AppTheme>? ThemeChanged;

    public static void ApplyTheme(AppTheme theme)
    {
        var app = Application.Current;
        if (app == null) return;

        CurrentTheme = theme;
        var colorsUri = new Uri(theme == AppTheme.Dark ? "Themes/DarkColors.xaml" : "Themes/LightColors.xaml", UriKind.Relative);
        var controlsUri = new Uri(theme == AppTheme.Dark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml", UriKind.Relative);

        var merged = app.Resources.MergedDictionaries;

        ResourceDictionary? converters = null;
        ResourceDictionary? comboStyles = null;

        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Converters", StringComparison.OrdinalIgnoreCase))
                converters = merged[i];
            else if (source.Contains("ComboBoxStyles", StringComparison.OrdinalIgnoreCase))
                comboStyles = merged[i];
        }

        merged.Clear();

        merged.Add(converters ?? new ResourceDictionary { Source = new Uri(ConvertersUri, UriKind.Relative) });
        merged.Add(new ResourceDictionary { Source = colorsUri });
        merged.Add(comboStyles ?? new ResourceDictionary { Source = new Uri(ComboBoxStylesUri, UriKind.Relative) });
        merged.Add(new ResourceDictionary { Source = controlsUri });

        app.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            foreach (Window window in app.Windows)
                RefreshWindowTheme(window);

            ThemeChanged?.Invoke(theme);
        });
    }

    private static void RefreshWindowTheme(Window window)
    {
        if (window.FindResource("BgDarkBrush") is System.Windows.Media.Brush background)
            window.Background = background;
        if (window.FindResource("TextPrimaryBrush") is System.Windows.Media.Brush foreground)
            window.Foreground = foreground;
    }
}
