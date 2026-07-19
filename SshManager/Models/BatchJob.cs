namespace SshManager.Models;

public class BatchJob
{
    public BatchCredential Credential { get; set; } = new();
    public BatchDefaults Defaults { get; set; } = new();
    public List<string> Targets { get; set; } = new();
    public List<BatchStep> Steps { get; set; } = new();
    public string? SourceFile { get; set; }

    public string Summary =>
        $"{Targets.Count} target(s), {Steps.Count} step(s), {Defaults.ConnectionType} port {Defaults.Port}";
}

public class BatchCredential
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Used for &lt;password&gt; steps. Falls back to Password when empty.</summary>
    public string EnablePassword { get; set; } = string.Empty;

    public string PasswordForStep => string.IsNullOrEmpty(EnablePassword) ? Password : EnablePassword;
}

public class BatchDefaults
{
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Telnet;
    public int Port { get; set; } = 23;
    public int StepDelayMs { get; set; } = 500;
}

public enum BatchStepType
{
    Command,
    Enter,
    Password
}

public class BatchStep
{
    public BatchStepType Type { get; set; }
    public string Text { get; set; } = string.Empty;

    public string DisplayText => Type switch
    {
        BatchStepType.Enter => "<enter>",
        BatchStepType.Password => "<password>",
        _ => Text
    };
}
