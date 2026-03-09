namespace StartOpenClawLauncher.Models;

public sealed class LauncherState
{
    public int? OpenClawPid { get; set; }
    public DateTimeOffset? OpenClawStartedAt { get; set; }
}
