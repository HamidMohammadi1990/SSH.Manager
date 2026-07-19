using System.Text;
using Renci.SshNet;
using SshManager.Models;

namespace SshManager.Services;

public class SshExecutor
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
            var (username, password, keyPath) = ConnectionTestService.ResolveCredentials(server, settings);

            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();

                using var client = ConnectionTestService.CreateSshClient(
                    server, username, password, keyPath, settings.ConnectionTimeoutSeconds);
                client.Connect();

                if (!client.IsConnected)
                    throw new InvalidOperationException("Failed to establish SSH connection.");

                using var cmd = client.CreateCommand(command.Text);
                cmd.CommandTimeout = TimeSpan.FromSeconds(settings.CommandTimeoutSeconds);

                var output = cmd.Execute();
                outputProgress?.Report(output);

                if (!string.IsNullOrEmpty(cmd.Error))
                {
                    output += Environment.NewLine + "[STDERR] " + cmd.Error;
                    outputProgress?.Report("[STDERR] " + cmd.Error);
                }

                result.Output = output.TrimEnd();
                result.Status = cmd.ExitStatus == 0 ? ExecutionStatus.Success : ExecutionStatus.Failed;

                if (cmd.ExitStatus != 0)
                    result.ErrorMessage = $"Command exited with code {cmd.ExitStatus}";

                client.Disconnect();
            }, ct);
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
}
