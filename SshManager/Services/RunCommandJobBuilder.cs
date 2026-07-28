using SshManager.Models;
using SshManager.Views;

namespace SshManager.Services;

public static class RunCommandJobBuilder
{
    public static BatchJob Build(RunCommandDialog dialog)
    {
        var targets = dialog.Targets
            .Select(t => t.Value.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
