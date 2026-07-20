using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SshManager.ViewModels;

namespace SshManager.Views;

public partial class ServerEditorView : UserControl
{
    public ServerEditorView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ResetPasswordFields();
    }

    private MainViewModel? ViewModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private void ServerField_Changed(object sender, RoutedEventArgs e) => ViewModel?.OnServerFieldChanged();
    private void CommandField_Changed(object sender, TextChangedEventArgs e) => ViewModel?.OnCommandFieldChanged();

    private void CommandTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
            return;

        e.Handled = true;

        var caret = textBox.CaretIndex;
        var newLine = Environment.NewLine;
        textBox.Text = textBox.Text.Insert(caret, newLine);
        textBox.CaretIndex = caret + newLine.Length;
        ViewModel?.OnCommandFieldChanged();
    }

    private void ResetPasswordFields()
    {
        RevealCustomPasswordToggle.IsChecked = false;
        CustomPasswordHidden.Password = (DataContext as ServerItemViewModel)?.CustomPassword ?? string.Empty;
        CustomPasswordHidden.Visibility = Visibility.Visible;
        CustomPasswordVisible.Visibility = Visibility.Collapsed;
    }

    private void RevealCustomPasswordToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (RevealCustomPasswordToggle.IsChecked == true)
        {
            CustomPasswordVisible.Text = CustomPasswordHidden.Password;
            CustomPasswordHidden.Visibility = Visibility.Collapsed;
            CustomPasswordVisible.Visibility = Visibility.Visible;
            CustomPasswordVisible.Focus();
            return;
        }

        CustomPasswordHidden.Password = CustomPasswordVisible.Text;
        if (DataContext is ServerItemViewModel vm)
            vm.CustomPassword = CustomPasswordVisible.Text;

        CustomPasswordVisible.Visibility = Visibility.Collapsed;
        CustomPasswordHidden.Visibility = Visibility.Visible;
        CustomPasswordHidden.Focus();
    }

    private void CustomPasswordHidden_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (CustomPasswordHidden.Visibility != Visibility.Visible)
            return;

        if (DataContext is ServerItemViewModel vm)
            vm.CustomPassword = CustomPasswordHidden.Password;

        ViewModel?.OnServerFieldChanged();
    }
}
