using System.IO;
using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class ConnectionTestService
{
    public async Task<bool> TestConnectionAsync(ServerProfile server, AppSettings settings, CancellationToken ct = default)
    {
        return server.ConnectionType switch
        {
            ConnectionType.Ssh => await TestSshAsync(server, settings, ct),
            ConnectionType.Telnet => await TestTelnetAsync(server, ct),
            _ => false
        };
    }

    private async Task<bool> TestSshAsync(ServerProfile server, AppSettings settings, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            try
            {
                var (username, password, keyPath) = ResolveCredentials(server, settings);
                if (string.IsNullOrWhiteSpace(username))
                    return false;

                using var client = CreateSshClient(server, username, password, keyPath, settings.ConnectionTimeoutSeconds);
                client.Connect();
                var connected = client.IsConnected;
                if (connected)
                    client.Disconnect();
                return connected;
            }
            catch
            {
                return false;
            }
        }, ct);
    }

    private async Task<bool> TestTelnetAsync(ServerProfile server, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(server.Host, server.Port, timeoutCts.Token);
            return client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static (string username, string password, string? keyPath) ResolveCredentials(
        ServerProfile server, AppSettings settings)
    {
        if (server.UseCustomCredentials)
        {
            var password = server.CustomPasswordEncrypted != null
                ? CredentialService.Decrypt(server.CustomPasswordEncrypted)
                : string.Empty;
            return (server.CustomUsername ?? string.Empty, password, server.PrivateKeyPath);
        }

        var defaultPassword = settings.DefaultPasswordEncrypted != null
            ? CredentialService.Decrypt(settings.DefaultPasswordEncrypted)
            : string.Empty;
        return (settings.DefaultUsername, defaultPassword, null);
    }

    public static SshClient CreateSshClient(
        ServerProfile server, string username, string password, string? keyPath, int timeoutSeconds)
    {
        var connectionInfo = !string.IsNullOrWhiteSpace(keyPath) && File.Exists(keyPath)
            ? new ConnectionInfo(server.Host, server.Port, username,
                new PrivateKeyAuthenticationMethod(username, new PrivateKeyFile(keyPath)))
            : new ConnectionInfo(server.Host, server.Port, username,
                new PasswordAuthenticationMethod(username, password));

        connectionInfo.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        return new SshClient(connectionInfo);
    }
}
