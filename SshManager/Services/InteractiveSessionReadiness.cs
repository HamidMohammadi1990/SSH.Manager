using SshManager.Models;

namespace SshManager.Services;

public static class InteractiveSessionReadiness
{
  private const int PromptSettleMs = 100;
  private const int MinQuietMs = 35;

  public static int ResolveRequiredIdleMs(string output, BatchStepType sentStepType, int baseIdleMs)
  {
    var tail = GetTail(output, 768);

    if (sentStepType == BatchStepType.Password)
    {
      if (IsPasswordPrompt(tail) && !HasInteractivePrompt(tail))
        return baseIdleMs;

      if (HasInteractivePrompt(tail))
        return PromptSettleMs;

      return baseIdleMs;
    }

    if (IsPasswordPrompt(tail) && !HasInteractivePrompt(tail))
      return baseIdleMs;

    if (HasInteractivePrompt(tail))
      return PromptSettleMs;

    return baseIdleMs;
  }

  public static bool IsReadyToSend(string output)
  {
    var tail = GetTail(output, 768);
    if (HasInteractivePrompt(tail))
      return true;

    if (IsPasswordPrompt(tail))
      return true;

    return IsLoginPrompt(tail);
  }

  public static bool HasInteractivePrompt(string text)
  {
    foreach (var line in GetLastNonEmptyLines(text, 4))
    {
      var trimmed = line.TrimEnd('\r', '\n', ' ', '\t', '\0');
      if (trimmed.Length == 0)
        continue;

      if (trimmed.EndsWith('#') || trimmed.EndsWith('>'))
        return true;

      if (trimmed.EndsWith('$'))
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

  public static bool ShouldBreakRead(
    string output,
    BatchStepType sentStepType,
    double idleMs,
    int baseIdleMs)
  {
    if (idleMs < MinQuietMs)
      return false;

    var requiredIdle = ResolveRequiredIdleMs(output, sentStepType, baseIdleMs);
    return idleMs >= requiredIdle;
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
