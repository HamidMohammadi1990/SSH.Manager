using System.Windows;
using SshManager.ViewModels;

namespace SshManager.Views;

public partial class ServerDetailsDialog : Window
{
    public ServerDetailsDialog()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ServerDetailsViewModel vm)
            await vm.LoadAsync();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
