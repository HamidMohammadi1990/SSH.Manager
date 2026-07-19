using System.Net.Sockets;
using System.Text;
using SshManager.Models;

namespace SshManager.Services;

public class TelnetExecutor
{
    public async Task<CommandExecutionResult> ExecuteCommandAsync(
        ServerProfile server,
        CommandItem command,
        AppSettings settings,
        IProgress<string>? outputProgress = null,
        CancellationToken ct = default)
    {
        var result = new CommandExecutionResult
        {
            CommandId = command.Id,
            CommandText = command.Text,
            StartedAt = DateTime.Now,
            Status = ExecutionStatus.Running
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds));

            await client.ConnectAsync(server.Host, server.Port, timeoutCts.Token);

            using var stream = client.GetStream();
            var buffer = new byte[4096];
            var output = new StringBuilder();

            await ReadAvailableAsync(stream, buffer, output, outputProgress, timeoutCts.Token);

            var commandBytes = Encoding.ASCII.GetBytes(command.Text + "\r\n");
            await stream.WriteAsync(commandBytes, timeoutCts.Token);

            await Task.Delay(500, timeoutCts.Token);
            await ReadAvailableAsync(stream, buffer, output, outputProgress, timeoutCts.Token);

            result.Output = output.ToString().TrimEnd();
            result.Status = ExecutionStatus.Success;
        }
        catch (OperationCanceledException)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = "Execution was cancelled.";
        }
        catch (Exception ex)
        {
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        result.FinishedAt = DateTime.Now;
        return result;
    }

    private static async Task ReadAvailableAsync(
        NetworkStream stream,
        byte[] buffer,
        StringBuilder output,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        while (stream.DataAvailable)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0) break;

            var text = Encoding.ASCII.GetString(buffer, 0, read);
            output.Append(text);
            progress?.Report(text);
        }
    }
}
