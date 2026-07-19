using SshManager.Models;

namespace SshManager.Services;

public class BatchExecutionService
{
    private readonly InteractiveSessionExecutor _sessionExecutor = new();

    public event Action<ServerExecutionResult>? ServerStarted;
    public event Action<ServerExecutionResult>? ServerCompleted;
    public event Action<CommandExecutionResult, string>? StepStarted;
    public event Action<CommandExecutionResult, string>? StepCompleted;
    public event Action<string>? OutputReceived;
    public event Action<ExecutionSession>? SessionCompleted;

    public async Task<ExecutionSession> ExecuteAsync(BatchJob job, CancellationToken ct = default)
    {
        var session = new ExecutionSession { StartedAt = DateTime.Now };

        foreach (var host in job.Targets)
        {
            ct.ThrowIfCancellationRequested();

            var server = new ServerProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = host,
                Host = host,
                Port = job.Defaults.Port,
                ConnectionType = job.Defaults.ConnectionType,
                UseCustomCredentials = true,
                CustomUsername = job.Credential.Username,
                CustomPasswordEncrypted = CredentialService.Encrypt(job.Credential.Password)
            };

            var targetResult = new ServerExecutionResult
            {
                ServerId = server.Id,
                ServerName = host,
                GroupName = "Batch Job",
                Status = ExecutionStatus.Running
            };

            ServerStarted?.Invoke(targetResult);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var progress = new Progress<string>(line => OutputReceived?.Invoke($"[{host}] {line}"));

                var stepResults = await _sessionExecutor.ExecuteStepsAsync(
                    server,
                    job.Credential,
                    job.Steps,
                    job.Defaults.StepDelayMs,
                    progress,
                    step =>
                    {
                        StepStarted?.Invoke(new CommandExecutionResult
                        {
                            CommandId = Guid.NewGuid().ToString(),
                            CommandText = step.DisplayText,
                            Status = ExecutionStatus.Running
                        }, host);
                    },
                    ct);

                foreach (var stepResult in stepResults)
                {
                    targetResult.Commands.Add(stepResult);
                    StepCompleted?.Invoke(stepResult, host);
                }

                targetResult.Status = stepResults.Count > 0 &&
                                      stepResults.All(r => r.Status == ExecutionStatus.Success)
                    ? ExecutionStatus.Success
                    : ExecutionStatus.Failed;
            }
            catch (OperationCanceledException)
            {
                targetResult.Status = ExecutionStatus.Failed;
                throw;
            }
            catch (Exception ex)
            {
                targetResult.Status = ExecutionStatus.Failed;
                var failure = new CommandExecutionResult
                {
                    CommandId = Guid.NewGuid().ToString(),
                    CommandText = "(session)",
                    Status = ExecutionStatus.Failed,
                    ErrorMessage = ex.Message,
                    StartedAt = DateTime.Now,
                    FinishedAt = DateTime.Now
                };
                targetResult.Commands.Add(failure);
                StepCompleted?.Invoke(failure, host);
                OutputReceived?.Invoke($"[{host}] ERROR: {ex.Message}");
            }

            sw.Stop();
            targetResult.Duration = sw.Elapsed;
            session.Servers.Add(targetResult);
            ServerCompleted?.Invoke(targetResult);
        }

        session.FinishedAt = DateTime.Now;
        SessionCompleted?.Invoke(session);
        return session;
    }
}
