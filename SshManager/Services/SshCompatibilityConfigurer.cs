using System.Collections.Generic;
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
        "ssh-ed25519",
        "ssh-dss"
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

    private static readonly string[] PreferredCompression = { "none" };

    public static void ConfigureConnection(ConnectionInfo connectionInfo)
    {
        PrioritizeDictionary(connectionInfo.KeyExchangeAlgorithms, PreferredKeyExchange);
        PrioritizeDictionary(connectionInfo.HostKeyAlgorithms, PreferredHostKeys);
        PrioritizeDictionary(connectionInfo.Encryptions, PreferredEncryptions);
        PrioritizeDictionary(connectionInfo.HmacAlgorithms, PreferredHmac);
        PrioritizeDictionary(connectionInfo.CompressionAlgorithms, PreferredCompression);
    }

    public static void ConfigureClient(SshClient client)
    {
        client.HostKeyReceived += (_, e) => e.CanTrust = true;
    }

  /// <summary>
  /// Reorders dictionary entries so preferred algorithms are negotiated first.
  /// Works with SSH.NET 2024.2 where collections are IDictionary, not IOrderedDictionary.
  /// </summary>
    private static void PrioritizeDictionary<T>(
        IDictionary<string, T> algorithms,
        IReadOnlyList<string> preferredOrder)
    {
        if (algorithms.Count == 0 || preferredOrder.Count == 0)
            return;

        var snapshot = new List<KeyValuePair<string, T>>(algorithms);
        algorithms.Clear();

        var added = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in preferredOrder)
        {
            var match = snapshot.Find(kvp => string.Equals(kvp.Key, name, StringComparison.Ordinal));
            if (match.Key is null)
                continue;

            algorithms[match.Key] = match.Value;
            added.Add(match.Key);
        }

        foreach (var kvp in snapshot)
        {
            if (added.Contains(kvp.Key))
                continue;

            algorithms[kvp.Key] = kvp.Value;
        }
    }
}
