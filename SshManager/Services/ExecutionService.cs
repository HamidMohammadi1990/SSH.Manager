using SshManager.Models;

namespace SshManager.Services;

public class ExecutionService
{
    private readonly SshExecutor _sshExecutor = new();
    private readonly TelnetExecutor _telnetExecutor = new();

    public event Action<ServerExecutionResult>? ServerStarted;
    public event Action<ServerExecutionResult>? ServerCompleted;
    public event Action<CommandExecutionResult, string>? CommandStarted;
    public event Action<CommandExecutionResult, string>? CommandCompleted;
    public event Action<string>? OutputReceived;
    public event Action<ExecutionSession>? SessionCompleted;

    public async Task<ExecutionSession> ExecuteAllAsync(
        IEnumerable<ServerProfile> servers,
        IEnumerable<ServerGroup> groups,
        AppSettings settings,
        CancellationToken ct = default)
    {
        var session = new ExecutionSession { StartedAt = DateTime.Now };
        var groupLookup = groups.ToDictionary(g => g.Id, g => g.Name);

        foreach (var server in servers.OrderBy(s => s.Order))
        {
            ct.ThrowIfCancellationRequested();

            var groupName = !string.IsNullOrEmpty(server.GroupId) && groupLookup.TryGetValue(server.GroupId, out var gn)
                ? gn
                : "Ungrouped";

            var serverResult = new ServerExecutionResult
            {
                ServerId = server.Id,
                ServerName = server.Name,
                GroupName = groupName,
                Status = ExecutionStatus.Running
            };

            ServerStarted?.Invoke(serverResult);
            var serverSw = System.Diagnostics.Stopwatch.StartNew();

            var commands = server.Commands.OrderBy(c => c.Order).ToList();
            if (commands.Count == 0)
            {
                serverResult.Status = ExecutionStatus.Skipped;
                serverResult.Duration = TimeSpan.Zero;
                session.Servers.Add(serverResult);
                ServerCompleted?.Invoke(serverResult);
                continue;
            }

            var hasFailure = false;

            foreach (var command in commands)
            {
                ct.ThrowIfCancellationRequested();

                CommandStarted?.Invoke(new CommandExecutionResult
                {
                    CommandId = command.Id,
                    CommandText = command.Text,
                    Status = ExecutionStatus.Running
                }, server.Name);

                var progress = new Progress<string>(line => OutputReceived?.Invoke($"[{server.Name}] {line}"));

                CommandExecutionResult cmdResult = server.ConnectionType switch
                {
                    ConnectionType.Ssh => await _sshExecutor.ExecuteCommandAsync(
                        server, command, settings, progress, ct),
                    ConnectionType.Telnet => await _telnetExecutor.ExecuteCommandAsync(
                        server, command, settings, progress, ct),
                    _ => new CommandExecutionResult
                    {
                        CommandId = command.Id,
                        CommandText = command.Text,
                        Status = ExecutionStatus.Failed,
                        ErrorMessage = "Unknown connection type."
                    }
                };

                serverResult.Commands.Add(cmdResult);
                CommandCompleted?.Invoke(cmdResult, server.Name);

                if (cmdResult.Status == ExecutionStatus.Failed)
                    hasFailure = true;
            }

            serverSw.Stop();
            serverResult.Duration = serverSw.Elapsed;
            serverResult.Status = hasFailure ? ExecutionStatus.Failed : ExecutionStatus.Success;
            session.Servers.Add(serverResult);
            ServerCompleted?.Invoke(serverResult);
        }

        session.FinishedAt = DateTime.Now;
        SessionCompleted?.Invoke(session);
        return session;
    }
}
