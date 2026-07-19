using System.Windows;

namespace SshManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var dataService = new Services.JsonDataService();
        var data = dataService.Load();
        Services.ThemeService.ApplyTheme(data.Settings.Theme);

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
