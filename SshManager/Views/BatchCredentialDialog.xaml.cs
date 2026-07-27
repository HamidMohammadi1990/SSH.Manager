using System.Windows;
using SshManager.Services;

namespace SshManager.Views;

public partial class BatchCredentialDialog : Window
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string EnablePassword { get; set; } = string.Empty;
    public bool RequiresEnablePassword { get; set; }
    public string BatchSummary { get; set; } = string.Empty;

    public BatchCredentialDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UsernameBox.Text = Username;
        PasswordHiddenBox.Password = Password;
        EnablePasswordHiddenBox.Password = EnablePassword;
        BatchSummaryText.Text = BatchSummary;
        EnablePasswordPanel.Visibility = RequiresEnablePassword ? Visibility.Visible : Visibility.Collapsed;
        UsernameBox.Focus();
    }

    private void RevealPasswordToggle_Changed(object sender, RoutedEventArgs e) =>
        TogglePasswordVisibility(
            RevealPasswordToggle, PasswordHiddenBox, PasswordVisibleBox);

    private void RevealEnablePasswordToggle_Changed(object sender, RoutedEventArgs e) =>
        TogglePasswordVisibility(
            RevealEnablePasswordToggle, EnablePasswordHiddenBox, EnablePasswordVisibleBox);

    private static void TogglePasswordVisibility(
        System.Windows.Controls.Primitives.ToggleButton toggle,
        System.Windows.Controls.PasswordBox hidden,
        System.Windows.Controls.TextBox visible)
    {
        if (toggle.IsChecked == true)
        {
            visible.Text = hidden.Password;
            hidden.Visibility = Visibility.Collapsed;
            visible.Visibility = Visibility.Visible;
            visible.Focus();
            return;
        }

        hidden.Password = visible.Text;
        visible.Visibility = Visibility.Collapsed;
        hidden.Visibility = Visibility.Visible;
        hidden.Focus();
    }

    private string CurrentPassword =>
        RevealPasswordToggle.IsChecked == true
            ? PasswordVisibleBox.Text
            : PasswordHiddenBox.Password;

    private string CurrentEnablePassword =>
        RevealEnablePasswordToggle.IsChecked == true
            ? EnablePasswordVisibleBox.Text
            : EnablePasswordHiddenBox.Password;

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        Username = UsernameBox.Text.Trim();
        Password = CurrentPassword;
        EnablePassword = CurrentEnablePassword;

        if (string.IsNullOrWhiteSpace(Username))
        {
            DialogService.ShowWarning("Username is required.", "Validation");
            UsernameBox.Focus();
            return;
        }

        if (string.IsNullOrEmpty(Password))
        {
            DialogService.ShowWarning("Password is required.", "Validation");
            PasswordHiddenBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
