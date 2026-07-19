using System.Windows;
using SshManager.ViewModels;

namespace SshManager.Views;

public partial class ServerDetailsDialog : Window
{
    private bool _loadStarted;

    public ServerDetailsDialog()
    {
        InitializeComponent();
    }

    private void Window_ContentRendered(object sender, EventArgs e)
    {
        if (_loadStarted || DataContext is not ServerDetailsViewModel vm)
            return;

        _loadStarted = true;
        _ = vm.LoadAsync();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is ServerDetailsViewModel vm)
            vm.CancelLoading();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
