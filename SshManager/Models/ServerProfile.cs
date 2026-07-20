namespace SshManager.Models;

public class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public ConnectionType ConnectionType { get; set; } = ConnectionType.Ssh;
    public string? GroupId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int Order { get; set; }
    public bool UseCustomCredentials { get; set; }
    public string? CustomUsername { get; set; }
    public string? CustomPasswordEncrypted { get; set; }
    public string? PrivateKeyPath { get; set; }
    public List<string> Targets { get; set; } = new();
    public List<CommandItem> Commands { get; set; } = new();
}
