namespace SshManager.Models;

public class AppSettings
{
    public string DefaultUsername { get; set; } = string.Empty;
    public string? DefaultPasswordEncrypted { get; set; }
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public int CommandTimeoutSeconds { get; set; } = 60;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
}
