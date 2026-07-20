using SshManager.Models;

namespace SshManager.Services;

public static class InteractiveStepExpander
{
    public static IReadOnlyList<BatchStep> Expand(BatchStep step)
    {
        if (step.Type != BatchStepType.Command)
            return new[] { step };

        return ExpandCommandText(step.Text);
    }

    public static List<BatchStep> ExpandCommandText(string text)
    {
        var steps = new List<BatchStep>();
        if (string.IsNullOrWhiteSpace(text))
            return steps;

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var rawLine in lines)
        {
            var token = rawLine.Trim();
            if (token.Length == 0)
                continue;

            if (token.Equals("<enter>", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new BatchStep { Type = BatchStepType.Enter });
                continue;
            }

            if (token.Equals("<password>", StringComparison.OrdinalIgnoreCase))
            {
                steps.Add(new BatchStep { Type = BatchStepType.Password });
                continue;
            }

            steps.Add(new BatchStep { Type = BatchStepType.Command, Text = token });
        }

        return steps;
    }
}
