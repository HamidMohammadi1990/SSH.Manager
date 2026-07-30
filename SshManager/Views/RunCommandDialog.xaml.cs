using System.Windows;
using System.Windows.Controls;
using SshManager.Models;
using SshManager.Services;

namespace SshManager.Views;

public partial class RunCommandDialog : Window
{
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string EnablePassword { get; private set; } = string.Empty;
    public string TargetsText { get; private set; } = string.Empty;
    public string CommandsText { get; private set; } = string.Empty;
    public ConnectionType ConnectionType => SshRadio.IsChecked == true ? ConnectionType.Ssh : ConnectionType.Telnet;

    public RunCommandDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void Initialize(
        string defaultUsername,
        ConnectionType connectionType,
        IEnumerable<string> initialTargets)
    {
        Username = defaultUsername;
        TargetsText = string.Join(Environment.NewLine, initialTargets
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        PrefillHintText.Text = TargetsText.Length > 0
            ? "Targets were filled from selected servers. You can edit or paste more IPs."
            : "Paste target IPs (one per line), then enter commands.";

        if (connectionType == ConnectionType.Ssh)
            SshRadio.IsChecked = true;
        else
            TelnetRadio.IsChecked = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UsernameBox.Text = Username;
        PasswordHiddenBox.Password = Password;
        EnablePasswordHiddenBox.Password = EnablePassword;
        TargetsBox.Text = TargetsText;
        CommandsBox.Text = CommandsText;
        UpdateEnablePasswordVisibility();
        UsernameBox.Focus();
    }

    private void CommandsBox_TextChanged(object sender, TextChangedEventArgs e) =>
        UpdateEnablePasswordVisibility();

    private void UpdateEnablePasswordVisibility()
    {
        var text = CommandsBox.Text ?? string.Empty;
        var needsPassword = text.Contains("<password>", StringComparison.OrdinalIgnoreCase);
        EnablePasswordPanel.Visibility = needsPassword ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RevealPasswordToggle_Changed(object sender, RoutedEventArgs e) =>
        TogglePasswordVisibility(RevealPasswordToggle, PasswordHiddenBox, PasswordVisibleBox);

    private void RevealEnablePasswordToggle_Changed(object sender, RoutedEventArgs e) =>
        TogglePasswordVisibility(RevealEnablePasswordToggle, EnablePasswordHiddenBox, EnablePasswordVisibleBox);

    private static void TogglePasswordVisibility(
        System.Windows.Controls.Primitives.ToggleButton toggle,
        PasswordBox hidden,
        TextBox visible)
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

    private void Execute_Click(object sender, RoutedEventArgs e)
    {
        Username = UsernameBox.Text.Trim();
        Password = CurrentPassword;
        EnablePassword = CurrentEnablePassword;
        TargetsText = TargetsBox.Text;
        CommandsText = CommandsBox.Text.Trim();

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

        if (RunCommandJobBuilder.ParseTargets(TargetsText).Count == 0)
        {
            DialogService.ShowWarning("Enter at least one target IP or hostname (one per line).", "Validation");
            TargetsBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(CommandsText))
        {
            DialogService.ShowWarning("Enter at least one command.", "Validation");
            CommandsBox.Focus();
            return;
        }

        if (InteractiveStepExpander.ExpandCommandText(CommandsText).Count == 0)
        {
            DialogService.ShowWarning("Commands are empty after parsing.", "Validation");
            CommandsBox.Focus();
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
