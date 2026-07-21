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
        var bootstrapStep = new BatchStep { Type = BatchStepType.Command, Text = string.Empty };
        BatchStep? lastSentSubStep = null;

        await ReadTelnetAsync(stream, buffer, sessionTail, outputProgress, responseIdleMs, maxReadMs,
            bootstrapStep, null, ct);

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

                        await WaitUntilReadyForStepTelnetAsync(
                            stream, buffer, sessionTail, subStep, lastSentSubStep,
                            outputProgress, responseIdleMs, maxReadMs, ct);

                        var payload = ResolveStepPayload(subStep, credential);
                        await stream.WriteAsync(Encoding.ASCII.GetBytes(payload), ct);
                        await Task.Delay(SendBufferMs, ct);

                        await ReadTelnetAsync(stream, buffer, stepOutput, outputProgress, responseIdleMs, maxReadMs,
                            subStep, lastSentSubStep, ct, sessionTail);

                        lastSentSubStep = subStep;
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
            var sessionTail = new StringBuilder();
            var responseIdleMs = ResolveResponseIdleMs(stepDelayMs);
            var maxReadMs = commandTimeoutSeconds * 1000;
            var bootstrapStep = new BatchStep { Type = BatchStepType.Command, Text = string.Empty };
            BatchStep? lastSentSubStep = null;

            ReadShellOutput(shell, sessionTail, outputProgress, responseIdleMs, maxReadMs,
                bootstrapStep, null);

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

                            WaitUntilReadyForStepShell(
                                shell, sessionTail, subStep, lastSentSubStep,
                                outputProgress, responseIdleMs, maxReadMs);

                            var payload = ResolveStepPayload(subStep, credential);
                            shell.Write(payload);
                            shell.Flush();
                            Thread.Sleep(SendBufferMs);

                            stepOutput.Append(ReadShellOutput(shell, sessionTail, outputProgress, responseIdleMs, maxReadMs,
                                subStep, lastSentSubStep));

                            lastSentSubStep = subStep;
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

    private static CommandExecutionResult CreateStepResult(BatchStep step) =>
        new()
        {
            CommandId = Guid.NewGuid().ToString(),
            CommandText = step.DisplayText,
            StartedAt = DateTime.Now,
            Status = ExecutionStatus.Running
        };

    private static string ResolveStepPayload(BatchStep step, BatchCredential credential) =>
        step.Type switch
        {
            BatchStepType.Enter => "\r\n",
            BatchStepType.Password => credential.PasswordForStep + "\r\n",
            BatchStepType.Command => step.Text + "\r\n",
            _ => "\r\n"
        };

    private static int ResolveResponseIdleMs(int stepDelayMs) =>
        Math.Clamp(stepDelayMs, 250, 1500);

    private static async Task WaitUntilReadyForStepTelnetAsync(
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
        var bootstrapStep = new BatchStep { Type = BatchStepType.Command, Text = string.Empty };
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            var drain = new StringBuilder();
            await ReadTelnetAsync(stream, buffer, drain, progress, baseIdleMs,
                Math.Min(500, remainingMs), bootstrapStep, lastSentStep, ct, sessionTail);
        }
    }

    private static void WaitUntilReadyForStepShell(
        ShellStream shell,
        StringBuilder sessionTail,
        BatchStep nextStep,
        BatchStep? lastSentStep,
        IProgress<string>? progress,
        int baseIdleMs,
        int maxWaitMs)
    {
        var bootstrapStep = new BatchStep { Type = BatchStepType.Command, Text = string.Empty };
        if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
            return;

        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            if (InteractiveSessionReadiness.IsReadyForStep(sessionTail.ToString(), nextStep, lastSentStep))
                return;

            var remainingMs = (int)Math.Max(PollIntervalMs, (deadline - DateTime.UtcNow).TotalMilliseconds);
            ReadShellOutput(shell, sessionTail, progress, baseIdleMs,
                Math.Min(500, remainingMs), bootstrapStep, lastSentStep);
        }
    }

    private static async Task ReadTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        BatchStep sentStep,
        BatchStep? lastSentStep,
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
                if (InteractiveSessionReadiness.ShouldBreakReadAfterSend(
                        output.ToString(), sentStep, idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            await Task.Delay(PollIntervalMs, ct);
        }
    }

    private static string ReadShellOutput(
        ShellStream shell,
        StringBuilder sessionTail,
        IProgress<string>? progress,
        int idleTimeoutMs,
        int maxWaitMs,
        BatchStep sentStep,
        BatchStep? lastSentStep,
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
                if (InteractiveSessionReadiness.ShouldBreakReadAfterSend(
                        output.ToString(), sentStep, idleMs, idleTimeoutMs, receivedData: true))
                    break;
            }

            Thread.Sleep(PollIntervalMs);
        }

        return output.ToString();
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
        var loginStep = new BatchStep { Type = BatchStepType.Command, Text = "login" };

        await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs,
            loginStep, null, ct, sessionTail);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(SendBufferMs, ct);
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs,
            loginStep, null, ct, sessionTail);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            var passwordStep = new BatchStep { Type = BatchStepType.Password };
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(SendBufferMs, ct);
            await ReadTelnetAsync(stream, buffer, loginOutput, progress, idleTimeoutMs, maxWaitMs,
                passwordStep, null, ct, sessionTail);
        }
    }
}
