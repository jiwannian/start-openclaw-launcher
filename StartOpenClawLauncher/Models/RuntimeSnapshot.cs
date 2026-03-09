namespace StartOpenClawLauncher.Models;

public sealed class RuntimeSnapshot
{
    public string? ClashPath { get; init; }
    public bool ClashInstalled { get; init; }
    public bool ClashRunning { get; init; }
    public bool ProxyReady { get; init; }
    public bool SystemProxyEnabled { get; init; }
    public string? OpenClawPath { get; init; }
    public bool OpenClawInstalled { get; init; }
    public bool OpenClawRunning { get; init; }
    public int? OpenClawPid { get; init; }
    public DateTimeOffset? OpenClawStartedAt { get; init; }
    public string OpenClawAccessUrl { get; init; } = string.Empty;
}
