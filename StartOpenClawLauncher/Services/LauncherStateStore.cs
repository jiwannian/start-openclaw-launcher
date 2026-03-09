using System.IO;
using System.Text.Json;
using StartOpenClawLauncher.Models;

namespace StartOpenClawLauncher.Services;

public sealed class LauncherStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _statePath = Path.Combine(AppContext.BaseDirectory, "launcher-state.json");

    public LauncherState Load()
    {
        if (!File.Exists(_statePath))
        {
            return new LauncherState();
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherState>(File.ReadAllText(_statePath), JsonOptions) ?? new LauncherState();
        }
        catch
        {
            return new LauncherState();
        }
    }

    public void Save(LauncherState state)
    {
        File.WriteAllText(_statePath, JsonSerializer.Serialize(state, JsonOptions));
    }

    public void Clear()
    {
        if (File.Exists(_statePath))
        {
            File.Delete(_statePath);
        }
    }
}
