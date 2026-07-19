using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SshManager.Views;

public enum DialogKind
{
    Information,
    Warning,
    Error,
    Question
}

public enum DialogButtons
{
    Ok,
    YesNo,
    YesNoCancel
}

public partial class AppDialog : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;

    private AppDialog()
    {
        InitializeComponent();
    }

    public static MessageBoxResult Show(
        string title,
        string message,
        DialogKind kind,
        DialogButtons buttons,
        Window? owner = null)
    {
        var dialog = new AppDialog();
        dialog.Configure(title, message, kind, buttons);

        if (owner is { IsLoaded: true })
            dialog.Owner = owner;

        dialog.ShowDialog();
        return dialog._result;
    }

    private void Configure(string title, string message, DialogKind kind, DialogButtons buttons)
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;

        var (accent, iconBg, iconFg, icon) = kind switch
        {
            DialogKind.Warning => ("#FFC107", "#33FFC107", "#FFC107", "!"),
            DialogKind.Error => ("#F44336", "#33F44336", "#F44336", "✕"),
            DialogKind.Question => ("#7C4DFF", "#337C4DFF", "#B388FF", "?"),
            _ => ("#7C4DFF", "#337C4DFF", "#B388FF", "i")
        };

        AccentBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accent)!);
        IconBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconBg)!);
        IconText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(iconFg)!);
        IconText.Text = icon;

        BuildButtons(buttons, kind);
        KeyDown += OnKeyDown;
    }

    private void BuildButtons(DialogButtons buttons, DialogKind kind)
    {
        ButtonPanel.Children.Clear();

        switch (buttons)
        {
            case DialogButtons.Ok:
                AddButton("OK", MessageBoxResult.OK, isPrimary: true, isDanger: false, isDefault: true);
                break;

            case DialogButtons.YesNo:
                AddButton("No", MessageBoxResult.No, isPrimary: false, isDanger: false);
                AddButton("Yes", MessageBoxResult.Yes, isPrimary: true, isDanger: kind == DialogKind.Warning, isDefault: true);
                break;

            case DialogButtons.YesNoCancel:
                AddButton("Cancel", MessageBoxResult.Cancel, isPrimary: false, isDanger: false);
                AddButton("No", MessageBoxResult.No, isPrimary: false, isDanger: false);
                AddButton("Yes", MessageBoxResult.Yes, isPrimary: true, isDanger: false, isDefault: true);
                break;
        }
    }

    private void AddButton(string text, MessageBoxResult result, bool isPrimary, bool isDanger, bool isDefault = false)
    {
        var styleKey = isDanger ? "DangerButton" : isPrimary ? "PrimaryButton" : "SecondaryButton";
        var button = new Button
        {
            Content = text,
            MinWidth = 88,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource(styleKey),
            IsDefault = isDefault
        };

        if (isDanger)
        {
            button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336")!);
            button.Foreground = Brushes.White;
        }

        button.Click += (_, _) =>
        {
            _result = result;
            Close();
        };

        ButtonPanel.Children.Add(button);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _result = MessageBoxResult.Cancel;
            Close();
        }
    }
}
