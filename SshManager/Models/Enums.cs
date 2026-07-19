namespace SshManager.Models;

public enum ConnectionType
{
    Ssh,
    Telnet
}

public enum ConnectionStatus
{
    Unknown,
    Testing,
    Online,
    Offline
}

public enum ExecutionStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Skipped
}
