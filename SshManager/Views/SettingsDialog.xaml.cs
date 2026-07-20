using System.Windows;
using SshManager.Services;

namespace SshManager.Views;

public partial class SettingsDialog : Window
{
    public string DefaultUsername { get; set; } = string.Empty;
    public string DefaultPassword { get; set; } = string.Empty;
    public int ConnectionTimeout { get; set; } = 30;
    public int CommandTimeout { get; set; } = 60;
    public int BatchStepDelay { get; set; } = 500;

    public SettingsDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            UsernameBox.Text = DefaultUsername;
            PasswordHiddenBox.Password = DefaultPassword;
            ConnectionTimeoutBox.Text = ConnectionTimeout.ToString();
            CommandTimeoutBox.Text = CommandTimeout.ToString();
            BatchStepDelayBox.Text = BatchStepDelay.ToString();
        };
    }

    private void RevealPasswordToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (RevealPasswordToggle.IsChecked == true)
        {
            PasswordVisibleBox.Text = PasswordHiddenBox.Password;
            PasswordHiddenBox.Visibility = Visibility.Collapsed;
            PasswordVisibleBox.Visibility = Visibility.Visible;
            PasswordVisibleBox.Focus();
            return;
        }

        PasswordHiddenBox.Password = PasswordVisibleBox.Text;
        PasswordVisibleBox.Visibility = Visibility.Collapsed;
        PasswordHiddenBox.Visibility = Visibility.Visible;
        PasswordHiddenBox.Focus();
    }

    private string CurrentPassword =>
        RevealPasswordToggle.IsChecked == true
            ? PasswordVisibleBox.Text
            : PasswordHiddenBox.Password;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        DefaultUsername = UsernameBox.Text;
        DefaultPassword = CurrentPassword;

        if (!int.TryParse(ConnectionTimeoutBox.Text, out var connTimeout) || connTimeout < 1)
        {
            DialogService.ShowWarning("Connection timeout must be a positive number.", "Validation");
            return;
        }

        if (!int.TryParse(CommandTimeoutBox.Text, out var cmdTimeout) || cmdTimeout < 1)
        {
            DialogService.ShowWarning("Command timeout must be a positive number.", "Validation");
            return;
        }

        if (!int.TryParse(BatchStepDelayBox.Text, out var batchDelay) || batchDelay < 0)
        {
            DialogService.ShowWarning("Batch step delay must be zero or a positive number.", "Validation");
            return;
        }

        ConnectionTimeout = connTimeout;
        CommandTimeout = cmdTimeout;
        BatchStepDelay = batchDelay;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
