using System.IO;
using System.Text.Json;
using StartOpenClawLauncher.Models;

namespace StartOpenClawLauncher.Services;

public sealed class LauncherConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _configPath = Path.Combine(AppContext.BaseDirectory, "config.json");

    public LauncherSettings LoadOrCreate()
    {
        if (!File.Exists(_configPath))
        {
            var defaults = new LauncherSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions) ?? new LauncherSettings();
        }
        catch
        {
            var fallback = new LauncherSettings();
            Save(fallback);
            return fallback;
        }
    }

    public void Save(LauncherSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_configPath, json);
    }
}
