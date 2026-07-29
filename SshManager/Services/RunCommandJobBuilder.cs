using SshManager.Models;
using SshManager.Views;

namespace SshManager.Services;

public static class RunCommandJobBuilder
{
    public static List<string> ParseTargets(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static BatchJob Build(RunCommandDialog dialog)
    {
        var targets = ParseTargets(dialog.TargetsText);

        var connectionType = dialog.ConnectionType;

        return new BatchJob
        {
            Credential = new BatchCredential
            {
                Username = dialog.Username,
                Password = dialog.Password,
                EnablePassword = dialog.EnablePassword
            },
            Targets = targets,
            Steps =
            [
                new BatchStep
                {
                    Type = BatchStepType.Command,
                    Text = dialog.CommandsText
                }
            ],
            Defaults = new BatchDefaults
            {
                ConnectionType = connectionType,
                Port = connectionType == ConnectionType.Ssh ? 22 : 23
            },
            SourceFile = "(Run Command)"
        };
    }
}
