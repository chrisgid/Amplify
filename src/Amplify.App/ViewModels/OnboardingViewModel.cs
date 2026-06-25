using Amplify.Core.Auth;
using Amplify.Core.Configuration;
using Amplify.Core.Onboarding;
using Amplify.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
    // How long the copy-to-clipboard chip shows its "Copied" glyph before reverting.
    private static readonly TimeSpan _copyFeedbackDuration = TimeSpan.FromSeconds(1.5);

    private readonly IAuthService _auth;
    private readonly ISettingsService _settings;
    private readonly OnboardingFlow _flow = new();
    private readonly DispatcherQueue? _dispatcher;
    private readonly ResourceLoader _strings = new();
    private readonly ILogger<OnboardingViewModel> _logger;

    [ObservableProperty]
    public partial string ClientId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool RedirectUriCopied { get; set; }

    /// <summary>The inverse of <see cref="RedirectUriCopied"/>, for the chip's idle-glyph visibility.</summary>
    /// <remarks>
    /// Kept as a real property rather than negating <see cref="RedirectUriCopied"/> inline in XAML —
    /// <c>x:Bind</c>'s parser does not accept a leading <c>!</c> in a property path (confirmed: it
    /// fails to compile), so there is no shorter equivalent here.
    /// </remarks>
    public bool RedirectUriNotCopied => !RedirectUriCopied;

    public OnboardingViewModel(
        IAuthService auth,
        ISettingsService settings,
        IOptions<SpotifyOptions> spotifyOptions,
        ILogger<OnboardingViewModel> logger)
    {
        _auth = auth;
        _settings = settings;
        _logger = logger;
        RedirectUri = SpotifyOAuthConstants.RedirectUri(spotifyOptions.Value.RedirectPort);
        DashboardUri = new Uri(OnboardingLinks.DeveloperDashboardUrl);
        TermsUri = new Uri(OnboardingLinks.DeveloperTermsUrl);

        // Captured on the UI thread (the view-model is resolved while the screen is built), so the
        // continuation after ConnectAsync can safely touch bindable state from whichever thread it
        // resumes on.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _flow.Changed += (_, _) => _dispatcher.RunOnUi(NotifyFlowChanged);
    }

    /// <summary>The loopback redirect URI shown in the copy-to-clipboard chip.</summary>
    public string RedirectUri { get; }

    /// <summary>The Spotify Developer Dashboard link shown in the setup guide.</summary>
    public Uri DashboardUri { get; }

    /// <summary>Spotify's Developer Terms link shown in the own-app note.</summary>
    public Uri TermsUri { get; }

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

    /// <summary>Whether the Cancel button (for an in-flight attempt) should be shown.</summary>
    public bool IsAuthorizing => Phase == OnboardingPhase.Authorizing;

    /// <summary>The helper text shown beneath the Connect button.</summary>
    public string HelperText => Phase switch
    {
        OnboardingPhase.Welcome when string.IsNullOrWhiteSpace(ClientId) =>
            _strings.GetString("Onboarding_Helper_NeedsClientId"),
        OnboardingPhase.Welcome => _strings.GetString("Onboarding_Helper_ReadyToConnect"),
        _ => _strings.GetString("Onboarding_Helper_Authorizing"),
    };

    public bool CanConnect => _flow.CanBeginConnect(ClientId);

    /// <summary>Whether Cancel is meaningful right now — only while an attempt is actually running.</summary>
    public bool CanCancel => IsAuthorizing;

    /// <summary>
    /// Runs the connect attempt. <c>IncludeCancelCommand</c> generates <see cref="ConnectCancelCommand"/>
    /// (an <c>ICommand</c> wrapping <c>ConnectCommand.Cancel()</c>) and has the MVVM Toolkit manage the
    /// underlying <see cref="CancellationTokenSource"/>'s lifetime — including refusing a new execution
    /// while one is already running — so this view-model doesn't need to hand-roll either.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConnect), IncludeCancelCommand = true)]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        string trimmed = ClientId.Trim();
        _settings.Update(s => s.SpotifyClientId = trimmed);

        _flow.BeginConnect();

        AuthResult result;
        try
        {
            result = await _auth.ConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // IAuthService.ConnectAsync is documented to convert every failure (including
            // cancellation) into a non-success AuthResult rather than throwing, but guard against an
            // unexpected escape so a bug there can never leave the screen stuck on Authorizing with no
            // way to retry.
            LogConnectFailed(ex);
            result = new AuthResult(false, false, _strings.GetString("Onboarding_Helper_UnexpectedError"));
        }

        // OnboardingFlow.OnConnectResult ignores this if Cancel (or a later attempt) already moved the
        // phase on, so a result from an abandoned attempt can never resurrect stale state.
        _dispatcher.RunOnUi(() => _flow.OnConnectResult(result));
    }

    /// <summary>
    /// Abandons an in-flight connect attempt — e.g. the user closed the browser tab the system
    /// opened — for instant UI feedback. <see cref="ConnectCancelCommand"/> (generated alongside
    /// <see cref="ConnectCommand"/>) separately requests cancellation of the actual
    /// <see cref="IAuthService.ConnectAsync"/> call.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        ConnectCommand.Cancel();
        _flow.Cancel();
    }

    [RelayCommand]
    private async Task CopyRedirectUriAsync()
    {
        var package = new DataPackage();
        package.SetText(RedirectUri);
        Clipboard.SetContent(package);

        RedirectUriCopied = true;
        await Task.Delay(_copyFeedbackDuration);
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
        OnPropertyChanged(nameof(IsAuthorizing));
        OnPropertyChanged(nameof(HelperText));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(CanCancel));
        ConnectCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Onboarding connect attempt failed unexpectedly.")]
    private partial void LogConnectFailed(Exception exception);
}
