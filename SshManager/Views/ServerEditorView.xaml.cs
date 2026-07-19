using System.Windows;
using System.Windows.Controls;
using SshManager.ViewModels;

namespace SshManager.Views;

public partial class ServerEditorView : UserControl
{
    public ServerEditorView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void ServerField_Changed(object sender, RoutedEventArgs e) => ViewModel?.OnServerFieldChanged();
    private void CommandField_Changed(object sender, TextChangedEventArgs e) => ViewModel?.OnCommandFieldChanged();
}
