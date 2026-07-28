using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SshManager.Models;
using SshManager.Services;

namespace SshManager.Views;

public partial class TargetEntry : ObservableObject
{
    [ObservableProperty] private string _value = string.Empty;
}

public partial class RunCommandDialog : Window
{
    public ObservableCollection<TargetEntry> Targets { get; } = new();

    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;
    public string EnablePassword { get; private set; } = string.Empty;
    public string CommandsText { get; private set; } = string.Empty;
    public ConnectionType ConnectionType => SshRadio.IsChecked == true ? ConnectionType.Ssh : ConnectionType.Telnet;

    public RunCommandDialog()
    {
        InitializeComponent();
        TargetsList.ItemsSource = Targets;
        Loaded += OnLoaded;
    }

    public void Initialize(
        string defaultUsername,
        ConnectionType connectionType,
        IEnumerable<string> initialTargets,
        bool prefilledFromSelection)
    {
        Username = defaultUsername;
        PrefillHintText.Text = prefilledFromSelection
            ? "Targets were filled from selected servers. You can edit them before running."
            : "No servers selected — enter targets and commands manually.";

        foreach (var target in initialTargets
                     .Select(t => t.Trim())
                     .Where(t => t.Length > 0)
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Targets.Add(new TargetEntry { Value = target });
        }

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

    private void AddTarget_Click(object sender, RoutedEventArgs e)
    {
        Targets.Add(new TargetEntry());
        TargetsList.SelectedIndex = Targets.Count - 1;
        TargetsList.ScrollIntoView(TargetsList.SelectedItem);
    }

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (TargetsList.SelectedItem is TargetEntry entry)
        {
            Targets.Remove(entry);
            return;
        }

        if (Targets.Count > 0)
            Targets.RemoveAt(Targets.Count - 1);
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

        var targets = Targets
            .Select(t => t.Value.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (targets.Count == 0)
        {
            DialogService.ShowWarning("Add at least one target IP or hostname.", "Validation");
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
