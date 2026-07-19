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
        var readTimeoutMs = Math.Max(commandTimeoutSeconds * 1000, stepDelayMs);

        await ReadTelnetAsync(stream, buffer, sessionOutput, outputProgress, stepDelayMs, readTimeoutMs, ct);

        if (!string.IsNullOrWhiteSpace(credential.Username))
            await TelnetLoginAsync(stream, buffer, credential, stepDelayMs, readTimeoutMs, outputProgress, ct);

        foreach (var step in steps)
        {
            ct.ThrowIfCancellationRequested();
            onStepStarted?.Invoke(step);
            var result = CreateStepResult(step);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var payload = ResolveStepPayload(step, credential);
                var bytes = Encoding.ASCII.GetBytes(payload);
                await stream.WriteAsync(bytes, ct);
                await Task.Delay(stepDelayMs, ct);

                var stepOutput = new StringBuilder();
                await ReadTelnetAsync(stream, buffer, stepOutput, outputProgress, stepDelayMs, readTimeoutMs, ct);

                result.Output = stepOutput.ToString().TrimEnd();
                result.Status = ExecutionStatus.Success;
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
            ReadShellOutput(shell, outputProgress, stepDelayMs, commandTimeoutSeconds * 1000);

            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                onStepStarted?.Invoke(step);
                var result = CreateStepResult(step);
                var sw = System.Diagnostics.Stopwatch.StartNew();

                try
                {
                    var payload = ResolveStepPayload(step, credential);
                    shell.Write(payload);
                    shell.Flush();
                    Thread.Sleep(stepDelayMs);

                    var output = ReadShellOutput(shell, outputProgress, stepDelayMs, commandTimeoutSeconds * 1000);
                    result.Output = output.TrimEnd();
                    result.Status = ExecutionStatus.Success;
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
            _ => step.Text + "\r\n"
        };
    }

    private static async Task ReadTelnetAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        int stepDelayMs,
        int maxWaitMs,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);

        while (DateTime.UtcNow < deadline)
        {
            if (stream.DataAvailable)
            {
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                var text = Encoding.ASCII.GetString(buffer, 0, read);
                output.Append(text);
                progress?.Report(text);
                deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            }
            else
            {
                await Task.Delay(Math.Max(stepDelayMs / 2, 50), ct);
            }
        }
    }

    private static string ReadShellOutput(ShellStream shell, IProgress<string>? progress, int stepDelayMs, int maxWaitMs)
    {
        var output = new StringBuilder();
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);

        while (DateTime.UtcNow < deadline)
        {
            var text = shell.Read();
            if (!string.IsNullOrEmpty(text))
            {
                output.Append(text);
                progress?.Report(text);
                deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            }
            else
            {
                Thread.Sleep(Math.Max(stepDelayMs / 2, 50));
            }
        }

        return output.ToString();
    }

    private static async Task TelnetLoginAsync(
        NetworkStream stream,
        byte[] buffer,
        BatchCredential credential,
        int stepDelayMs,
        int maxWaitMs,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var loginOutput = new StringBuilder();
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, maxWaitMs, ct);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(stepDelayMs, ct);
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, maxWaitMs, ct);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(stepDelayMs, ct);
            await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, maxWaitMs, ct);
        }
    }
}
