using System.IO;
using System.Text;
using SshManager.Models;

namespace SshManager.Services;

public static class ServerProfileFileParser
{
    private static readonly HashSet<string> KnownSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "server", "credential", "targets", "steps"
    };

    public static ServerProfileFile Parse(string content, string? sourceFile = null)
    {
        var profile = new ServerProfileFile
        {
            SourceFile = sourceFile,
            Port = 0
        };

        if (!string.IsNullOrWhiteSpace(sourceFile))
            profile.ServerName = Path.GetFileNameWithoutExtension(sourceFile);

        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string? section = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith('@') && line.Length > 1)
            {
                var sectionName = line[1..].Trim();
                if (KnownSections.Contains(sectionName))
                {
                    section = sectionName.ToLowerInvariant();
                    continue;
                }

                throw new FormatException($"Unknown section '@{sectionName}'.");
            }

            switch (section)
            {
                case "server":
                    ParseServerLine(line, profile);
                    break;
                case "credential":
                    ParseCredentialLine(line, profile);
                    break;
                case "targets":
                    ParseTargetLine(line, profile.Targets);
                    break;
                case "steps":
                    ParseStepLine(line, profile.Steps);
                    break;
                default:
                    throw new FormatException(
                        "Content found outside a section. Start with @server, @credential, @targets, or @steps.");
            }
        }

        Validate(profile);
        return profile;
    }

    public static ServerProfileFile ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Parse(content, filePath);
    }

    private static void ParseServerLine(string line, ServerProfileFile profile)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0)
            throw new FormatException($"Invalid @server line: '{line}'");

        var key = line[..eq].Trim().ToLowerInvariant();
        var value = line[(eq + 1)..].Trim();

        switch (key)
        {
            case "ip":
            case "host":
            case "address":
                profile.Host = value;
                break;
            case "port":
                if (!int.TryParse(value, out var port) || port < 1 || port > 65535)
                    throw new FormatException($"Invalid port: '{value}'");
                profile.Port = port;
                break;
            case "type":
            case "connectiontype":
                profile.ConnectionType = ParseConnectionType(value);
                break;
            case "description":
            case "desc":
                profile.Description = value;
                break;
            default:
                throw new FormatException($"Unknown @server key: '{key}'");
        }
    }

    private static void ParseCredentialLine(string line, ServerProfileFile profile)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0)
            throw new FormatException($"Invalid @credential line: '{line}'");

        var key = line[..eq].Trim().ToLowerInvariant();
        var value = line[(eq + 1)..].Trim();

        switch (key)
        {
            case "user.name":
            case "username":
            case "user":
                profile.Username = value;
                break;
            case "user.password":
            case "password":
                profile.Password = value;
                break;
            default:
                throw new FormatException($"Unknown @credential key: '{key}'");
        }
    }

    private static void ParseTargetLine(string line, List<string> targets)
    {
        var host = line.Trim();
        if (host.Length == 0)
            return;

        if (!targets.Contains(host, StringComparer.OrdinalIgnoreCase))
            targets.Add(host);
    }

    private static void ParseStepLine(string line, List<string> steps)
    {
        var step = line.Trim();
        if (step.Length == 0)
            return;

        steps.Add(step);
    }

    private static ConnectionType ParseConnectionType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "s" or "ssh" => ConnectionType.Ssh,
            "t" or "telnet" => ConnectionType.Telnet,
            _ => throw new FormatException($"Unknown connection type: '{value}'. Use s/ssh or t/telnet.")
        };
    }

    private static void Validate(ServerProfileFile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.ServerName))
            throw new FormatException("Server name could not be determined from the file name.");

        if (string.IsNullOrWhiteSpace(profile.Host))
        {
            if (profile.Targets.Count == 0)
                throw new FormatException("No host defined. Set ip= under @server or add a host under @targets.");

            profile.Host = profile.Targets[0];
        }

        if (profile.Port <= 0)
            profile.Port = profile.ConnectionType == ConnectionType.Telnet ? 23 : 22;
    }
}
