using SshManager.Models;
using System.Text;

namespace SshManager.Services;

public static class InteractiveSessionReadiness
{
    private const int InputPromptSettleMs = 120;
    private const int ExecPromptSettleMs = 180;
    private const int MinQuietMs = 40;

    public static bool IsReadyToSend(string output)
    {
        var tail = GetTail(output, 1024);

        if (HasInteractiveInputPrompt(tail))
            return true;

        if (HasExecPrompt(tail))
            return true;

        if (IsPasswordPrompt(tail))
            return true;

        return IsLoginPrompt(tail);
    }

    public static bool ShouldBreakRead(
        string output,
        BatchStepType sentStepType,
        double idleMs,
        int baseIdleMs,
        bool receivedData)
    {
        if (!receivedData)
            return false;

        if (idleMs < MinQuietMs)
            return false;

        var tail = GetTail(output, 1024);

        if (HasInteractiveInputPrompt(tail))
            return idleMs >= InputPromptSettleMs;

        if (sentStepType == BatchStepType.Password)
        {
            if (IsPasswordPrompt(tail) && !HasExecPrompt(tail))
                return idleMs >= baseIdleMs;

            if (HasExecPrompt(tail))
                return idleMs >= ExecPromptSettleMs;

            return idleMs >= baseIdleMs;
        }

        if (IsPasswordPrompt(tail) && !HasExecPrompt(tail))
            return idleMs >= baseIdleMs;

        if (HasExecPrompt(tail))
            return idleMs >= baseIdleMs;

        return idleMs >= baseIdleMs;
    }

    /// <summary>
    /// Cisco-style multi-step prompts: "Address or name of remote host []?"
    /// </summary>
    public static bool HasInteractiveInputPrompt(string text)
    {
        foreach (var line in GetLastNonEmptyLines(text, 3))
        {
            var trimmed = line.TrimEnd('\r', '\n', ' ', '\t', '\0');
            if (trimmed.Length == 0)
                continue;

            if (trimmed.EndsWith('?'))
                return true;

            if (trimmed.Contains('[', StringComparison.Ordinal) &&
                trimmed.EndsWith(':', StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Exec/shell prompt only when # or > is the last character on the line
    /// (not command echo like "hostname#copy running-config tftp:").
    /// </summary>
    public static bool HasExecPrompt(string text)
    {
        foreach (var line in GetLastNonEmptyLines(text, 4))
        {
            if (IsExecPromptLine(line))
                return true;
        }

        return false;
    }

    public static bool IsPasswordPrompt(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        var tail = GetTail(text, 320);
        return tail.Contains("assword:", StringComparison.OrdinalIgnoreCase) ||
               tail.Contains("asswd:", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLoginPrompt(string text)
    {
        var tail = GetTail(text, 320);
        return tail.Contains("login:", StringComparison.OrdinalIgnoreCase) ||
               tail.Contains("username:", StringComparison.OrdinalIgnoreCase) ||
               tail.Contains("user name:", StringComparison.OrdinalIgnoreCase);
    }

    public static void AppendToSessionTail(StringBuilder sessionTail, string chunk, int maxChars = 2048)
    {
        if (string.IsNullOrEmpty(chunk))
            return;

        sessionTail.Append(chunk);
        if (sessionTail.Length <= maxChars)
            return;

        sessionTail.Remove(0, sessionTail.Length - maxChars);
    }

    private static bool IsExecPromptLine(string line)
    {
        var trimmed = line.TrimEnd('\r', '\n', ' ', '\t', '\0');
        if (trimmed.Length == 0)
            return false;

        var hashIndex = trimmed.LastIndexOf('#');
        var gtIndex = trimmed.LastIndexOf('>');
        var promptIndex = Math.Max(hashIndex, gtIndex);
        if (promptIndex < 0)
            return false;

        return trimmed[(promptIndex + 1)..].Trim().Length == 0;
    }

    private static string GetTail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];

    private static IEnumerable<string> GetLastNonEmptyLines(string text, int maxLines)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var collected = 0;
        for (var i = lines.Length - 1; i >= 0 && collected < maxLines; i--)
        {
            if (lines[i].Length == 0)
                continue;

            yield return lines[i];
            collected++;
        }
    }
}
