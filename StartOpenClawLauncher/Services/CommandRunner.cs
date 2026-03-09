using System.Diagnostics;
using System.Text;

namespace StartOpenClawLauncher.Services;

public static class CommandRunner
{
    public static async Task<CommandResult> RunAsync(
        string fileName,
        string arguments,
        int timeoutMs = 15000,
        IDictionary<string, string?>? environment = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                process.StartInfo.Environment[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var delayTask = Task.Delay(timeoutMs, cancellationToken);

        var completedTask = await Task.WhenAny(Task.WhenAll(waitTask, stdOutTask, stdErrTask), delayTask);
        if (completedTask == delayTask)
        {
            TryKillProcessTree(process.Id);
            return new CommandResult
            {
                ExitCode = -1,
                StdOut = string.Empty,
                StdErr = "命令执行超时。",
                TimedOut = true
            };
        }

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            StdOut = await stdOutTask,
            StdErr = await stdErrTask
        };
    }

    public static Process? StartDetached(
        string fileName,
        string arguments,
        IDictionary<string, string?>? environment = null,
        string? workingDirectory = null)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory
            }
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                process.StartInfo.Environment[pair.Key] = pair.Value ?? string.Empty;
            }
        }

        return process.Start() ? process : null;
    }

    public static void TryKillProcessTree(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(true);
        }
        catch
        {
        }
    }
}
