namespace SshManager.Models;

public class AppData
{
    public AppSettings Settings { get; set; } = new();
    public List<ServerGroup> Groups { get; set; } = new();
    public List<ServerProfile> Servers { get; set; } = new();
}
