using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Amplify.Core.Onboarding;
using Amplify.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.ApplicationModel.DataTransfer;

namespace Amplify.App.ViewModels;

/// <summary>
/// Backs the first-run screen: the setup guide, the gated Client ID field, and the connect attempt
/// itself. All phase-transition rules live in <see cref="OnboardingFlow"/>; this view-model is the
/// UI-facing adapter that persists the Client ID, drives <see cref="IAuthService"/>, and exposes the
/// bindable copy/state the page needs. Navigating away on success is the shell's job
/// (<c>ShellRouter</c> reacts to <see cref="IAuthService.ConnectionStateChanged"/>), not this
/// view-model's.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly IAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly OnboardingFlow _flow = new();
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();

    [ObservableProperty]
    public partial string ClientId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RedirectUriCopied { get; set; }

    /// <summary>The inverse of <see cref="RedirectUriCopied"/>, for the chip's idle-glyph visibility.</summary>
    public bool RedirectUriNotCopied => !RedirectUriCopied;

    public OnboardingViewModel(IAuthService auth, ISettingsService settings, IOptions<SpotifyOptions> spotifyOptions)
    {
        _auth = auth;
        _settings = settings;
        RedirectUri = SpotifyOAuthConstants.RedirectUri(spotifyOptions.Value.RedirectPort);

        // Captured on the UI thread (the view-model is resolved while the screen is built), so the
        // continuation after ConnectAsync can safely touch bindable state from whichever thread it
        // resumes on.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _flow.Changed += (_, _) => RunOnUi(NotifyFlowChanged);
    }

    /// <summary>The loopback redirect URI shown in the copy-to-clipboard chip.</summary>
    public string RedirectUri { get; }

    /// <summary>The screen currently being shown.</summary>
    public OnboardingPhase Phase => _flow.Phase;

    /// <summary>Whether the most recent attempt ended in the user declining consent.</summary>
    public bool Denied => _flow.Denied;

    /// <summary>A user-readable message from the most recent non-denied failure, or <c>null</c>.</summary>
    public string? ErrorMessage => _flow.ErrorMessage;

    /// <summary>Whether a non-denied failure message should be shown.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>The Connect button's label, switching to a "waiting" caption mid-attempt.</summary>
    public string ConnectButtonText => Phase == OnboardingPhase.Welcome
        ? _strings.GetString("Onboarding_Connect_ButtonText")
        : _strings.GetString("Onboarding_Connecting_ButtonText");

    /// <summary>Whether the Client ID field should accept input (disabled mid-attempt).</summary>
    public bool IsClientIdEditable => Phase == OnboardingPhase.Welcome;

    /// <summary>Whether the connect spinner should be shown.</summary>
    public bool IsConnecting => Phase != OnboardingPhase.Welcome;

    /// <summary>The helper text shown beneath the Connect button.</summary>
    public string HelperText => Phase switch
    {
        OnboardingPhase.Welcome when string.IsNullOrWhiteSpace(ClientId) =>
            _strings.GetString("Onboarding_Helper_NeedsClientId"),
        OnboardingPhase.Welcome => _strings.GetString("Onboarding_Helper_ReadyToConnect"),
        _ => _strings.GetString("Onboarding_Helper_Authorizing"),
    };

    public bool CanConnect => _flow.CanBeginConnect(ClientId);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        string trimmed = ClientId.Trim();
        _settings.Update(s => s.SpotifyClientId = trimmed);

        _flow.BeginConnect();

        AuthResult result = await _auth.ConnectAsync();

        RunOnUi(() => _flow.OnConnectResult(result));
    }

    [RelayCommand]
    private async Task CopyRedirectUriAsync()
    {
        var package = new DataPackage();
        package.SetText(RedirectUri);
        Clipboard.SetContent(package);

        RedirectUriCopied = true;
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        RedirectUriCopied = false;
    }

    partial void OnClientIdChanged(string value)
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(HelperText));
        ConnectCommand.NotifyCanExecuteChanged();
    }

    partial void OnRedirectUriCopiedChanged(bool value) => OnPropertyChanged(nameof(RedirectUriNotCopied));

    private void NotifyFlowChanged()
    {
        OnPropertyChanged(nameof(Phase));
        OnPropertyChanged(nameof(Denied));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsClientIdEditable));
        OnPropertyChanged(nameof(IsConnecting));
        OnPropertyChanged(nameof(HelperText));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(CanConnect));
        ConnectCommand.NotifyCanExecuteChanged();
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
}
