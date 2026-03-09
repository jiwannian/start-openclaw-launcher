using System.Text;
using System.Windows;
using System.Windows.Controls;
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
    private readonly LocalizationService _localizer;
    private readonly DispatcherTimer _runtimeTimer;

    private bool _isBusy;
    private bool _isOpenClawRunning;
    private bool _isLanguageSelectionUpdating;
    private DateTimeOffset? _currentOpenClawStartedAt;
    private TimeSpan? _lastRunDuration;

    public MainWindow()
    {
        InitializeComponent();
        _configService = new LauncherConfigService();
        _stateStore = new LauncherStateStore();
        _fileLogService = new FileLogService();
        _settings = _configService.LoadOrCreate();
        _localizer = new LocalizationService();
        _localizer.SetLanguage(_settings.LanguageCode);
        _controller = new LauncherController(_settings, _stateStore, _localizer);
        _runtimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _runtimeTimer.Tick += RuntimeTimer_Tick;
        PopulateLanguageOptions();
        ApplyLocalization();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    private void PopulateLanguageOptions()
    {
        _isLanguageSelectionUpdating = true;
        LanguageComboBox.ItemsSource = _localizer.Languages;
        LanguageComboBox.SelectedItem = _localizer.Languages.FirstOrDefault(language => language.Code.Equals(_settings.LanguageCode, StringComparison.OrdinalIgnoreCase))
            ?? _localizer.Languages.First(language => language.Code == "en");
        _isLanguageSelectionUpdating = false;
    }

    private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLanguageSelectionUpdating || LanguageComboBox.SelectedItem is not LanguageOption option)
        {
            return;
        }

        _settings.LanguageCode = option.Code;
        _configService.Save(_settings);
        _localizer.SetLanguage(option.Code);
        ApplyLocalization();
        await RefreshSnapshotAsync();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshSnapshotAsync();
        _runtimeTimer.Start();
        AppendLog(_localizer.T("ReadyLog"), false);
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
            AppendLog(_localizer.T("StartFlowLog"), false);
            var result = await _controller.StartAsync(LogAsync);
            AppendLog(result.Summary, !result.Success);

            if (result.ShouldOpenOfficialPage)
            {
                MessageBox.Show(this, _localizer.T("NotInstalledMessage"), _localizer.T("InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                _controller.OpenOfficialOpenClawPage();
                return;
            }

            if (result.Success && !string.IsNullOrWhiteSpace(result.AccessUrl))
            {
                AppendLog(_localizer.T("OpeningUrlLog", result.AccessUrl), false);
                _controller.OpenUrl(result.AccessUrl);
            }
        });
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;

        await RunBusyAsync(async () =>
        {
            AppendLog(_localizer.T("StopFlowLog"), false);
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
            AppendLog(_localizer.T("UnhandledException", ex.Message), true);
            MessageBox.Show(this, ex.Message, _localizer.T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
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
        HintTextBlock.Text = _localizer.T(isBusy ? "HintBusy" : "HintIdle");
    }

    private void ApplyLocalization()
    {
        Title = _localizer.T("WindowTitle");
        LanguageLabelTextBlock.Text = _localizer.T("LanguageLabel");
        HeaderTitleTextBlock.Text = _localizer.T("HeaderTitle");
        HeaderSubtitleTextBlock.Text = _localizer.T("HeaderSubtitle");
        ClashCardTitleTextBlock.Text = _localizer.T("CardClash");
        ProxyCardTitleTextBlock.Text = _localizer.T("CardProxy");
        OpenClawCardTitleTextBlock.Text = _localizer.T("CardOpenClaw");
        StartButton.Content = _localizer.T("ButtonStart");
        StopButton.Content = _localizer.T("ButtonStop");
        HintTextBlock.Text = _localizer.T(_isBusy ? "HintBusy" : "HintIdle");
        AccessCardTitleTextBlock.Text = _localizer.T("CardAccess");
        AccessDescriptionTextBlock.Text = _localizer.T("AccessDescription");
        RuntimeCardTitleTextBlock.Text = _localizer.T("CardRuntime");
        LogsTitleTextBlock.Text = _localizer.T("LogsTitle");
        UpdateRuntimeDisplay();
    }

    private async Task RefreshSnapshotAsync()
    {
        var snapshot = await _controller.GetSnapshotAsync();

        SetStatus(
            ClashStatusTextBlock,
            ClashDetailTextBlock,
            snapshot.ClashInstalled ? (snapshot.ClashRunning ? _localizer.T("InstalledRunning") : _localizer.T("InstalledNotRunning")) : _localizer.T("NotDetected"),
            snapshot.ClashInstalled ? snapshot.ClashPath ?? _localizer.T("DetectedPathUnknown") : _localizer.T("ClashMissingDetail"),
            !snapshot.ClashInstalled);

        var proxyStatus = snapshot.ProxyReady
            ? snapshot.SystemProxyEnabled
                ? _localizer.T("ProxyReadySystemEnabled")
                : _localizer.T("ProxyReadySystemDisabled")
            : _localizer.T("ProxyNotReady");

        var proxyDetail = _localizer.T(
            "ProxyDetail",
            _settings.ProxyHost,
            _settings.ProxyPort,
            _localizer.T(snapshot.SystemProxyEnabled ? "SystemProxyEnabled" : "SystemProxyDisabled"));

        SetStatus(
            ProxyStatusTextBlock,
            ProxyDetailTextBlock,
            proxyStatus,
            proxyDetail,
            !snapshot.ProxyReady || !snapshot.SystemProxyEnabled);

        var openClawPathSuffix = snapshot.OpenClawPid.HasValue ? $" | PID: {snapshot.OpenClawPid.Value}" : string.Empty;
        SetStatus(
            OpenClawStatusTextBlock,
            OpenClawDetailTextBlock,
            snapshot.OpenClawInstalled ? (snapshot.OpenClawRunning ? _localizer.T("InstalledRunning") : _localizer.T("InstalledNotRunning")) : _localizer.T("NotDetected"),
            snapshot.OpenClawInstalled
                ? _localizer.T("OpenClawPathDetail", snapshot.OpenClawPath ?? _localizer.T("DetectedPathUnknown"), openClawPathSuffix)
                : _localizer.T("OpenClawMissingDetail"),
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
                RuntimeDetailTextBlock.Text = _localizer.T("RuntimeStartedAt", _currentOpenClawStartedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                return;
            }

            RuntimeValueTextBlock.Text = _localizer.T("RuntimeUnknown");
            RuntimeDetailTextBlock.Text = _localizer.T("RuntimeUnknownDetail");
            return;
        }

        RuntimeValueTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(17, 24, 39));

        if (_lastRunDuration.HasValue)
        {
            RuntimeValueTextBlock.Text = _localizer.T("RuntimeLastRun", FormatDuration(_lastRunDuration.Value));
            RuntimeDetailTextBlock.Text = _localizer.T("RuntimeStoppedDetail");
            return;
        }

        RuntimeValueTextBlock.Text = _localizer.T("RuntimeNotRunning");
        RuntimeDetailTextBlock.Text = _localizer.T("RuntimeIdleDescription");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private static void SetStatus(TextBlock titleBlock, TextBlock detailBlock, string title, string detail, bool isWarning)
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

        var line = $"[{DateTime.Now:HH:mm:ss}] {(isError ? _localizer.T("ErrorPrefix") : string.Empty)}{message}";
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
