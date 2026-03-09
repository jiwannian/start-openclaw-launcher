using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using StartOpenClawLauncher.Models;
using StartOpenClawLauncher.Services;

namespace StartOpenClawLauncher;

public partial class MainWindow : Window
{
    private readonly LauncherConfigService _configService;
    private readonly LauncherStateStore _stateStore;
    private readonly FileLogService _fileLogService;
    private readonly LauncherController _controller;
    private readonly LauncherSettings _settings;
    private readonly DispatcherTimer _runtimeTimer;

    private bool _isBusy;
    private bool _isOpenClawRunning;
    private DateTimeOffset? _currentOpenClawStartedAt;
    private TimeSpan? _lastRunDuration;

    public MainWindow()
    {
        InitializeComponent();
        _configService = new LauncherConfigService();
        _stateStore = new LauncherStateStore();
        _fileLogService = new FileLogService();
        _settings = _configService.LoadOrCreate();
        _controller = new LauncherController(_settings, _stateStore);
        _runtimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _runtimeTimer.Tick += RuntimeTimer_Tick;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshSnapshotAsync();
        _runtimeTimer.Start();
        AppendLog("启动器已就绪。配置文件位于 exe 同目录下的 config.json。", false);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _runtimeTimer.Stop();
    }

    private void RuntimeTimer_Tick(object? sender, EventArgs e)
    {
        UpdateRuntimeDisplay();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        await RunBusyAsync(async () =>
        {
            AppendLog("开始执行一键启动流程...", false);
            var result = await _controller.StartAsync(LogAsync);
            AppendLog(result.Summary, !result.Success);

            if (result.ShouldOpenOfficialPage)
            {
                MessageBox.Show(this, "未检测到 OpenClaw，接下来会打开官方安装页面。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                _controller.OpenOfficialOpenClawPage();
                return;
            }

            if (result.Success && !string.IsNullOrWhiteSpace(result.AccessUrl))
            {
                AppendLog($"正在打开 OpenClaw 访问地址：{result.AccessUrl}", false);
                _controller.OpenUrl(result.AccessUrl);
            }
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        await RunBusyAsync(async () =>
        {
            AppendLog("开始执行一键关闭流程...", false);
            var result = await _controller.StopAsync(LogAsync);
            _lastRunDuration = result.RunDuration;
            AppendLog(result.Summary, !result.Success);
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        try
        {
            SetBusy(true);
            await action();
        }
        catch (Exception ex)
        {
            AppendLog($"发生未处理异常：{ex.Message}", true);
            MessageBox.Show(this, ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            await RefreshSnapshotAsync();
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _isBusy = isBusy;
        StartButton.IsEnabled = !isBusy;
        StopButton.IsEnabled = !isBusy;
        HintTextBlock.Text = isBusy
            ? "正在执行，请稍候..."
            : "启动按钮会自动拉起 Clash、自动开启系统代理，并在确认 OpenClaw 运行后自动打开访问地址。";
    }

    private async Task RefreshSnapshotAsync()
    {
        var snapshot = await _controller.GetSnapshotAsync();

        SetStatus(
            ClashStatusTextBlock,
            ClashDetailTextBlock,
            snapshot.ClashInstalled ? (snapshot.ClashRunning ? "已安装 / 运行中" : "已安装 / 未运行") : "未检测到",
            snapshot.ClashInstalled ? snapshot.ClashPath ?? "已检测到，但路径未知" : "未在常见路径、进程或注册表中检测到 Clash。",
            !snapshot.ClashInstalled);

        var proxyStatus = snapshot.ProxyReady
            ? snapshot.SystemProxyEnabled
                ? "代理可用 / 已开启系统代理"
                : "代理可用 / 系统代理未开启"
            : "代理未就绪";
        var proxyDetail = $"端口：{_settings.ProxyHost}:{_settings.ProxyPort} | 系统代理：{(snapshot.SystemProxyEnabled ? "已开启" : "未开启")}";
        SetStatus(
            ProxyStatusTextBlock,
            ProxyDetailTextBlock,
            proxyStatus,
            proxyDetail,
            !snapshot.ProxyReady || !snapshot.SystemProxyEnabled);

        SetStatus(
            OpenClawStatusTextBlock,
            OpenClawDetailTextBlock,
            snapshot.OpenClawInstalled ? (snapshot.OpenClawRunning ? "已安装 / 运行中" : "已安装 / 未运行") : "未检测到",
            snapshot.OpenClawInstalled
                ? $"路径：{snapshot.OpenClawPath}{(snapshot.OpenClawPid.HasValue ? $" | PID: {snapshot.OpenClawPid.Value}" : string.Empty)}"
                : "未在 PATH 或常见 npm 全局目录中检测到 openclaw.cmd。",
            !snapshot.OpenClawInstalled);

        AccessUrlTextBlock.Text = snapshot.OpenClawAccessUrl;
        AccessUrlTextBlock.Foreground = snapshot.OpenClawRunning
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : new SolidColorBrush(Color.FromRgb(107, 114, 128));

        _isOpenClawRunning = snapshot.OpenClawRunning;
        _currentOpenClawStartedAt = snapshot.OpenClawStartedAt;
        if (snapshot.OpenClawRunning)
        {
            _lastRunDuration = null;
        }

        UpdateRuntimeDisplay();
    }

    private void UpdateRuntimeDisplay()
    {
        if (_isOpenClawRunning)
        {
            RuntimeValueTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));

            if (_currentOpenClawStartedAt.HasValue)
            {
                var duration = DateTimeOffset.Now - _currentOpenClawStartedAt.Value;
                RuntimeValueTextBlock.Text = FormatDuration(duration);
                RuntimeDetailTextBlock.Text = $"启动时间：{_currentOpenClawStartedAt.Value:yyyy-MM-dd HH:mm:ss}";
                return;
            }

            RuntimeValueTextBlock.Text = "正在运行（启动时间未知）";
            RuntimeDetailTextBlock.Text = "该进程可能不是由本启动器拉起，因此无法准确统计总时长。";
            return;
        }

        RuntimeValueTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39));

        if (_lastRunDuration.HasValue)
        {
            RuntimeValueTextBlock.Text = $"上次运行：{FormatDuration(_lastRunDuration.Value)}";
            RuntimeDetailTextBlock.Text = "OpenClaw 当前未运行。";
            return;
        }

        RuntimeValueTextBlock.Text = "未运行";
        RuntimeDetailTextBlock.Text = "启动后将自动开始计时。";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static void SetStatus(System.Windows.Controls.TextBlock titleBlock, System.Windows.Controls.TextBlock detailBlock, string title, string detail, bool isWarning)
    {
        titleBlock.Text = title;
        titleBlock.Foreground = isWarning
            ? new SolidColorBrush(Color.FromRgb(220, 38, 38))
            : new SolidColorBrush(Color.FromRgb(37, 99, 235));
        detailBlock.Text = detail;
    }

    private Task LogAsync(string message)
    {
        AppendLog(message, false);
        return Task.CompletedTask;
    }

    private void AppendLog(string message, bool isError)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendLog(message, isError));
            return;
        }

        var line = $"[{DateTime.Now:HH:mm:ss}] {(isError ? "[错误] " : string.Empty)}{message}";
        _fileLogService.WriteLine(line);

        var builder = new StringBuilder(LogTextBox.Text);
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
        LogTextBox.Text = builder.ToString();
        LogTextBox.ScrollToEnd();
    }
}
