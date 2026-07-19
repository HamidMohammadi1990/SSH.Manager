using System.Windows;
using System.Windows.Threading;
using SshManager.ViewModels;

namespace SshManager.Views;

public partial class ServerDetailsDialog : Window
{
    private bool _loadStarted;

    public ServerDetailsDialog()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loadStarted || DataContext is not ServerDetailsViewModel vm)
            return;

        _loadStarted = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () => _ = LoadSafeAsync(vm));
    }

    private static async Task LoadSafeAsync(ServerDetailsViewModel vm)
    {
        try
        {
            await vm.LoadAsync();
        }
        catch (Exception)
        {
            // Errors are surfaced inside the view model; swallow to avoid crashing the app.
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ServerDetailsViewModel vm)
            vm.OnDialogClosed();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
