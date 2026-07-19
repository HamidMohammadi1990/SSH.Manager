namespace SshManager.Models;

public class CommandItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;
    public int Order { get; set; }
}
