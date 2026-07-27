using System.Text;
using SshManager.Models;

namespace SshManager.Services;

public static class InteractiveStepPayloadBuilder
{
    /// <summary>
    /// Cisco SSH PTY treats \r\n as command + empty line. Use \r only for SSH.
    /// </summary>
    public static string LineEnding(ConnectionType connectionType) =>
        connectionType == ConnectionType.Ssh ? "\r" : "\r\n";

    public static string ResolvePart(BatchStep step, BatchCredential credential, ConnectionType connectionType)
    {
        var eol = LineEnding(connectionType);
        return step.Type switch
        {
            BatchStepType.Enter => eol,
            BatchStepType.Password => credential.PasswordForStep + eol,
            BatchStepType.Command => step.Text + eol,
            _ => eol
        };
    }

    public static string Build(IReadOnlyList<BatchStep> steps, BatchCredential credential, ConnectionType connectionType)
    {
        if (steps.Count == 0)
            return string.Empty;

        var payload = new StringBuilder();
        foreach (var step in steps)
            payload.Append(ResolvePart(step, credential, connectionType));

        return payload.ToString();
    }

    public static string Build(BatchStep step, BatchCredential credential, ConnectionType connectionType) =>
        Build(InteractiveStepExpander.Expand(step), credential, connectionType);
}
