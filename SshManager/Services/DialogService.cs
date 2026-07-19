using System.Windows;
using SshManager.Views;

namespace SshManager.Services;

public static class DialogService
{
    private static Window? Owner => Application.Current.MainWindow;

    public static void ShowInfo(string message, string title = "Information")
    {
        AppDialog.Show(title, message, DialogKind.Information, DialogButtons.Ok, Owner);
    }

    public static void ShowWarning(string message, string title = "Warning")
    {
        AppDialog.Show(title, message, DialogKind.Warning, DialogButtons.Ok, Owner);
    }

    public static void ShowError(string message, string title = "Error")
    {
        AppDialog.Show(title, message, DialogKind.Error, DialogButtons.Ok, Owner);
    }

    public static MessageBoxResult ShowYesNo(string message, string title, DialogKind kind = DialogKind.Question)
    {
        return AppDialog.Show(title, message, kind, DialogButtons.YesNo, Owner);
    }

    public static MessageBoxResult ShowYesNoCancel(string message, string title)
    {
        return AppDialog.Show(title, message, DialogKind.Question, DialogButtons.YesNoCancel, Owner);
    }
}
