using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class InteractiveSessionExecutor
{
    public async Task<List<CommandExecutionResult>> ExecuteStepsAsync(
        ServerProfile server,
        BatchCredential credential,
        IReadOnlyList<BatchStep> steps,
        int stepDelayMs,
        int connectionTimeoutSeconds,
        int commandTimeoutSeconds,
        IProgress<string>? outputProgress = null,
        Action<BatchStep>? onStepStarted = null,
        CancellationToken ct = default)
    {
        return server.ConnectionType switch
        {
            ConnectionType.Telnet => await ExecuteTelnetStepsAsync(
                server, credential, steps, stepDelayMs, connectionTimeoutSeconds, commandTimeoutSeconds,
                outputProgress, onStepStarted, ct),
            ConnectionType.Ssh => await ExecuteSshStepsAsync(
                server, credential, steps, stepDelayMs, connectionTimeoutSeconds, commandTimeoutSeconds,
                outputProgress, onStepStarted, ct),
            _ => throw new NotSupportedException($"Unsupported connection type: {server.ConnectionType}")
        };
    }

    private static async Task<List<CommandExecutionResult>> ExecuteTelnetStepsAsync(
        ServerProfile server,
        BatchCredential credential,
        IReadOnlyList<BatchStep> steps,
        int stepDelayMs,
        int connectionTimeoutSeconds,
        int commandTimeoutSeconds,
        IProgress<string>? outputProgress,
        Action<BatchStep>? onStepStarted,
        CancellationToken ct)
    {
        var results = new List<CommandExecutionResult>();

        using var client = new TcpClient();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(TimeSpan.FromSeconds(connectionTimeoutSeconds));

        await client.ConnectAsync(server.Host, server.Port, connectCts.Token);
        await using var stream = client.GetStream();
        var buffer = new byte[4096];
        var sessionOutput = new StringBuilder();
        var responseIdleMs = ResolveResponseIdleMs(stepDelayMs);
        var maxReadMs = commandTimeoutSeconds * 1000;
        var sendPauseMs = Math.Min(stepDelayMs, 150);

        await ReadTelnetAsync(stream, buffer, sessionOutput, outputProgress, responseIdleMs, maxReadMs, ct);

        if (!string.IsNullOrWhiteSpace(credential.Username))
            await TelnetLoginAsync(stream, buffer, credential, sendPauseMs, responseIdleMs, maxReadMs, outputProgress, ct);

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            onStepStarted?.Invoke(step);
            var result = CreateStepResult(step);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var subSteps = InteractiveStepExpander.Expand(step);
                if (subSteps.Count == 0)
                {
                    result.Status = ExecutionStatus.Skipped;
                    result.Output = string.Empty;
                }
                else
                {
                    var stepOutput = new StringBuilder();
                    foreach (var subStep in subSteps)
                    {
                        ct.ThrowIfCancellationRequested();

                        var payload = ResolveStepPayload(subStep, credential);
                        var bytes = Encoding.ASCII.GetBytes(payload);
                        await stream.WriteAsync(bytes, ct);
                        await Task.Delay(sendPauseMs, ct);

                        await ReadTelnetAsync(stream, buffer, stepOutput, outputProgress, responseIdleMs, maxReadMs, ct);
                    }

                    result.Output = stepOutput.ToString().TrimEnd();
                    result.Status = ExecutionStatus.Success;
                }
            }
            catch (OperationCanceledException)
            {
                result.Status = ExecutionStatus.Failed;
                result.ErrorMessage = "Execution was cancelled.";
                results.Add(result);
                throw;
            }
            catch (Exception ex)
            {
                result.Status = ExecutionStatus.Failed;
                result.ErrorMessage = ex.Message;
            }

            sw.Stop();
            result.Duration = sw.Elapsed;
            result.FinishedAt = DateTime.Now;
            results.Add(result);

            if (result.Status == ExecutionStatus.Failed)
                break;
        }

        return results;
    }

    private static async Task<List<CommandExecutionResult>> ExecuteSshStepsAsync(
        ServerProfile server,
        BatchCredential credential,
        IReadOnlyList<BatchStep> steps,
        int stepDelayMs,
        int connectionTimeoutSeconds,
        int commandTimeoutSeconds,
        IProgress<string>? outputProgress,
        Action<BatchStep>? onStepStarted,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var results = new List<CommandExecutionResult>();
            ct.ThrowIfCancellationRequested();

            using var client = ConnectionTestService.CreateSshClient(
                server, credential.Username, credential.Password, null, connectionTimeoutSeconds);
            client.Connect();

            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to establish SSH connection.");

            using var shell = client.CreateShellStream("vt100", 120, 40, 800, 600, 4096);
            var responseIdleMs = ResolveResponseIdleMs(stepDelayMs);
            var maxReadMs = commandTimeoutSeconds * 1000;
            var sendPauseMs = Math.Min(stepDelayMs, 150);

            ReadShellOutput(shell, outputProgress, responseIdleMs, maxReadMs);

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                onStepStarted?.Invoke(step);
                var result = CreateStepResult(step);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var subSteps = InteractiveStepExpander.Expand(step);
                    if (subSteps.Count == 0)
                    {
                        result.Status = ExecutionStatus.Skipped;
                        result.Output = string.Empty;
                    }
                    else
                    {
                        var stepOutput = new StringBuilder();
                        foreach (var subStep in subSteps)
                        {
                            ct.ThrowIfCancellationRequested();

                            var payload = ResolveStepPayload(subStep, credential);
                            shell.Write(payload);
                            shell.Flush();
                            Thread.Sleep(sendPauseMs);

                            stepOutput.Append(ReadShellOutput(shell, outputProgress, responseIdleMs, maxReadMs));
                        }

                        result.Output = stepOutput.ToString().TrimEnd();
                        result.Status = ExecutionStatus.Success;
                    }
                }
                catch (OperationCanceledException)
                {
                    result.Status = ExecutionStatus.Failed;
                    result.ErrorMessage = "Execution was cancelled.";
                    results.Add(result);
                    throw;
                }
                catch (Exception ex)
                {
                    result.Status = ExecutionStatus.Failed;
                    result.ErrorMessage = ex.Message;
                }

                sw.Stop();
                result.Duration = sw.Elapsed;
                result.FinishedAt = DateTime.Now;
                results.Add(result);

                if (result.Status == ExecutionStatus.Failed)
                    break;
            }

            client.Disconnect();
            return results;
        }, ct);
    }

    private static CommandExecutionResult CreateStepResult(BatchStep step)
    {
        return new CommandExecutionResult
        {
            CommandId = Guid.NewGuid().ToString(),
            CommandText = step.DisplayText,
            StartedAt = DateTime.Now,
            Status = ExecutionStatus.Running
        };
    }

    private static string ResolveStepPayload(BatchStep step, BatchCredential credential)
    {
        return step.Type switch
        {
            BatchStepType.Enter => "\r\n",
            BatchStepType.Password => credential.PasswordForStep + "\r\n",
            BatchStepType.Command => step.Text + "\r\n",
            _ => "\r\n"
        };
    }

    private static int ResolveResponseIdleMs(int stepDelayMs) =>
        Math.Clamp(stepDelayMs, 250, 1500);

    private static async Task ReadTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var overallDeadline = startedAt.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;
        const int pollIntervalMs = 25;

        while (DateTime.UtcNow < overallDeadline)
        {
            if (stream.DataAvailable)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                var text = Encoding.ASCII.GetString(buffer, 0, read);
                output.Append(text);
                progress?.Report(text);
                lastDataAt = DateTime.UtcNow;
                continue;
            }

            if (lastDataAt.HasValue)
            {
                if ((DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds >= idleTimeoutMs)
                    break;
            }
            else if ((DateTime.UtcNow - startedAt).TotalMilliseconds >= idleTimeoutMs)
            {
                break;
            }

            await Task.Delay(pollIntervalMs, ct);
        }
    }

    private static string ReadShellOutput(
        ShellStream shell,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs)
    {
        var output = new StringBuilder();
        var startedAt = DateTime.UtcNow;
        var overallDeadline = startedAt.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;
        const int pollIntervalMs = 25;

        while (DateTime.UtcNow < overallDeadline)
        {
            var text = shell.Read();
            if (!string.IsNullOrEmpty(text))
            {
                output.Append(text);
                progress?.Report(text);
                lastDataAt = DateTime.UtcNow;
                continue;
            }

            if (lastDataAt.HasValue)
            {
                if ((DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds >= idleTimeoutMs)
                    break;
            }
            else if ((DateTime.UtcNow - startedAt).TotalMilliseconds >= idleTimeoutMs)
            {
                break;
            }

            Thread.Sleep(pollIntervalMs);
        }

        return output.ToString();
    }

    private static async Task TelnetLoginAsync(
        NetworkStream stream,
        byte[] buffer,
        BatchCredential credential,
        int sendPauseMs,
        int idleTimeoutMs,
        int maxWaitMs,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var loginOutput = new StringBuilder();
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(sendPauseMs, ct);
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(sendPauseMs, ct);
            await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct);
        }
    }
}
