using System.Globalization;
using Amplify.Core.Auth;
using Amplify.Core.Settings;
using Amplify.Core.Tray;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs the settings screen: it exposes the editable preferences as bindable properties, persisting
/// each change through <see cref="ISettingsService"/>, and surfaces a read-only view of the connected
/// account and the stored Client ID. The account and reset actions themselves are owned by other
/// features; here they are presented for context only.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IAuthService _auth;
    private readonly IStartupTaskManager _startupTasks;
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();

    // Guards the bindable properties while they are being populated from the store so that loading a
    // value doesn't immediately write it straight back.
    private bool _suppressPersist;

    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    // The launch-at-startup switch is disabled when the OS won't let the app change it (the user turned
    // it off in Task Manager, or group policy pins it), so the toggle can't silently bounce back.
    [ObservableProperty]
    public partial bool LaunchAtStartupConfigurable { get; set; } = true;

    [ObservableProperty]
    public partial bool StartMinimizedToTray { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTrayOnClose { get; set; }

    [ObservableProperty]
    public partial int VolumeStep { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial string AccountTitle { get; set; } = string.Empty;

    public SettingsViewModel(
        ISettingsService settings,
        IAuthService auth,
        IStartupTaskManager startupTasks,
        ILogger<SettingsViewModel> logger)
    {
        _settings = settings;
        _auth = auth;
        _startupTasks = startupTasks;
        _logger = logger;

        // Captured on the UI thread (the view-model is resolved while the screen is built) so changes
        // raised on other threads can be marshalled back before touching bindable state.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        FooterText = BuildFooterText();

        LoadFromSettings();
        RefreshAccount();

        // Reflect whether the OS currently lets the user change the startup entry (queried off the UI
        // thread; the toggle stays enabled until we know otherwise).
        _ = InitializeLaunchAtStartupConfigurableAsync();

        _settings.Changed += OnSettingsChanged;
        _auth.ConnectionStateChanged += OnConnectionStateChanged;
    }

    /// <summary>The volume step shown beside the slider, e.g. "5%".</summary>
    public string VolumeStepDisplay => $"{VolumeStep}%";

    /// <summary>
    /// The volume step as a <see cref="double"/> for two-way binding to a <c>Slider</c>, whose value
    /// is a double. Reads and writes flow through the integer <see cref="VolumeStep"/>.
    /// </summary>
    public double VolumeStepValue
    {
        get => VolumeStep;
        set => VolumeStep = (int)Math.Round(value);
    }

    /// <summary>The stored Spotify Client ID, or a placeholder when none has been captured yet.</summary>
    public string SpotifyClientIdDisplay =>
        string.IsNullOrEmpty(_settings.Current.SpotifyClientId)
            ? _strings.GetString("Settings_ClientId_NotSet")
            : _settings.Current.SpotifyClientId;

    /// <summary>The version + affiliation line shown in the footer, after the brand hyperlink.</summary>
    public string FooterText { get; }

    private void LoadFromSettings()
    {
        AppSettings s = _settings.Current;

        _suppressPersist = true;
        LaunchAtStartup = s.LaunchAtStartup;
        StartMinimizedToTray = s.StartMinimizedToTray;
        MinimizeToTrayOnClose = s.MinimizeToTrayOnClose;
        VolumeStep = s.VolumeStep;
        SelectedThemeIndex = (int)s.ThemeMode;
        _suppressPersist = false;

        OnPropertyChanged(nameof(VolumeStepDisplay));
        OnPropertyChanged(nameof(SpotifyClientIdDisplay));
    }

    // Launch-at-startup is owned by the OS: enabling it can be refused (a user disabled it in Task
    // Manager, or policy pins it), so apply the change and then reflect whatever the OS actually reports
    // back into the toggle and the stored setting.
    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (_suppressPersist)
        {
            return;
        }

        _ = ApplyLaunchAtStartupAsync(value);
    }

    private async Task ApplyLaunchAtStartupAsync(bool desired)
    {
        // Fire-and-forget from the property setter, so a failure here has no caller to observe it —
        // catch, log, and leave the toggle reflecting the last known-good state rather than throwing
        // into an unobserved task.
        try
        {
            StartupState state = desired ? await _startupTasks.TryEnableAsync() : await _startupTasks.DisableAsync();
            bool actual = StartupTaskReconciler.ToToggleValue(state);
            bool configurable = StartupTaskReconciler.IsUserConfigurable(state);

            _settings.Update(s => s.LaunchAtStartup = actual);
            _dispatcher.RunOnUi(() =>
            {
                _suppressPersist = true;
                LaunchAtStartup = actual;
                _suppressPersist = false;
                LaunchAtStartupConfigurable = configurable;
            });
        }
        catch (Exception ex)
        {
            LogLaunchAtStartupApplyFailed(_logger, ex);
        }
    }

    private async Task InitializeLaunchAtStartupConfigurableAsync()
    {
        try
        {
            StartupState state = await _startupTasks.GetStateAsync();
            _dispatcher.RunOnUi(() => LaunchAtStartupConfigurable = StartupTaskReconciler.IsUserConfigurable(state));
        }
        catch (Exception ex)
        {
            LogLaunchAtStartupQueryFailed(_logger, ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Applying the launch-at-startup preference failed; the toggle was left unchanged.")]
    private static partial void LogLaunchAtStartupApplyFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Querying the launch-at-startup state failed; the toggle remains enabled.")]
    private static partial void LogLaunchAtStartupQueryFailed(ILogger logger, Exception exception);

    partial void OnStartMinimizedToTrayChanged(bool value) => Persist(s => s.StartMinimizedToTray = value);

    partial void OnMinimizeToTrayOnCloseChanged(bool value) => Persist(s => s.MinimizeToTrayOnClose = value);

    partial void OnSelectedThemeIndexChanged(int value)
    {
        if (Enum.IsDefined((ThemeMode)value))
        {
            Persist(s => s.ThemeMode = (ThemeMode)value);
        }
    }

    partial void OnVolumeStepChanged(int value)
    {
        Persist(s => s.VolumeStep = value);
        OnPropertyChanged(nameof(VolumeStepDisplay));
        OnPropertyChanged(nameof(VolumeStepValue));
    }

    private void Persist(Action<AppSettings> mutate)
    {
        if (_suppressPersist)
        {
            return;
        }

        _settings.Update(mutate);
    }

    private void OnSettingsChanged(object? sender, AppSettings e) =>
        _dispatcher.RunOnUi(() =>
        {
            LoadFromSettings();
            OnPropertyChanged(nameof(SpotifyClientIdDisplay));
        });

    private void OnConnectionStateChanged(object? sender, ConnectionState e) =>
        _dispatcher.RunOnUi(() =>
        {
            RefreshAccount();
            OnPropertyChanged(nameof(SpotifyClientIdDisplay));
        });

    private void RefreshAccount() =>
        AccountTitle = _auth.State == ConnectionState.Connected && _auth.CurrentAccount is Account account
            ? account.DisplayName
            : _strings.GetString("Settings_Account_NotConnected");

    // A leading space separates the version from the preceding brand hyperlink in the footer line.
    private string BuildFooterText() =>
        " " + string.Format(CultureInfo.CurrentCulture, _strings.GetString("Settings_Footer_Suffix"), AppVersion());

    private static string AppVersion()
    {
        try
        {
            PackageVersion v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}";
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run): fall back to a placeholder version.
            return "1.0.0";
        }
    }
}
