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
        IProgress<string>? outputProgress = null,
        Action<BatchStep>? onStepStarted = null,
        CancellationToken ct = default)
    {
        return server.ConnectionType switch
        {
            ConnectionType.Telnet => await ExecuteTelnetStepsAsync(
                server, credential, steps, stepDelayMs, outputProgress, onStepStarted, ct),
            ConnectionType.Ssh => await ExecuteSshStepsAsync(
                server, credential, steps, stepDelayMs, outputProgress, onStepStarted, ct),
            _ => throw new NotSupportedException($"Unsupported connection type: {server.ConnectionType}")
        };
    }

    private static async Task<List<CommandExecutionResult>> ExecuteTelnetStepsAsync(
        ServerProfile server,
        BatchCredential credential,
        IReadOnlyList<BatchStep> steps,
        int stepDelayMs,
        IProgress<string>? outputProgress,
        Action<BatchStep>? onStepStarted,
        CancellationToken ct)
    {
        var results = new List<CommandExecutionResult>();

        using var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        await client.ConnectAsync(server.Host, server.Port, timeoutCts.Token);
        await using var stream = client.GetStream();
        var buffer = new byte[4096];
        var sessionOutput = new StringBuilder();

        await ReadTelnetAsync(stream, buffer, sessionOutput, outputProgress, stepDelayMs, timeoutCts.Token);

        if (!string.IsNullOrWhiteSpace(credential.Username))
            await TelnetLoginAsync(stream, buffer, credential, stepDelayMs, outputProgress, timeoutCts.Token);

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
                await stream.WriteAsync(bytes, timeoutCts.Token);
                await Task.Delay(stepDelayMs, timeoutCts.Token);

                var stepOutput = new StringBuilder();
                await ReadTelnetAsync(stream, buffer, stepOutput, outputProgress, stepDelayMs, timeoutCts.Token);

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
        IProgress<string>? outputProgress,
        Action<BatchStep>? onStepStarted,
        CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var results = new List<CommandExecutionResult>();
            ct.ThrowIfCancellationRequested();

            using var client = ConnectionTestService.CreateSshClient(
                server, credential.Username, credential.Password, null, 30);
            client.Connect();

            if (!client.IsConnected)
                throw new InvalidOperationException("Failed to establish SSH connection.");

            using var shell = client.CreateShellStream("vt100", 120, 40, 800, 600, 4096);
            ReadShellOutput(shell, outputProgress, stepDelayMs);

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

                    var output = ReadShellOutput(shell, outputProgress, stepDelayMs);
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
        CancellationToken ct)
    {
        var idleRounds = 0;
        const int maxIdleRounds = 4;

        while (idleRounds < maxIdleRounds)
        {
            if (stream.DataAvailable)
            {
                idleRounds = 0;
                var read = await stream.ReadAsync(buffer, ct);
                if (read == 0) break;

                var text = Encoding.ASCII.GetString(buffer, 0, read);
                output.Append(text);
                progress?.Report(text);
            }
            else
            {
                idleRounds++;
                await Task.Delay(stepDelayMs / 2, ct);
            }
        }
    }

    private static string ReadShellOutput(ShellStream shell, IProgress<string>? progress, int stepDelayMs)
    {
        var output = new StringBuilder();
        var idleRounds = 0;
        const int maxIdleRounds = 6;

        while (idleRounds < maxIdleRounds)
        {
            var text = shell.Read();
            if (!string.IsNullOrEmpty(text))
            {
                idleRounds = 0;
                output.Append(text);
                progress?.Report(text);
            }
            else
            {
                idleRounds++;
                Thread.Sleep(stepDelayMs / 2);
            }
        }

        return output.ToString();
    }

    private static async Task TelnetLoginAsync(
        NetworkStream stream,
        byte[] buffer,
        BatchCredential credential,
        int stepDelayMs,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var loginOutput = new StringBuilder();
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, ct);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Username + "\r\n"), ct);
        await Task.Delay(stepDelayMs, ct);
        await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, ct);

        if (!string.IsNullOrEmpty(credential.Password))
        {
            await stream.WriteAsync(Encoding.ASCII.GetBytes(credential.Password + "\r\n"), ct);
            await Task.Delay(stepDelayMs, ct);
            await ReadTelnetAsync(stream, buffer, loginOutput, progress, stepDelayMs, ct);
        }
    }
}
