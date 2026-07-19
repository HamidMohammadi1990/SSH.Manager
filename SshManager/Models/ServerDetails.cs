namespace SshManager.Models;

public class ServerDetailsReport
{
    public string ServerName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime CollectedAt { get; set; } = DateTime.Now;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }

    public string Hostname { get; set; } = "—";
    public string OperatingSystem { get; set; } = "—";
    public string OsVersion { get; set; } = "—";
    public string Kernel { get; set; } = "—";
    public string Architecture { get; set; } = "—";
    public string Uptime { get; set; } = "—";
    public string CurrentUser { get; set; } = "—";
    public string Platform { get; set; } = "—";

    public string CpuModel { get; set; } = "—";
    public int CpuCores { get; set; }
    public double? CpuUsagePercent { get; set; }
    public string LoadAverage { get; set; } = "—";

    public long MemoryTotalBytes { get; set; }
    public long MemoryUsedBytes { get; set; }
    public double MemoryUsagePercent => MemoryTotalBytes > 0
        ? Math.Round(MemoryUsedBytes * 100.0 / MemoryTotalBytes, 1)
        : 0;

    public List<DiskVolumeInfo> Disks { get; set; } = new();
    public List<DetailSection> Sections { get; set; } = new();

    public int CommandCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DiskVolumeInfo
{
    public string Name { get; set; } = string.Empty;
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long UsedBytes { get; set; }

    public double UsagePercent => TotalBytes > 0
        ? Math.Round(UsedBytes * 100.0 / TotalBytes, 1)
        : 0;
}

public class DetailSection
{
    public string Title { get; set; } = string.Empty;
    public List<DetailItem> Items { get; set; } = new();
}

public class DetailItem
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
