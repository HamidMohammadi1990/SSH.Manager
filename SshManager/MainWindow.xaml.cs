using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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

    private void ServerList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (FindListBoxItem(listBox, e.OriginalSource) is not ListBoxItem { DataContext: ServerItemViewModel server })
            return;

        ViewModel.EnsureServerEditorOpen(server);
    }

    private void ServerList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (FindListBoxItem(listBox, e.OriginalSource) is not ListBoxItem item)
            return;

        ViewModel.BeginContextMenuSelection();
        item.IsSelected = true;
        item.Focus();
    }

    private static ListBoxItem? FindListBoxItem(ListBox listBox, object? source)
    {
        if (source is not DependencyObject element)
            return null;

        return ItemsControl.ContainerFromElement(listBox, element) as ListBoxItem;
    }

    private async void ViewDetailsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ServerListBox.SelectedItem is not ServerItemViewModel server)
                return;

            if (sender is MenuItem { Parent: ContextMenu menu })
                menu.IsOpen = false;

            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
            ViewModel.OpenServerDetails(server);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Server Details", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
