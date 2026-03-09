using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using StartOpenClawLauncher.Models;

namespace StartOpenClawLauncher.Services;

public sealed class LauncherController
{
    private const string InternetSettingsRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const int InternetOptionRefresh = 37;
    private const int InternetOptionSettingsChanged = 39;

    private readonly LauncherSettings _settings;
    private readonly LauncherStateStore _stateStore;
    private readonly LocalizationService _localizer;

    public LauncherController(LauncherSettings settings, LauncherStateStore stateStore, LocalizationService localizer)
    {
        _settings = settings;
        _stateStore = stateStore;
        _localizer = localizer;
    }

    public async Task<RuntimeSnapshot> GetSnapshotAsync()
    {
        var state = _stateStore.Load();
        var clashPath = DetectClashPath();
        var openClawPath = await DetectOpenClawPathAsync();
        var openClawPid = await DetectOpenClawPidAsync();
        var gatewayReady = await IsPortOpenAsync(_settings.GatewayHost, _settings.GatewayPort);
        var openClawRunning = gatewayReady;

        return new RuntimeSnapshot
        {
            ClashPath = clashPath,
            ClashInstalled = !string.IsNullOrWhiteSpace(clashPath),
            ClashRunning = IsClashRunning(),
            ProxyReady = await IsPortOpenAsync(_settings.ProxyHost, _settings.ProxyPort),
            SystemProxyEnabled = IsSystemProxyEnabled(),
            OpenClawPath = openClawPath,
            OpenClawInstalled = !string.IsNullOrWhiteSpace(openClawPath),
            OpenClawRunning = openClawRunning,
            OpenClawPid = openClawRunning ? openClawPid : null,
            OpenClawStartedAt = openClawRunning ? state.OpenClawStartedAt : null,
            OpenClawAccessUrl = BuildOpenClawAccessUrl()
        };
    }

        public async Task<ActionResult> StartAsync(Func<string, Task> log)
    {
        if (_settings.StartClashIfInstalled)
        {
            var clashPath = DetectClashPath();
            if (!string.IsNullOrWhiteSpace(clashPath))
            {
                if (IsClashRunning())
                {
                    await log(_localizer.T("ClashRunningDetectedLog"));
                }
                else
                {
                    await log(_localizer.T("ClashDetectedLog", clashPath));
                    StartClash(clashPath);
                    await log(_localizer.T("ClashStartAttemptLog"));
                }

                var proxyReady = await WaitForPortAsync(
                    _settings.ProxyHost,
                    _settings.ProxyPort,
                    TimeSpan.FromSeconds(_settings.StartupTimeoutSeconds));

                if (proxyReady)
                {
                    await log(_localizer.T("ProxyReadyLog", _settings.ProxyHost, _settings.ProxyPort));

                    if (_settings.AutoEnableSystemProxy)
                    {
                        if (IsSystemProxyEnabled())
                        {
                            await log(_localizer.T("SystemProxyAlreadyLog", GetProxyServer()));
                        }
                        else if (EnableSystemProxy())
                        {
                            await log(_localizer.T("SystemProxyEnabledLog", GetProxyServer()));
                        }
                        else
                        {
                            await log(_localizer.T("SystemProxyEnableFailedLog"));
                        }
                    }
                }
                else
                {
                    await log(_localizer.T("ProxyNotReadyLog", _settings.ProxyHost, _settings.ProxyPort));
                }
            }
            else
            {
                await log(_localizer.T("ClashNotDetectedLog"));
            }
        }

        var openClawPath = await DetectOpenClawPathAsync();
        if (string.IsNullOrWhiteSpace(openClawPath))
        {
            return new ActionResult
            {
                Success = false,
                ShouldOpenOfficialPage = true,
                Summary = _localizer.T("OpenClawMissingSummary")
            };
        }

        var existingPid = await DetectOpenClawPidAsync();
        if (existingPid.HasValue)
        {
            var gatewayReady = await WaitForPortAsync(_settings.GatewayHost, _settings.GatewayPort, TimeSpan.FromSeconds(5));
            if (gatewayReady)
            {
                return new ActionResult
                {
                    Success = true,
                    AccessUrl = _settings.AutoOpenAccessPageOnStart ? BuildOpenClawAccessUrl() : null,
                    Summary = _localizer.T("OpenClawRunningReadySummary", existingPid.Value)
                };
            }

            await log(_localizer.T("SuspectedProcessLog", existingPid.Value));
        }

        var startedAt = DateTimeOffset.Now;
        var environment = BuildOpenClawEnvironment();
        var (fileName, arguments) = BuildCmdInvocation(openClawPath, $"gateway run --port {_settings.GatewayPort}");
        var process = CommandRunner.StartDetached(fileName, arguments, environment);
        if (process is null)
        {
            return new ActionResult
            {
                Success = false,
                Summary = _localizer.T("OpenClawCreateFailedSummary")
            };
        }

        _stateStore.Save(new LauncherState
        {
            OpenClawPid = process.Id,
            OpenClawStartedAt = startedAt
        });

        var ready = await WaitForPortAsync(
            _settings.GatewayHost,
            _settings.GatewayPort,
            TimeSpan.FromSeconds(_settings.StartupTimeoutSeconds));

        var actualPid = await DetectOpenClawPidAsync();
        _stateStore.Save(new LauncherState
        {
            OpenClawPid = actualPid ?? process.Id,
            OpenClawStartedAt = startedAt
        });

        return new ActionResult
        {
            Success = ready,
            AccessUrl = ready && _settings.AutoOpenAccessPageOnStart ? BuildOpenClawAccessUrl() : null,
            Summary = ready
                ? _localizer.T("OpenClawStartedSummary", BuildOpenClawAccessUrl())
                : _localizer.T("OpenClawStartedNotReadySummary", actualPid ?? process.Id)
        };
    }
    public async Task<ActionResult> StopAsync(Func<string, Task> log)
    {
        var state = _stateStore.Load();
        var runDuration = state.OpenClawStartedAt.HasValue
            ? DateTimeOffset.Now - state.OpenClawStartedAt.Value
            : (TimeSpan?)null;
        var openClawPath = await DetectOpenClawPathAsync();

        if (_settings.PreferDaemonStop && !string.IsNullOrWhiteSpace(openClawPath))
        {
            var (daemonFile, daemonArgs) = BuildCmdInvocation(openClawPath, "daemon stop");
            var daemonResult = await CommandRunner.RunAsync(daemonFile, daemonArgs, timeoutMs: 12000);
            if (daemonResult.ExitCode == 0)
            {
                await log(_localizer.T("DaemonStopLog"));
            }
        }

        if (state.OpenClawPid.HasValue)
        {
            await log(_localizer.T("KillRecordedPidLog", state.OpenClawPid.Value));
            CommandRunner.TryKillProcessTree(state.OpenClawPid.Value);
        }

        foreach (var pid in await GetOwningProcessIdsByPortAsync(_settings.GatewayPort))
        {
            if (await IsOpenClawProcessAsync(pid))
            {
                await log(_localizer.T("KillPortPidLog", _settings.GatewayPort, pid));
                CommandRunner.TryKillProcessTree(pid);
            }
        }

        foreach (var pid in await FindOpenClawGatewayProcessIdsAsync())
        {
            await log(_localizer.T("KillGatewayPidLog", pid));
            CommandRunner.TryKillProcessTree(pid);
        }

        var gatewayClosed = await WaitForPortClosedAsync(
            _settings.GatewayHost,
            _settings.GatewayPort,
            TimeSpan.FromSeconds(Math.Min(_settings.StartupTimeoutSeconds, 15)));
        if (gatewayClosed)
        {
            _stateStore.Clear();
            return new ActionResult
            {
                Success = true,
                RunDuration = runDuration,
                Summary = runDuration.HasValue
                    ? _localizer.T("OpenClawStoppedDurationSummary", FormatDuration(runDuration.Value))
                    : _localizer.T("OpenClawStoppedSummary")
            };
        }

        return new ActionResult
        {
            Success = false,
            RunDuration = runDuration,
            Summary = _localizer.T("OpenClawStopNotConfirmedSummary")
        };
    }
    public void OpenOfficialOpenClawPage()
    {
        OpenUrl(_settings.OpenClawOfficialUrl);
    }

    public void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private IDictionary<string, string?>? BuildOpenClawEnvironment()
    {
        if (!_settings.InjectProxyEnvironment)
        {
            return null;
        }

        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["HTTP_PROXY"] = $"http://{_settings.ProxyHost}:{_settings.ProxyPort}",
            ["HTTPS_PROXY"] = $"http://{_settings.ProxyHost}:{_settings.ProxyPort}",
            ["ALL_PROXY"] = $"http://{_settings.ProxyHost}:{_settings.ProxyPort}",
            ["NO_PROXY"] = "localhost,127.0.0.1"
        };
    }

    private string BuildOpenClawAccessUrl()
    {
        return $"http://{_settings.GatewayHost}:{_settings.GatewayPort}/";
    }

    private string GetProxyServer()
    {
        return $"{_settings.ProxyHost}:{_settings.ProxyPort}";
    }

    private bool EnableSystemProxy()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsRegistryPath, true);
            if (key is null)
            {
                return false;
            }

            key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
            key.SetValue("ProxyServer", GetProxyServer(), RegistryValueKind.String);

            var existingOverride = key.GetValue("ProxyOverride")?.ToString();
            key.SetValue("ProxyOverride", MergeProxyOverride(existingOverride), RegistryValueKind.String);

            RefreshInternetSettings();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsSystemProxyEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsRegistryPath, false);
            if (key is null)
            {
                return false;
            }

            var proxyEnable = Convert.ToInt32(key.GetValue("ProxyEnable", 0)) == 1;
            var proxyServer = key.GetValue("ProxyServer")?.ToString();

            return proxyEnable
                   && !string.IsNullOrWhiteSpace(proxyServer)
                   && proxyServer.Contains(GetProxyServer(), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string MergeProxyOverride(string? existingOverride)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (existingOverride ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            values.Add(item.Trim());
        }

        values.Add("<local>");
        values.Add("localhost");
        values.Add("127.0.0.1");

        return string.Join(';', values);
    }

    private static void RefreshInternetSettings()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }

    private void StartClash(string clashPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = clashPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Minimized,
            WorkingDirectory = Path.GetDirectoryName(clashPath) ?? AppContext.BaseDirectory
        });
    }

    private async Task<string?> DetectOpenClawPathAsync()
    {
        var whereResults = await LocateViaWhereAsync("openclaw.cmd");
        var located = whereResults.FirstOrDefault(path => path.EndsWith("openclaw.cmd", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(located))
        {
            return located;
        }

        foreach (var candidate in _settings.OpenClawCandidates)
        {
            var expanded = Environment.ExpandEnvironmentVariables(candidate);
            if (File.Exists(expanded))
            {
                return expanded;
            }
        }

        return null;
    }

    private string? DetectClashPath()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (!_settings.ClashProcessKeywords.Any(keyword => process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var mainModulePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath) && File.Exists(mainModulePath))
                {
                    return mainModulePath;
                }
            }
            catch
            {
            }
        }

        foreach (var candidate in _settings.ClashCandidates)
        {
            var expanded = Environment.ExpandEnvironmentVariables(candidate);
            if (File.Exists(expanded))
            {
                return expanded;
            }
        }

        return FindClashPathFromRegistry();
    }

    private string? FindClashPathFromRegistry()
    {
        var locations = new[]
        {
            (RegistryHive.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall")
        };

        foreach (var (hive, path) in locations)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var uninstall = baseKey.OpenSubKey(path);
                if (uninstall is null)
                {
                    continue;
                }

                foreach (var subKeyName in uninstall.GetSubKeyNames())
                {
                    using var appKey = uninstall.OpenSubKey(subKeyName);
                    var displayName = appKey?.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(displayName) || !_settings.ClashProcessKeywords.Any(keyword => displayName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var displayIcon = appKey?.GetValue("DisplayIcon")?.ToString();
                    var installLocation = appKey?.GetValue("InstallLocation")?.ToString();
                    var candidate = NormalizeExecutablePath(displayIcon) ?? FindExecutableInDirectory(installLocation);
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? FindExecutableInDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        return Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(file => file.Contains("clash", StringComparison.OrdinalIgnoreCase)
                                 || file.Contains("verge", StringComparison.OrdinalIgnoreCase)
                                 || file.Contains("mihomo", StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeExecutablePath(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var cleaned = rawPath.Trim().Trim('"');
        var commaIndex = cleaned.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex > 0)
        {
            cleaned = cleaned[..commaIndex];
        }

        return File.Exists(cleaned) ? cleaned : null;
    }

    private bool IsClashRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (_settings.ClashProcessKeywords.Any(keyword => process.ProcessName.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
            catch
            {
            }
        }

        return false;
    }

    private async Task<int?> DetectOpenClawPidAsync()
    {
        var state = _stateStore.Load();
        if (state.OpenClawPid.HasValue && await IsOpenClawProcessAsync(state.OpenClawPid.Value))
        {
            return state.OpenClawPid.Value;
        }

        foreach (var pid in await GetOwningProcessIdsByPortAsync(_settings.GatewayPort))
        {
            if (await IsOpenClawProcessAsync(pid))
            {
                return pid;
            }
        }

        return (await FindOpenClawGatewayProcessIdsAsync()).FirstOrDefault();
    }

    private async Task<List<int>> FindOpenClawGatewayProcessIdsAsync()
    {
        const string script = "Get-CimInstance Win32_Process | Where-Object { $_.Name -notmatch '^(powershell|pwsh)\\.exe$' -and $_.CommandLine -match 'openclaw' -and $_.CommandLine -match 'gateway' } | Select-Object -ExpandProperty ProcessId";
        var result = await CommandRunner.RunAsync("powershell.exe", $"-NoProfile -Command \"{script}\"", timeoutMs: 10000);
        return ParseProcessIds(result.StdOut);
    }

    private async Task<bool> IsOpenClawProcessAsync(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process.HasExited)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        var commandLine = await GetProcessCommandLineAsync(pid);
        return !string.IsNullOrWhiteSpace(commandLine)
               && ((commandLine.Contains("openclaw", StringComparison.OrdinalIgnoreCase)
                    && commandLine.Contains("gateway", StringComparison.OrdinalIgnoreCase))
                   || commandLine.Contains("dist\\index.js gateway", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> GetProcessCommandLineAsync(int pid)
    {
        var script = $"(Get-CimInstance Win32_Process -Filter \"ProcessId = {pid}\" | Select-Object -ExpandProperty CommandLine)";
        var result = await CommandRunner.RunAsync("powershell.exe", $"-NoProfile -Command \"{script}\"", timeoutMs: 8000);
        return string.IsNullOrWhiteSpace(result.StdOut) ? null : result.StdOut.Trim();
    }

    private async Task<List<int>> GetOwningProcessIdsByPortAsync(int port)
    {
        var script = $"Get-NetTCPConnection -State Listen -LocalPort {port} -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique";
        var result = await CommandRunner.RunAsync("powershell.exe", $"-NoProfile -Command \"{script}\"", timeoutMs: 8000);
        return ParseProcessIds(result.StdOut);
    }

    private static List<int> ParseProcessIds(string raw)
    {
        return raw.Split(new[] { '\r', '\n', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => int.TryParse(value, out var pid) ? pid : (int?)null)
            .Where(pid => pid.HasValue)
            .Select(pid => pid!.Value)
            .Distinct()
            .ToList();
    }

    private async Task<List<string>> LocateViaWhereAsync(string commandName)
    {
        var result = await CommandRunner.RunAsync("where.exe", commandName, timeoutMs: 5000);
        return result.StdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(File.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static (string FileName, string Arguments) BuildCmdInvocation(string scriptPath, string scriptArguments)
    {
        return ("cmd.exe", $"/c \"\"{scriptPath}\" {scriptArguments}\"");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port)
    {
        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await client.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> WaitForPortAsync(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await IsPortOpenAsync(host, port))
            {
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }

    private static async Task<bool> WaitForPortClosedAsync(string host, int port, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (!await IsPortOpenAsync(host, port))
            {
                return true;
            }

            await Task.Delay(1000);
        }

        return false;
    }

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);
}

