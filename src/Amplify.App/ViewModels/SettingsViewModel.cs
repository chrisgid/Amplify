using System.Globalization;
using Amplify.Core.Auth;
using Amplify.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();

    // Guards the bindable properties while they are being populated from the store so that loading a
    // value doesn't immediately write it straight back.
    private bool _suppressPersist;

    [ObservableProperty]
    public partial bool LaunchAtStartup { get; set; }

    [ObservableProperty]
    public partial bool StartMinimizedToTray { get; set; }

    [ObservableProperty]
    public partial bool NotifyOnVolumeChange { get; set; }

    [ObservableProperty]
    public partial int VolumeStep { get; set; }

    [ObservableProperty]
    public partial int SelectedThemeIndex { get; set; }

    [ObservableProperty]
    public partial string AccountTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AccountSubtitle { get; set; } = string.Empty;

    public SettingsViewModel(ISettingsService settings, IAuthService auth)
    {
        _settings = settings;
        _auth = auth;

        // Captured on the UI thread (the view-model is resolved while the screen is built) so changes
        // raised on other threads can be marshalled back before touching bindable state.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        FooterText = BuildFooterText();

        LoadFromSettings();
        RefreshAccount();

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
        NotifyOnVolumeChange = s.NotifyOnVolumeChange;
        VolumeStep = s.VolumeStep;
        SelectedThemeIndex = (int)s.ThemeMode;
        _suppressPersist = false;

        OnPropertyChanged(nameof(VolumeStepDisplay));
        OnPropertyChanged(nameof(SpotifyClientIdDisplay));
    }

    partial void OnLaunchAtStartupChanged(bool value) => Persist(s => s.LaunchAtStartup = value);

    partial void OnStartMinimizedToTrayChanged(bool value) => Persist(s => s.StartMinimizedToTray = value);

    partial void OnNotifyOnVolumeChangeChanged(bool value) => Persist(s => s.NotifyOnVolumeChange = value);

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
        RunOnUi(() =>
        {
            LoadFromSettings();
            OnPropertyChanged(nameof(SpotifyClientIdDisplay));
        });

    private void OnConnectionStateChanged(object? sender, ConnectionState e) =>
        RunOnUi(() =>
        {
            RefreshAccount();
            OnPropertyChanged(nameof(SpotifyClientIdDisplay));
        });

    private void RefreshAccount()
    {
        if (_auth.State == ConnectionState.Connected && _auth.CurrentAccount is Account account)
        {
            AccountTitle = account.DisplayName;
            AccountSubtitle = account.IsPremium
                ? account.Plan
                : _strings.GetString("Settings_Account_FreeSubtitle");
        }
        else
        {
            AccountTitle = _strings.GetString("Settings_Account_NotConnected");
            AccountSubtitle = _strings.GetString("Settings_Account_NotConnectedHint");
        }
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            _dispatcher.TryEnqueue(() => action());
        }
    }

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
