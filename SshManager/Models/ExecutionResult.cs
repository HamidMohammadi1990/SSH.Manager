namespace SshManager.Models;

public class CommandExecutionResult
{
    public string CommandId { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }
}

public class ServerExecutionResult
{
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string TargetHost { get; set; } = string.Empty;
    public ExecutionStatus Status { get; set; }
    public TimeSpan Duration { get; set; }
    public List<CommandExecutionResult> Commands { get; set; } = new();
}

public class ExecutionSession
{
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public TimeSpan TotalDuration => FinishedAt.HasValue ? FinishedAt.Value - StartedAt : DateTime.Now - StartedAt;
    public List<ServerExecutionResult> Servers { get; set; } = new();
    public int SuccessCount => Servers.Count(s => s.Status == ExecutionStatus.Success);
    public int FailedCount => Servers.Count(s => s.Status == ExecutionStatus.Failed);
    public int TotalCommands => Servers.Sum(s => s.Commands.Count);
    public int SuccessfulCommands => Servers.Sum(s => s.Commands.Count(c => c.Status == ExecutionStatus.Success));
    public int FailedCommands => Servers.Sum(s => s.Commands.Count(c => c.Status == ExecutionStatus.Failed));
}
