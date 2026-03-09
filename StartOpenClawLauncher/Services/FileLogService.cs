using System.IO;

namespace StartOpenClawLauncher.Services;

public sealed class FileLogService
{
    private readonly string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

    public FileLogService()
    {
        Directory.CreateDirectory(_logDirectory);
    }

    public void WriteLine(string message)
    {
        var path = Path.Combine(_logDirectory, $"launcher-{DateTime.Now:yyyyMMdd}.log");
        File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
