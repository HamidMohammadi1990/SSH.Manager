using System.Net.Sockets;
using System.Text;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class InteractiveSessionExecutor
{
    private const int PollIntervalMs = 25;
    private const int SendBufferMs = 30;
    private const int SshPostSendDelayMs = 50;

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
        IProgress<string>? progress,
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
        BatchStep? lastSentSubStep = null;

        await DrainTelnetAsync(stream, buffer, sessionTail, progress, responseIdleMs, maxReadMs, ct);

        if (!string.IsNullOrWhiteSpace(credential.Username))
            await TelnetLoginAsync(stream, buffer, credential, sessionTail, responseIdleMs, maxReadMs, progress, ct);

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            onStepStarted?.Invoke(step);
            var result = CreateStepResult(step);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var output = await ExecuteStepSequentiallyTelnetAsync(
                    stream, buffer, sessionTail, step, credential, lastSentSubStep,
                    progress, responseIdleMs, maxReadMs, ct);

                if (string.IsNullOrEmpty(output) && InteractiveStepExpander.Expand(step).Count == 0)
                {
                    result.Status = ExecutionStatus.Skipped;
                    result.Output = string.Empty;
                }
                else
                {
                    result.Output = output.TrimEnd();
                    result.Status = InteractiveSessionReadiness.ContainsDeviceError(result.Output)
                        ? ExecutionStatus.Failed
                        : ExecutionStatus.Success;
                    if (result.Status == ExecutionStatus.Failed)
                        result.ErrorMessage = "Device reported an error during command execution.";
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

            lastSentSubStep = GetLastSubStep(step);
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
        IProgress<string>? progress,
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
            BatchStep? lastSentSubStep = null;

            DrainShell(shell, sessionTail, progress, responseIdleMs, maxReadMs);

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                onStepStarted?.Invoke(step);
                var result = CreateStepResult(step);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var output = ExecuteStepSequentiallyShell(
                        shell, sessionTail, step, credential, lastSentSubStep,
                        progress, responseIdleMs, maxReadMs, ct);

                    if (string.IsNullOrEmpty(output) && InteractiveStepExpander.Expand(step).Count == 0)
                    {
                        result.Status = ExecutionStatus.Skipped;
                        result.Output = string.Empty;
                    }
                    else
                    {
                        result.Output = output.TrimEnd();
                        result.Status = InteractiveSessionReadiness.ContainsDeviceError(result.Output)
                            ? ExecutionStatus.Failed
                            : ExecutionStatus.Success;
                        if (result.Status == ExecutionStatus.Failed)
                            result.ErrorMessage = "Device reported an error during command execution.";
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

                lastSentSubStep = GetLastSubStep(step);
            }

            client.Disconnect();
            return results;
        }, ct);
    }

    private static async Task<string> ExecuteStepSequentiallyTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder sessionTail,
        BatchStep step,
        BatchCredential credential,
        BatchStep? lastSentSubStep,
        IProgress<string>? progress,
        int responseIdleMs,
        int maxReadMs,
        CancellationToken ct)
    {
        var subSteps = InteractiveStepExpander.Expand(step);
        var output = new StringBuilder();
        var previous = lastSentSubStep;

        foreach (var subStep in subSteps)
        {
            ct.ThrowIfCancellationRequested();

            await WaitUntilReadyTelnetAsync(
                stream, buffer, sessionTail, subStep, previous, progress, responseIdleMs, maxReadMs, ct);

            var payload = InteractiveStepPayloadBuilder.ResolvePart(subStep, credential, ConnectionType.Telnet);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(payload), ct);
            await Task.Delay(SendBufferMs, ct);

            var chunk = new StringBuilder();
            await ReadAfterSendTelnetAsync(
                stream, buffer, chunk, sessionTail, subStep, progress, responseIdleMs, maxReadMs, ct);
            output.Append(chunk);

            previous = subStep;
        }

        return output.ToString();
    }

    private static string ExecuteStepSequentiallyShell(
        ShellStream shell,
        StringBuilder sessionTail,
        BatchStep step,
        BatchCredential credential,
        BatchStep? lastSentSubStep,
        IProgress<string>? progress,
        int responseIdleMs,
        int maxReadMs,
        CancellationToken ct)
    {
        var subSteps = InteractiveStepExpander.Expand(step);
        var output = new StringBuilder();
        var previous = lastSentSubStep;

        foreach (var subStep in subSteps)
        {
            ct.ThrowIfCancellationRequested();

            WaitUntilReadyShell(
                shell, sessionTail, subStep, previous, progress, responseIdleMs, maxReadMs);

            var payload = InteractiveStepPayloadBuilder.ResolvePart(subStep, credential, ConnectionType.Ssh);
            shell.Write(payload);
            shell.Flush();
            Thread.Sleep(SshPostSendDelayMs);

            var chunk = new StringBuilder();
            ReadAfterSendShell(shell, chunk, sessionTail, subStep, progress, responseIdleMs, maxReadMs);
            output.Append(chunk);

            previous = subStep;
        }

        return output.ToString();
    }

    private static BatchStep? GetLastSubStep(BatchStep step)
    {
        var subSteps = InteractiveStepExpander.Expand(step);
        return subSteps.Count > 0 ? subSteps[^1] : null;
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
        Math.Clamp(stepDelayMs, 300, 2000);

    private static async Task WaitUntilReadyTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder sessionTail,
        BatchStep nextSubStep,
        BatchStep? lastSentSubStep,
        IProgress<string>? progress,
        int baseIdleMs,
        int maxWaitMs,
        CancellationToken ct)
    {
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextSubStep, lastSentSubStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextSubStep, lastSentSubStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            var drain = new StringBuilder();
            await DrainTelnetAsync(stream, buffer, drain, progress, baseIdleMs,
                Math.Min(800, remainingMs), ct, sessionTail);
        }
    }

    private static void WaitUntilReadyShell(
        ShellStream shell,
        StringBuilder sessionTail,
        BatchStep nextSubStep,
        BatchStep? lastSentSubStep,
        IProgress<string>? progress,
        int baseIdleMs,
        int maxWaitMs)
    {
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextSubStep, lastSentSubStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextSubStep, lastSentSubStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            DrainShell(shell, sessionTail, progress, baseIdleMs, Math.Min(800, remainingMs));
        }
    }

    private static async Task ReadAfterSendTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        StringBuilder sessionTail,
        BatchStep sentSubStep,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < deadline)
        {
            if (stream.DataAvailable)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0)
                    break;

                var text = Encoding.ASCII.GetString(buffer, 0, read);
                output.Append(text);
                progress?.Report(text);
                InteractiveSessionReadiness.AppendToSessionTail(sessionTail, text);
                lastDataAt = DateTime.UtcNow;
                continue;
            }

            if (lastDataAt.HasValue)
            {
                var idleMs = (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds;
                if (InteractiveSessionReadiness.ShouldBreakReadAfterSend(
                        sessionTail.ToString(), sentSubStep, idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    private static void ReadAfterSendShell(
        ShellStream shell,
        StringBuilder output,
        StringBuilder sessionTail,
        BatchStep sentSubStep,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < deadline)
        {
            var readAny = false;
            string text;
            while (!string.IsNullOrEmpty(text = shell.Read()))
            {
                readAny = true;
                output.Append(text);
                progress?.Report(text);
                InteractiveSessionReadiness.AppendToSessionTail(sessionTail, text);
                lastDataAt = DateTime.UtcNow;
            }

            if (readAny)
                continue;

            if (lastDataAt.HasValue)
            {
                var idleMs = (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds;
                if (InteractiveSessionReadiness.ShouldBreakReadAfterSend(
                        sessionTail.ToString(), sentSubStep, idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            Thread.Sleep(PollIntervalMs);
        }
    }

    private static async Task DrainTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        CancellationToken ct,
        StringBuilder? sessionTail = null)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < deadline)
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

            if (lastDataAt.HasValue &&
                (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds >= idleTimeoutMs)
                break;

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    private static void DrainShell(
        ShellStream shell,
        StringBuilder sessionTail,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        StringBuilder? output = null)
    {
        var buffer = output ?? new StringBuilder();
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        DateTime? lastDataAt = null;

        while (DateTime.UtcNow < deadline)
        {
            var readAny = false;
            string text;
            while (!string.IsNullOrEmpty(text = shell.Read()))
            {
                readAny = true;
                buffer.Append(text);
                progress?.Report(text);
                InteractiveSessionReadiness.AppendToSessionTail(sessionTail, text);
                lastDataAt = DateTime.UtcNow;
            }

            if (readAny)
                continue;

            if (lastDataAt.HasValue &&
                (DateTime.UtcNow - lastDataAt.Value).TotalMilliseconds >= idleTimeoutMs)
                break;

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
        await DrainTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(SendBufferMs, ct);
        await DrainTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(SendBufferMs, ct);
            await DrainTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs, ct, sessionTail);
        }
    }
}
