namespace StartOpenClawLauncher.Models;

public sealed class ActionResult
{
    public bool Success { get; init; }
    public bool ShouldOpenOfficialPage { get; init; }
    public string? AccessUrl { get; init; }
    public TimeSpan? RunDuration { get; init; }
    public string Summary { get; init; } = string.Empty;
}
