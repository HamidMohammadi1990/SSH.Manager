using System.Windows;
using SshManager.Services;

namespace SshManager.Views;

public partial class GroupDialog : Window
{
    public string GroupName { get; private set; } = string.Empty;

    public GroupDialog()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            TitleText.Text = Title;
            NameBox.Text = GroupName;
            NameBox.SelectAll();
            NameBox.Focus();
        };
    }

    public static bool TryPrompt(Window owner, string title, string buttonText, string defaultName, out string groupName)
    {
        var dialog = new GroupDialog
        {
            Owner = owner,
            Title = title,
            GroupName = defaultName
        };
        dialog.SaveButton.Content = buttonText;

        if (dialog.ShowDialog() != true)
        {
            groupName = string.Empty;
            return false;
        }

        groupName = dialog.GroupName;
        return true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        GroupName = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(GroupName))
        {
            DialogService.ShowWarning("Group name is required.", "Validation");
            NameBox.Focus();
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
