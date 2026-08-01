using System.Diagnostics;
using System.IO;
using System.Windows;
using SshManager.Services;

namespace SshManager.Views;

public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => Title = ((ViewModels.HelpViewModel)DataContext).WindowTitle;
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ViewModels.HelpViewModel vm)
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ViewModels.HelpViewModel.WindowTitle))
                        Title = vm.WindowTitle;
                };
        };
    }

    private static string SamplesDirectory =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");

    private void OpenSampleFile(string fileName)
    {
        var path = Path.Combine(SamplesDirectory, fileName);
        if (!File.Exists(path))
        {
            DialogService.ShowWarning(
                $"Sample file not found:\n{path}\n\nReinstall or rebuild the application to restore sample files.",
                "Sample Not Found");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DialogService.ShowError($"Could not open sample file:\n{ex.Message}", "Open Failed");
        }
    }

    private void OpenBatchSample_Click(object sender, RoutedEventArgs e) =>
        OpenSampleFile("example.sshbatch");

    private void OpenServerSample_Click(object sender, RoutedEventArgs e) =>
        OpenSampleFile("example.sshserver");

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
