namespace SshManager.Models;

public class ServerProfileFile
{
    public string ServerName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Ssh;
    public string Description { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Targets { get; set; } = new();
    public List<string> Steps { get; set; } = new();
    public string? SourceFile { get; set; }

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password);
}
