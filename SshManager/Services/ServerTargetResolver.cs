using SshManager.Models;

namespace SshManager.Services;

public static class ServerTargetResolver
{
    public static IReadOnlyList<string> ResolveEndpoints(ServerProfile server)
    {
        var targets = server.Targets?
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        if (targets.Count > 0)
            return targets;

        if (!string.IsNullOrWhiteSpace(server.Host))
            return new[] { server.Host.Trim() };

        return Array.Empty<string>();
    }

    public static ServerProfile CloneForTarget(ServerProfile server, string targetHost) =>
        new()
        {
            Id = server.Id,
            Name = server.Name,
            Host = targetHost,
            Port = server.Port,
            ConnectionType = server.ConnectionType,
            GroupId = server.GroupId,
            Description = server.Description,
            CreatedAt = server.CreatedAt,
            Order = server.Order,
            UseCustomCredentials = server.UseCustomCredentials,
            CustomUsername = server.CustomUsername,
            CustomPasswordEncrypted = server.CustomPasswordEncrypted,
            PrivateKeyPath = server.PrivateKeyPath,
            Targets = server.Targets.ToList(),
            Commands = server.Commands.Select(c => new CommandItem
            {
                Id = c.Id,
                Text = c.Text,
                Order = c.Order
            }).ToList()
        };

    public static string GetExecutionDisplayName(string serverName, string? targetHost) =>
        string.IsNullOrWhiteSpace(targetHost) ? serverName : $"{serverName} → {targetHost}";
}
