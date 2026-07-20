using Renci.SshNet;

namespace SshManager.Services;

/// <summary>
/// Tunes SSH.NET negotiation for legacy network devices (Cisco, etc.)
/// that only support older key exchange, host key, and cipher algorithms.
/// </summary>
public static class SshCompatibilityConfigurer
{
    private static readonly string[] PreferredKeyExchange =
    {
        "diffie-hellman-group-exchange-sha1",
        "diffie-hellman-group14-sha1",
        "diffie-hellman-group1-sha1",
        "diffie-hellman-group-exchange-sha256",
        "diffie-hellman-group14-sha256",
        "diffie-hellman-group16-sha512",
        "ecdh-sha2-nistp256",
        "ecdh-sha2-nistp384",
        "ecdh-sha2-nistp521",
        "curve25519-sha256",
        "curve25519-sha256@libssh.org"
    };

    private static readonly string[] PreferredHostKeys =
    {
        "ssh-rsa",
        "rsa-sha2-256",
        "rsa-sha2-512",
        "ecdsa-sha2-nistp256",
        "ecdsa-sha2-nistp384",
        "ecdsa-sha2-nistp521",
        "ssh-ed25519"
    };

    private static readonly string[] PreferredEncryptions =
    {
        "aes128-cbc",
        "aes192-cbc",
        "aes256-cbc",
        "3des-cbc",
        "aes128-ctr",
        "aes192-ctr",
        "aes256-ctr"
    };

    private static readonly string[] PreferredHmac =
    {
        "hmac-sha1",
        "hmac-sha1-etm@openssh.com",
        "hmac-sha2-256",
        "hmac-sha2-512",
        "hmac-sha2-256-etm@openssh.com",
        "hmac-sha2-512-etm@openssh.com"
    };

    public static void ConfigureConnection(ConnectionInfo connectionInfo)
    {
        Prioritize(connectionInfo.KeyExchangeAlgorithms, PreferredKeyExchange);
        Prioritize(connectionInfo.HostKeyAlgorithms, PreferredHostKeys);
        Prioritize(connectionInfo.Encryptions, PreferredEncryptions);
        Prioritize(connectionInfo.HmacAlgorithms, PreferredHmac);

        if (connectionInfo.CompressionAlgorithms.ContainsKey("none"))
            connectionInfo.CompressionAlgorithms.SetPosition("none", 0);
    }

    public static void ConfigureClient(SshClient client)
    {
        client.HostKeyReceived += (_, e) => e.CanTrust = true;
    }

    private static void Prioritize<T>(IOrderedDictionary<string, T> algorithms, IReadOnlyList<string> preferredOrder)
    {
        var position = 0;
        foreach (var name in preferredOrder)
        {
            if (!algorithms.ContainsKey(name))
                continue;

            algorithms.SetPosition(name, position++);
        }
    }
}
