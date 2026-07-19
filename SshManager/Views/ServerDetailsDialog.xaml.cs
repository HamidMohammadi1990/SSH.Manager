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

        // Let the window paint first, then start background collection.
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => _ = LoadSafeAsync(vm));
    }

    private async Task LoadSafeAsync(ServerDetailsViewModel vm)
    {
        try
        {
            await vm.LoadAsync();
        }
        catch (Exception ex)
        {
            vm.StatusMessage = ex.Message;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ServerDetailsViewModel vm)
            vm.CancelLoading();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
