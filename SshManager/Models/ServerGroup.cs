namespace SshManager.Models;

public class ServerGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
}
