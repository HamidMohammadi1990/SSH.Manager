using SshManager.Models;

namespace SshManager.Services;

public class ExecutionService
{
    private readonly InteractiveSessionExecutor _sessionExecutor = new();

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

            try
            {
                var steps = commands.Select(c => new BatchStep
                {
                    Type = BatchStepType.Command,
                    Text = c.Text
                }).ToList();

                var credential = BuildCredential(server, settings);
                var progress = new Progress<string>(line => OutputReceived?.Invoke($"[{server.Name}] {line}"));
                var commandIndex = 0;

                var stepResults = await _sessionExecutor.ExecuteStepsAsync(
                    server,
                    credential,
                    steps,
                    settings.BatchStepDelayMs,
                    settings.ConnectionTimeoutSeconds,
                    settings.CommandTimeoutSeconds,
                    progress,
                    _ =>
                    {
                        if (commandIndex >= commands.Count) return;

                        var command = commands[commandIndex++];
                        CommandStarted?.Invoke(new CommandExecutionResult
                        {
                            CommandId = command.Id,
                            CommandText = command.Text,
                            Status = ExecutionStatus.Running
                        }, server.Name);
                    },
                    ct);

                var hasFailure = false;
                for (var i = 0; i < stepResults.Count; i++)
                {
                    var stepResult = stepResults[i];
                    if (i < commands.Count)
                    {
                        stepResult.CommandId = commands[i].Id;
                        stepResult.CommandText = commands[i].Text;
                    }

                    serverResult.Commands.Add(stepResult);
                    CommandCompleted?.Invoke(stepResult, server.Name);

                    if (stepResult.Status == ExecutionStatus.Failed)
                        hasFailure = true;
                }

                serverResult.Status = stepResults.Count > 0 && !hasFailure
                    ? ExecutionStatus.Success
                    : ExecutionStatus.Failed;
            }
            catch (OperationCanceledException)
            {
                serverResult.Status = ExecutionStatus.Failed;
                throw;
            }
            catch (Exception ex)
            {
                serverResult.Status = ExecutionStatus.Failed;
                var failure = new CommandExecutionResult
                {
                    CommandId = Guid.NewGuid().ToString(),
                    CommandText = "(session)",
                    Status = ExecutionStatus.Failed,
                    ErrorMessage = ex.Message,
                    StartedAt = DateTime.Now,
                    FinishedAt = DateTime.Now
                };
                serverResult.Commands.Add(failure);
                CommandCompleted?.Invoke(failure, server.Name);
                OutputReceived?.Invoke($"[{server.Name}] ERROR: {ex.Message}");
            }

            serverSw.Stop();
            serverResult.Duration = serverSw.Elapsed;
            session.Servers.Add(serverResult);
            ServerCompleted?.Invoke(serverResult);
        }

        session.FinishedAt = DateTime.Now;
        SessionCompleted?.Invoke(session);
        return session;
    }

    private static BatchCredential BuildCredential(ServerProfile server, AppSettings settings)
    {
        var (username, password, _) = ConnectionTestService.ResolveCredentials(server, settings);
        return new BatchCredential
        {
            Username = username,
            Password = password
        };
    }
}
