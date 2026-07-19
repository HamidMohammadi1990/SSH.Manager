using System.IO;
using System.Text;
using SshManager.Models;

namespace SshManager.Services;

public static class BatchJobParser
{
    private static readonly HashSet<string> KnownSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "credential", "defaults", "targets", "steps"
    };

    public static BatchJob Parse(string content, string? sourceFile = null)
    {
        var job = new BatchJob { SourceFile = sourceFile };
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
                case "credential":
                    ParseCredentialLine(line, job.Credential);
                    break;
                case "defaults":
                    ParseDefaultsLine(line, job.Defaults);
                    break;
                case "targets":
                    ParseTargetLine(line, job.Targets);
                    break;
                case "steps":
                    job.Steps.Add(ParseStepLine(line));
                    break;
                default:
                    throw new FormatException(
                        "Content found outside a section. Start with @credential, @targets, or @steps.");
            }
        }

        Validate(job);
        return job;
    }

    public static BatchJob ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        return Parse(content, filePath);
    }

    private static void ParseCredentialLine(string line, BatchCredential credential)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0)
            throw new FormatException($"Invalid credential line: '{line}'");

        var key = line[..eq].Trim().ToLowerInvariant();
        var value = line[(eq + 1)..].Trim();

        switch (key)
        {
            case "user.name":
            case "username":
            case "user":
                credential.Username = value;
                break;
            case "user.password":
            case "password":
                credential.Password = value;
                break;
            case "enable.password":
            case "enable":
                credential.EnablePassword = value;
                break;
            default:
                throw new FormatException($"Unknown credential key: '{key}'");
        }
    }

    private static void ParseDefaultsLine(string line, BatchDefaults defaults)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0)
            throw new FormatException($"Invalid defaults line: '{line}'");

        var key = line[..eq].Trim().ToLowerInvariant();
        var value = line[(eq + 1)..].Trim();

        switch (key)
        {
            case "type":
            case "connectiontype":
                defaults.ConnectionType = value.ToLowerInvariant() switch
                {
                    "ssh" => ConnectionType.Ssh,
                    "telnet" => ConnectionType.Telnet,
                    _ => throw new FormatException($"Unknown connection type: '{value}'")
                };
                break;
            case "port":
                if (!int.TryParse(value, out var port) || port < 1 || port > 65535)
                    throw new FormatException($"Invalid port: '{value}'");
                defaults.Port = port;
                break;
            case "delay":
            case "stepdelay":
            case "stepdelayms":
                if (!int.TryParse(value, out var delay) || delay < 0)
                    throw new FormatException($"Invalid step delay: '{value}'");
                defaults.StepDelayOverrideMs = delay;
                break;
            default:
                throw new FormatException($"Unknown defaults key: '{key}'");
        }
    }

    private static void ParseTargetLine(string line, List<string> targets)
    {
        var host = line.Trim();
        if (host.Length == 0)
            return;

        if (targets.Contains(host, StringComparer.OrdinalIgnoreCase))
            return;

        targets.Add(host);
    }

    private static BatchStep ParseStepLine(string line)
    {
        var token = line.Trim();
        if (token.Length == 0)
            throw new FormatException("Empty step line is not allowed. Use '<enter>' for a blank line.");

        if (token.Equals("<enter>", StringComparison.OrdinalIgnoreCase))
            return new BatchStep { Type = BatchStepType.Enter };

        if (token.Equals("<password>", StringComparison.OrdinalIgnoreCase))
            return new BatchStep { Type = BatchStepType.Password };

        return new BatchStep { Type = BatchStepType.Command, Text = token };
    }

    private static void Validate(BatchJob job)
    {
        if (job.Targets.Count == 0)
            throw new FormatException("No targets defined. Add at least one host under @targets.");

        if (job.Steps.Count == 0)
            throw new FormatException("No steps defined. Add at least one step under @steps.");

        if (job.Steps.Any(s => s.Type == BatchStepType.Password) &&
            string.IsNullOrEmpty(job.Credential.PasswordForStep))
            throw new FormatException("<password> step requires user.password or enable.password in @credential.");
    }
}
