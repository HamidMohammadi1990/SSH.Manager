using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class InteractiveSessionExecutor
{
    private const int PollIntervalMs = 20;
    private const int SendBufferMs = 15;

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
        var sessionTail = new StringBuilder();
        var responseIdleMs = ResolveResponseIdleMs(stepDelayMs);
        var maxReadMs = commandTimeoutSeconds * 1000;
        BatchStep? lastSentStep = null;

        await ReadTelnetBurstAsync(stream, buffer, sessionTail, outputProgress, responseIdleMs, maxReadMs, ct);

        if (!string.IsNullOrWhiteSpace(credential.Username))
            await TelnetLoginAsync(stream, buffer, credential, sessionTail, responseIdleMs, maxReadMs, outputProgress, ct);

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            onStepStarted?.Invoke(step);
            var result = CreateStepResult(step);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var payload = InteractiveStepPayloadBuilder.Build(step, credential);
                if (string.IsNullOrEmpty(payload))
                {
                    result.Status = ExecutionStatus.Skipped;
                    result.Output = string.Empty;
                }
                else
                {
                    await WaitUntilReadyTelnetAsync(
                        stream, buffer, sessionTail, step, lastSentStep,
                        outputProgress, responseIdleMs, maxReadMs, ct);

                    await stream.WriteAsync(Encoding.ASCII.GetBytes(payload), ct);
                    await Task.Delay(SendBufferMs, ct);

                    var stepOutput = new StringBuilder();
                    await ReadTelnetBurstAsync(stream, buffer, stepOutput, outputProgress, responseIdleMs, maxReadMs, ct, sessionTail);

                    result.Output = stepOutput.ToString().TrimEnd();
                    result.Status = ExecutionStatus.Success;
                    lastSentStep = step;
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
            var sessionTail = new StringBuilder();
            var responseIdleMs = ResolveResponseIdleMs(stepDelayMs);
            var maxReadMs = commandTimeoutSeconds * 1000;
            BatchStep? lastSentStep = null;

            ReadShellBurst(shell, sessionTail, outputProgress, responseIdleMs, maxReadMs);

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                onStepStarted?.Invoke(step);
                var result = CreateStepResult(step);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var payload = InteractiveStepPayloadBuilder.Build(step, credential);
                    if (string.IsNullOrEmpty(payload))
                    {
                        result.Status = ExecutionStatus.Skipped;
                        result.Output = string.Empty;
                    }
                    else
                    {
                        WaitUntilReadyShell(
                            shell, sessionTail, step, lastSentStep,
                            outputProgress, responseIdleMs, maxReadMs);

                        shell.Write(payload);
                        shell.Flush();
                        Thread.Sleep(SendBufferMs);

                        var stepOutput = new StringBuilder();
                        ReadShellBurst(shell, sessionTail, outputProgress, responseIdleMs, maxReadMs, stepOutput);

                        result.Output = stepOutput.ToString().TrimEnd();
                        result.Status = ExecutionStatus.Success;
                        lastSentStep = step;
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

    private static CommandExecutionResult CreateStepResult(BatchStep step) =>
        new()
        {
            CommandId = Guid.NewGuid().ToString(),
            CommandText = step.DisplayText,
            StartedAt = DateTime.Now,
            Status = ExecutionStatus.Running
        };

    private static int ResolveResponseIdleMs(int stepDelayMs) =>
        Math.Clamp(stepDelayMs, 250, 1500);

    private static async Task WaitUntilReadyTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder sessionTail,
        BatchStep nextStep,
        BatchStep? lastSentStep,
        IProgress<string>? progress,
        int baseIdleMs,
        int maxWaitMs,
        CancellationToken ct)
    {
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            var drain = new StringBuilder();
            await ReadTelnetBurstAsync(stream, buffer, drain, progress, baseIdleMs,
                Math.Min(500, remainingMs), ct, sessionTail);
        }
    }

    private static void WaitUntilReadyShell(
        ShellStream shell,
        StringBuilder sessionTail,
        BatchStep nextStep,
        BatchStep? lastSentStep,
        IProgress<string>? progress,
        int baseIdleMs,
        int maxWaitMs)
    {
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            ReadShellBurst(shell, sessionTail, progress, baseIdleMs, Math.Min(500, remainingMs));
        }
    }

    private static async Task ReadTelnetBurstAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        CancellationToken ct,
        StringBuilder? sessionTail = null)
    {
        var startedAt = DateTime.UtcNow;
        var overallDeadline = startedAt.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < overallDeadline)
        {
            if (stream.DataAvailable)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0)
                    break;

                var text = Encoding.ASCII.GetString(buffer, 0, read);
                output.Append(text);
                progress?.Report(text);
                if (sessionTail != null)
                    InteractiveSessionReadiness.AppendToSessionTail(sessionTail, text);

                lastDataAt = DateTime.UtcNow;
                continue;
            }

            if (lastDataAt.HasValue)
            {
                var idleMs = (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds;
                if (InteractiveSessionReadiness.ShouldBreakReadAfterBurst(
                        output.ToString(), idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    private static void ReadShellBurst(
        ShellStream shell,
        StringBuilder sessionTail,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        StringBuilder? stepOutput = null)
    {
        var output = stepOutput ?? new StringBuilder();
        var startedAt = DateTime.UtcNow;
        var overallDeadline = startedAt.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < overallDeadline)
        {
            var text = shell.Read();
            if (!string.IsNullOrEmpty(text))
            {
                output.Append(text);
                progress?.Report(text);
                InteractiveSessionReadiness.AppendToSessionTail(sessionTail, text);

                lastDataAt = DateTime.UtcNow;
                continue;
            }

            if (lastDataAt.HasValue)
            {
                var idleMs = (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds;
                if (InteractiveSessionReadiness.ShouldBreakReadAfterBurst(
                        output.ToString(), idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static async Task TelnetLoginAsync(
        NetworkStream stream,
        byte[] buffer,
        BatchCredential credential,
        StringBuilder sessionTail,
        int idleTimeoutMs,
        int maxWaitMs,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var loginOutput = new StringBuilder();
        await ReadTelnetBurstAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(SendBufferMs, ct);
        await ReadTelnetBurstAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(SendBufferMs, ct);
            await ReadTelnetBurstAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);
        }
    }
}
