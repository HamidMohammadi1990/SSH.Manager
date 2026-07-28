using System.ComponentModel;
using System.IO;
using System.Linq;
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
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.GroupSelectionRequested += ApplyGroupSelection;
        ViewModel.ServerSelectionRequested += ApplyServerSelection;
    }

    private bool _suppressGroupSelectionSync;
    private bool _suppressServerSelectionSync;

    private void GroupListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressGroupSelectionSync || sender is not ListBox listBox)
            return;

        ViewModel.SyncSelectedGroups(listBox.SelectedItems.Cast<GroupItemViewModel>().ToList());
    }

    private void GroupListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox)
            return;

        if (FindListBoxItem(listBox, e.OriginalSource) is not ListBoxItem item)
            return;

        if (item.DataContext is GroupItemViewModel group && !listBox.SelectedItems.Contains(group))
        {
            _suppressGroupSelectionSync = true;
            try
            {
                listBox.SelectedItems.Clear();
                listBox.SelectedItems.Add(group);
            }
            finally
            {
                _suppressGroupSelectionSync = false;
            }

            ViewModel.SyncSelectedGroups(new[] { group });
        }

        item.Focus();
    }

    private void ApplyGroupSelection(IReadOnlyList<GroupItemViewModel> groups)
    {
        _suppressGroupSelectionSync = true;
        try
        {
            GroupListBox.SelectedItems.Clear();
            foreach (var group in groups)
                GroupListBox.SelectedItems.Add(group);
        }
        finally
        {
            _suppressGroupSelectionSync = false;
        }
    }

    private void ServerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressServerSelectionSync || sender is not ListBox listBox)
            return;

        var selected = listBox.SelectedItems.Cast<ServerItemViewModel>().ToList();
        ViewModel.SyncSelectedServers(selected);

        if (selected.Count == 1)
            ViewModel.EnsureServerEditorOpen(selected[0]);
    }

    private void ApplyServerSelection(IReadOnlyList<ServerItemViewModel> servers)
    {
        _suppressServerSelectionSync = true;
        try
        {
            ServerListBox.SelectedItems.Clear();
            foreach (var server in servers)
                ServerListBox.SelectedItems.Add(server);
        }
        finally
        {
            _suppressServerSelectionSync = false;
        }
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

    private void ServerList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (Keyboard.Modifiers is ModifierKeys.Control or ModifierKeys.Shift)
            return;

        if (FindListBoxItem(listBox, e.OriginalSource) is not ListBoxItem { DataContext: ServerItemViewModel server })
            return;

        ViewModel.EnsureServerEditorOpen(server);
    }

    private void ServerList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ListBox listBox) return;
        if (FindListBoxItem(listBox, e.OriginalSource) is not ListBoxItem item)
            return;

        if (item.DataContext is ServerItemViewModel server && !listBox.SelectedItems.Contains(server))
        {
            _suppressServerSelectionSync = true;
            try
            {
                listBox.SelectedItems.Clear();
                listBox.SelectedItems.Add(server);
            }
            finally
            {
                _suppressServerSelectionSync = false;
            }

            ViewModel.SyncSelectedServers(new[] { server });
        }

        ViewModel.BeginContextMenuSelection();
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
