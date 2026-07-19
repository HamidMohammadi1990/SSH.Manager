using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class ServerDetailsService
{
    public async Task<ServerDetailsReport> CollectAsync(
        ServerProfile server,
        AppSettings settings,
        string groupName,
        int commandCount,
        CancellationToken ct = default)
    {
        var report = new ServerDetailsReport
        {
            ServerName = server.Name,
            Host = server.Host,
            Port = server.Port,
            ConnectionType = server.ConnectionType,
            GroupName = groupName,
            Description = server.Description,
            CreatedAt = server.CreatedAt,
            CommandCount = commandCount,
            CollectedAt = DateTime.Now
        };

        if (server.ConnectionType != ConnectionType.Ssh)
        {
            report.ErrorMessage = "Detailed system information is available over SSH. Switch this server to SSH to collect metrics.";
            PopulateAppSections(report, server);
            return report;
        }

        try
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                var (username, password, keyPath) = ConnectionTestService.ResolveCredentials(server, settings);

                using var client = ConnectionTestService.CreateSshClient(
                    server, username, password, keyPath, settings.ConnectionTimeoutSeconds);
                client.Connect();

                if (!client.IsConnected)
                    throw new InvalidOperationException("Could not connect via SSH.");

                var platform = Run(client, "uname -s 2>/dev/null || echo unknown", settings.CommandTimeoutSeconds, ct)
                    .Trim().ToLowerInvariant();

                if (platform.Contains("linux") || platform.Contains("darwin") || platform.Contains("unix"))
                    CollectLinux(client, report, settings, ct);
                else if (platform.Contains("windows") || platform.Contains("mingw") || platform.Contains("cygwin"))
                    CollectWindows(client, report, settings, ct);
                else
                    CollectGeneric(client, report, settings, ct);

                client.Disconnect();
            }, ct);

            PopulateAppSections(report, server);
            report.IsSuccess = string.IsNullOrEmpty(report.ErrorMessage);
        }
        catch (Exception ex)
        {
            report.ErrorMessage = ex.Message;
            report.IsSuccess = false;
            PopulateAppSections(report, server);
        }

        return report;
    }

    private static void CollectLinux(SshClient client, ServerDetailsReport report, AppSettings settings, CancellationToken ct)
    {
        report.Platform = "Linux / Unix";

        report.Hostname = FirstLine(Run(client, "hostname -f 2>/dev/null || hostname", settings.CommandTimeoutSeconds, ct));
        report.CurrentUser = FirstLine(Run(client, "whoami", settings.CommandTimeoutSeconds, ct));
        report.Uptime = FirstLine(Run(client, "uptime -p 2>/dev/null || uptime", settings.CommandTimeoutSeconds, ct));
        report.LoadAverage = ExtractLoadAverage(Run(client, "uptime", settings.CommandTimeoutSeconds, ct));

        var osRelease = Run(client, "cat /etc/os-release 2>/dev/null", settings.CommandTimeoutSeconds, ct);
        ParseOsRelease(osRelease, report);

        var uname = Run(client, "uname -srvmo", settings.CommandTimeoutSeconds, ct);
        if (!string.IsNullOrWhiteSpace(uname))
            report.Kernel = uname.Trim();

        var arch = FirstLine(Run(client, "uname -m", settings.CommandTimeoutSeconds, ct));
        if (!string.IsNullOrWhiteSpace(arch))
            report.Architecture = arch;

        var cpuInfo = Run(client, "lscpu 2>/dev/null", settings.CommandTimeoutSeconds, ct);
        ParseLscpu(cpuInfo, report);

        var coresText = FirstLine(Run(client, "nproc 2>/dev/null || getconf _NPROCESSORS_ONLN 2>/dev/null", settings.CommandTimeoutSeconds, ct));
        if (int.TryParse(coresText, out var cores) && cores > 0)
            report.CpuCores = cores;

        var freeOutput = Run(client, "free -b 2>/dev/null | awk 'NR==2{print $2,$3,$4}'", settings.CommandTimeoutSeconds, ct);
        ParseFree(freeOutput, report);

        var cpuScript = Run(client,
            @"sh -c 'read _ u1 n1 s1 i1 iw1 irq1 sirq1 _ < /proc/stat; idle1=i1+iw1; total1=u1+n1+s1+idle1+irq1+sirq1; sleep 1; read _ u2 n2 s2 i2 iw2 irq2 sirq2 _ < /proc/stat; idle2=i2+iw2; total2=u2+n2+s2+idle2+irq2+sirq2; dt=total2-total1; di=idle2-idle1; if [ $dt -gt 0 ]; then awk -v u=$dt -v i=$di ""BEGIN{printf ""%.1f"", (u-i)*100/u}""; fi'",
            settings.CommandTimeoutSeconds, ct);
        if (double.TryParse(cpuScript.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var cpuPct))
            report.CpuUsagePercent = cpuPct;

        var dfOutput = Run(client, "df -B1 -P 2>/dev/null | tail -n +2", settings.CommandTimeoutSeconds, ct);
        ParseDf(dfOutput, report);

        var network = Run(client, "ip -br addr 2>/dev/null || ifconfig 2>/dev/null | head -20", settings.CommandTimeoutSeconds, ct);
        if (!string.IsNullOrWhiteSpace(network))
        {
            report.Sections.Add(new DetailSection
            {
                Title = "Network",
                Items = network.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Take(12)
                    .Select((line, i) => new DetailItem { Label = $"Interface {i + 1}", Value = line })
                    .ToList()
            });
        }
    }

    private static void CollectWindows(SshClient client, ServerDetailsReport report, AppSettings settings, CancellationToken ct)
    {
        report.Platform = "Windows";

        var script = """
            powershell -NoProfile -ExecutionPolicy Bypass -Command "
            $os = Get-CimInstance Win32_OperatingSystem;
            $cs = Get-CimInstance Win32_ComputerSystem;
            $cpu = Get-CimInstance Win32_Processor | Select-Object -First 1;
            $disks = Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3';
            $data = [ordered]@{
              Hostname = $env:COMPUTERNAME;
              User = $env:USERNAME;
              OS = $os.Caption;
              OSVersion = $os.Version;
              Architecture = $os.OSArchitecture;
              Uptime = ((Get-Date) - $os.LastBootUpTime).ToString();
              TotalRAM = [int64]$cs.TotalPhysicalMemory;
              FreeRAM = [int64]$os.FreePhysicalMemory * 1024;
              CpuModel = $cpu.Name;
              CpuCores = $cpu.NumberOfLogicalProcessors;
              CpuLoad = $cpu.LoadPercentage;
              Disks = @($disks | ForEach-Object { [ordered]@{ Name=$_.DeviceID; Total=[int64]$_.Size; Free=[int64]$_.FreeSpace; FileSystem=$_.FileSystem } })
            };
            $data | ConvertTo-Json -Compress
            "
            """;

        var json = Run(client, script, Math.Max(settings.CommandTimeoutSeconds, 45), ct);
        ParseWindowsJson(json, report);
    }

    private static void CollectGeneric(SshClient client, ServerDetailsReport report, AppSettings settings, CancellationToken ct)
    {
        report.Platform = "Unknown";
        report.Hostname = FirstLine(Run(client, "hostname 2>/dev/null", settings.CommandTimeoutSeconds, ct));
        report.OperatingSystem = FirstLine(Run(client, "uname -a 2>/dev/null", settings.CommandTimeoutSeconds, ct));
        report.CurrentUser = FirstLine(Run(client, "whoami 2>/dev/null", settings.CommandTimeoutSeconds, ct));
    }

    private static void ParseWindowsJson(string json, ServerDetailsReport report)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            report.Hostname = GetJsonString(root, "Hostname");
            report.CurrentUser = GetJsonString(root, "User");
            report.OperatingSystem = GetJsonString(root, "OS");
            report.OsVersion = GetJsonString(root, "OSVersion");
            report.Architecture = GetJsonString(root, "Architecture");
            report.Uptime = GetJsonString(root, "Uptime");
            report.CpuModel = GetJsonString(root, "CpuModel");
            report.CpuCores = root.TryGetProperty("CpuCores", out var cores) ? cores.GetInt32() : 0;

            if (root.TryGetProperty("CpuLoad", out var load) && load.ValueKind == JsonValueKind.Number)
                report.CpuUsagePercent = load.GetDouble();

            if (root.TryGetProperty("TotalRAM", out var totalRam) && totalRam.TryGetInt64(out var tr))
                report.MemoryTotalBytes = tr;

            if (root.TryGetProperty("FreeRAM", out var freeRam) && freeRam.TryGetInt64(out var fr))
                report.MemoryUsedBytes = Math.Max(0, report.MemoryTotalBytes - fr);

            if (root.TryGetProperty("Disks", out var disks) && disks.ValueKind == JsonValueKind.Array)
            {
                foreach (var disk in disks.EnumerateArray())
                {
                    var total = disk.TryGetProperty("Total", out var t) && t.TryGetInt64(out var tv) ? tv : 0;
                    var free = disk.TryGetProperty("Free", out var f) && f.TryGetInt64(out var fv) ? fv : 0;
                    report.Disks.Add(new DiskVolumeInfo
                    {
                        Name = GetJsonString(disk, "Name"),
                        MountPoint = GetJsonString(disk, "Name"),
                        FileSystem = GetJsonString(disk, "FileSystem"),
                        TotalBytes = total,
                        UsedBytes = Math.Max(0, total - free)
                    });
                }
            }
        }
        catch
        {
            report.Sections.Add(new DetailSection
            {
                Title = "Raw Output",
                Items = [new DetailItem { Label = "Response", Value = json.Trim() }]
            });
        }
    }

    private static void ParseOsRelease(string content, ServerDetailsReport report)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var values = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim().Trim('"'));

        if (values.TryGetValue("PRETTY_NAME", out var pretty))
            report.OperatingSystem = pretty;
        else if (values.TryGetValue("NAME", out var name))
            report.OperatingSystem = name;

        if (values.TryGetValue("VERSION_ID", out var version))
            report.OsVersion = version;
        else if (values.TryGetValue("VERSION", out var ver))
            report.OsVersion = ver;
    }

    private static void ParseLscpu(string content, ServerDetailsReport report)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        foreach (var line in content.Split('\n'))
        {
            var parts = line.Split(':', 2);
            if (parts.Length != 2) continue;

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            switch (key)
            {
                case "Model name":
                    report.CpuModel = value;
                    break;
                case "CPU(s)":
                    if (int.TryParse(value, out var count))
                        report.CpuCores = count;
                    break;
                case "Architecture":
                    report.Architecture = value;
                    break;
            }
        }
    }

    private static void ParseFree(string content, ServerDetailsReport report)
    {
        var parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return;

        if (long.TryParse(parts[0], out var total))
            report.MemoryTotalBytes = total;
        if (long.TryParse(parts[1], out var used))
            report.MemoryUsedBytes = used;
    }

    private static void ParseDf(string content, ServerDetailsReport report)
    {
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6) continue;

            if (!long.TryParse(parts[1], out var total) || total <= 0) continue;
            if (!long.TryParse(parts[2], out var used)) continue;

            report.Disks.Add(new DiskVolumeInfo
            {
                Name = parts[0],
                FileSystem = parts[0].StartsWith('/') ? "disk" : parts[0],
                MountPoint = parts[5],
                TotalBytes = total,
                UsedBytes = used
            });
        }
    }

    private static void PopulateAppSections(ServerDetailsReport report, ServerProfile server)
    {
        report.Sections.Insert(0, new DetailSection
        {
            Title = "Application Profile",
            Items =
            [
                new DetailItem { Label = "Display Name", Value = server.Name },
                new DetailItem { Label = "Connection", Value = $"{server.ConnectionType} : {server.Port}" },
                new DetailItem { Label = "Group", Value = report.GroupName },
                new DetailItem { Label = "Saved Commands", Value = report.CommandCount.ToString() },
                new DetailItem { Label = "Created", Value = report.CreatedAt.ToString("yyyy-MM-dd HH:mm") },
                new DetailItem { Label = "Custom Credentials", Value = server.UseCustomCredentials ? "Yes" : "No (default)" }
            ]
        });

        if (!string.IsNullOrWhiteSpace(report.Description))
        {
            report.Sections[0].Items.Add(new DetailItem
            {
                Label = "Description",
                Value = report.Description
            });
        }
    }

    private static string Run(SshClient client, string command, int timeoutSeconds, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var cmd = client.CreateCommand(command);
        cmd.CommandTimeout = TimeSpan.FromSeconds(timeoutSeconds);
        var output = cmd.Execute() ?? string.Empty;
        if (!string.IsNullOrEmpty(cmd.Error))
            output += Environment.NewLine + cmd.Error;
        return output;
    }

    private static string FirstLine(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "—";

    private static string ExtractLoadAverage(string uptime)
    {
        var match = Regex.Match(uptime, @"load average:\s*(.+)$", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : "—";
    }

    private static string GetJsonString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.ToString() : "—";
}
