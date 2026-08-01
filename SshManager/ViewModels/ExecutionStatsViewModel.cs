using CommunityToolkit.Mvvm.ComponentModel;
using SshManager.Models;

namespace SshManager.ViewModels;

public partial class ExecutionStatsViewModel : ObservableObject
{
    [ObservableProperty] private double _totalSeconds;
    [ObservableProperty] private int _successServers;
    [ObservableProperty] private int _failedServers;
    [ObservableProperty] private int _skippedServers;
    [ObservableProperty] private int _successCommands;
    [ObservableProperty] private int _failedCommands;
    [ObservableProperty] private int _skippedCommands;
    [ObservableProperty] private int _totalCommands;
    [ObservableProperty] private int _totalServers;
    [ObservableProperty] private double _serverSuccessRate;
    [ObservableProperty] private double _commandSuccessRate;

    public static ExecutionStatsViewModel FromSession(ExecutionSession session)
    {
        var skippedServers = session.Servers.Count(s => s.Status == ExecutionStatus.Skipped);
        var skippedCommands = session.Servers.Sum(s => s.Commands.Count(c => c.Status == ExecutionStatus.Skipped));
        var totalServers = session.Servers.Count;
        var totalCommands = session.TotalCommands;
        var successServers = session.SuccessCount;
        var failedServers = session.FailedCount;
        var successCommands = session.SuccessfulCommands;
        var failedCommands = session.FailedCommands;

        return new ExecutionStatsViewModel
        {
            TotalSeconds = session.TotalDuration.TotalSeconds,
            TotalServers = totalServers,
            SuccessServers = successServers,
            FailedServers = failedServers,
            SkippedServers = skippedServers,
            TotalCommands = totalCommands,
            SuccessCommands = successCommands,
            FailedCommands = failedCommands,
            SkippedCommands = skippedCommands,
            ServerSuccessRate = totalServers > 0 ? successServers * 100.0 / totalServers : 0,
            CommandSuccessRate = totalCommands > 0 ? successCommands * 100.0 / totalCommands : 0
        };
    }
}
