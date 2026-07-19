using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SshManager.ViewModels;

namespace SshManager;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.ConfirmSaveOnExit())
            e.Cancel = true;
    }

    private void ServerField_Changed(object sender, RoutedEventArgs e) => ViewModel.OnServerFieldChanged();
    private void CommandField_Changed(object sender, TextChangedEventArgs e) => ViewModel.OnCommandFieldChanged();
}
