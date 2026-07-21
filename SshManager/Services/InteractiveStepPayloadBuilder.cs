using System.Text;
using SshManager.Models;

namespace SshManager.Services;

public static class InteractiveStepPayloadBuilder
{
    public static string Build(IReadOnlyList<BatchStep> steps, BatchCredential credential)
    {
        if (steps.Count == 0)
            return string.Empty;

        var payload = new StringBuilder();
        foreach (var step in steps)
            payload.Append(ResolvePart(step, credential));

        return payload.ToString();
    }

    public static string Build(BatchStep step, BatchCredential credential) =>
        Build(InteractiveStepExpander.Expand(step), credential);

    private static string ResolvePart(BatchStep step, BatchCredential credential) =>
        step.Type switch
        {
            BatchStepType.Enter => "\r\n",
            BatchStepType.Password => credential.PasswordForStep + "\r\n",
            BatchStepType.Command => step.Text + "\r\n",
            _ => "\r\n"
        };
}
