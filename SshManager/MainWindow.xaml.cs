using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SshManager.ViewModels;

namespace SshManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        TryLoadWindowIcon();
    }

    private void TryLoadWindowIcon()
    {
        var paths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app-icon.ico"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", "app-icon.ico"))
        };

        foreach (var path in paths)
        {
            if (!File.Exists(path)) continue;
            Icon = BitmapFrame.Create(new Uri(path, UriKind.Absolute), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            break;
        }
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.ConfirmSaveOnExit())
            e.Cancel = true;
    }

    private void ServerField_Changed(object sender, RoutedEventArgs e) => ViewModel.OnServerFieldChanged();
    private void CommandField_Changed(object sender, TextChangedEventArgs e) => ViewModel.OnCommandFieldChanged();
    private void GroupField_Changed(object sender, TextChangedEventArgs e) => ViewModel.OnGroupFieldChanged();
}
