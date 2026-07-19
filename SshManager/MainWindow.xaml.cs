using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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

    private void GroupField_Changed(object sender, TextChangedEventArgs e) => ViewModel.OnGroupFieldChanged();

    private void ServerList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;

        var element = listBox.InputHitTest(e.GetPosition(listBox)) as DependencyObject;
        while (element != null && element is not ListBoxItem)
            element = VisualTreeHelper.GetParent(element);

        if (element is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }
}
