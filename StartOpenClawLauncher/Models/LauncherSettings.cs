namespace StartOpenClawLauncher.Models;

public sealed class LauncherSettings
{
    public int ProxyPort { get; set; } = 8090;
    public string ProxyHost { get; set; } = "127.0.0.1";
    public string GatewayHost { get; set; } = "127.0.0.1";
    public int GatewayPort { get; set; } = 18789;
    public int StartupTimeoutSeconds { get; set; } = 25;
    public bool StartClashIfInstalled { get; set; } = true;
    public bool AutoEnableSystemProxy { get; set; } = true;
    public bool AutoOpenAccessPageOnStart { get; set; } = true;
    public bool InjectProxyEnvironment { get; set; } = true;
    public bool PreferDaemonStop { get; set; } = true;
    public string OpenClawOfficialUrl { get; set; } = "https://docs.openclaw.ai/install/index";
    public string[] OpenClawCandidates { get; set; } =
    [
        "%APPDATA%\\npm\\openclaw.cmd",
        "%USERPROFILE%\\AppData\\Roaming\\npm\\openclaw.cmd",
        "%LOCALAPPDATA%\\Programs\\OpenClaw\\openclaw.cmd",
        "%ProgramFiles%\\OpenClaw\\openclaw.cmd",
        "D:\\tool\\nodejs\\node_global\\openclaw.cmd"
    ];

    public string[] ClashCandidates { get; set; } =
    [
        "%LOCALAPPDATA%\\Programs\\Clash for Windows\\Clash for Windows.exe",
        "%LOCALAPPDATA%\\Programs\\Clash Verge\\Clash Verge.exe",
        "%LOCALAPPDATA%\\Programs\\Clash Verge Rev\\Clash Verge.exe",
        "%LOCALAPPDATA%\\Programs\\Mihomo Party\\Mihomo Party.exe",
        "%ProgramFiles%\\Clash for Windows\\Clash for Windows.exe",
        "%ProgramFiles%\\Clash Verge\\Clash Verge.exe"
    ];

    public string[] ClashProcessKeywords { get; set; } =
    [
        "clash",
        "verge",
        "mihomo",
        "nyanpasu"
    ];
}
