using Amplify.App.ViewModels;
using Amplify.Core.Auth;
using Amplify.Core.Spotify;
using Amplify.Core.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Amplify.App.Spotify;

/// <summary>
/// The single Spotify player-state poller. While connected and the window is visible it reads
/// <c>GET /v1/me/player</c> on a short timer (Spotify has no push for "a device became active"), and
/// it also reads on demand; every read is published once so the status card and the volume controller
/// share one set of requests instead of polling independently. Owns the timer here (App layer) because
/// it depends on WinUI's <see cref="DispatcherQueue"/>; the abstraction it fulfils is UI-free.
/// </summary>
/// <remarks>
/// Public so the shell (<c>MainWindow</c>) can name it to drive <see cref="Suspend"/>/<see cref="Resume"/>
/// from window visibility; all other consumers depend on <see cref="IPlayerStateProvider"/>.
/// </remarks>
public sealed partial class PlayerStateProvider : IPlayerStateProvider, IStartupInitializer, IDisposable
{
    // Short enough to feel responsive when a device starts playing, long enough not to hammer the API.
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    private readonly IAuthService _auth;
    private readonly ISpotifyClient _client;
    private readonly DispatcherQueue? _dispatcher;
    private readonly ILogger<PlayerStateProvider> _logger;
    private readonly DispatcherQueueTimer? _pollTimer;

    // Advisory only: it decides when a run of failed reads is worth telling the user about, and never
    // affects whether or how often we poll. Mutated solely inside the dispatcher callbacks below, so it
    // is single-threaded even though reads complete on a pool thread.
    private readonly ReachabilityTracker _reachability = new();

    // Whether the main window is currently visible; set by the shell via Suspend()/Resume() so polling
    // stops when nobody can see the result. True initially — the window is visible at launch.
    private bool _windowVisible = true;
    private bool _refreshing;
    private bool _disposed;

    public PlayerStateProvider(IAuthService auth, ISpotifyClient client, ILogger<PlayerStateProvider> logger)
    {
        _auth = auth;
        _client = client;
        _logger = logger;

        // Captured on the UI thread (the startup initializer runs there); the connection-state event
        // can arrive off-thread and the timer ticks on this queue, so publishing marshals here.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _pollTimer = _dispatcher?.CreateTimer();
        if (_pollTimer is not null)
        {
            _pollTimer.Interval = _pollInterval;
            _pollTimer.IsRepeating = true;
            _pollTimer.Tick += (_, _) => _ = RefreshAsync();
        }

        _auth.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public PlayerState? Current { get; private set; }

    public bool IsUnreachable => _reachability.IsUnreachable;

    public event EventHandler<PlayerState?>? PlayerStateChanged;

    public event EventHandler<bool>? ReachabilityChanged;

    // After tray/window (200): the window is up to drive Suspend()/Resume(), and the first read below
    // needs the session restored (a pre-step). Consumers seed from Current, so order beyond this is
    // not load-bearing.
    public int Order => 250;

    public Task OnLaunchedAsync(CancellationToken ct)
    {
        if (_auth.State == ConnectionState.Connected)
        {
            _ = RefreshAsync();
        }

        UpdatePollingState();
        return Task.CompletedTask;
    }

    public async Task RefreshAsync()
    {
        if (_auth.State != ConnectionState.Connected)
        {
            PublishDisconnected();
            return;
        }

        // The poll tick, a reconnect, and an on-demand refresh can overlap; collapse them into one
        // in-flight read (all initiated on the UI thread).
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        PlayerState? state;
        try
        {
            state = await _client.GetPlayerStateAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // A failed request/token-refresh (or HttpClient's own timeout — there's no caller token
            // here) skips this read: no PlayerStateChanged, so nobody mistakes the old reading for a
            // fresh one. The run of failures is recorded instead, and once it's long enough the status
            // block says so. Deliberately no change to the timer — the poll carries on at its normal
            // cadence, which is what lets the app recover by itself when the network comes back.
            LogPlayerStateReadFailed(ex);
            RecordReadOutcome(succeeded: false);
            return;
        }
        finally
        {
            _refreshing = false;
        }

        // Clear any failure run before publishing, so a consumer handling PlayerStateChanged already
        // sees IsUnreachable false rather than a fresh reading alongside a stale outage flag.
        RecordReadOutcome(succeeded: true);
        Publish(state);
    }

    /// <summary>Stops polling while the main window isn't visible (e.g. minimised). Called by the shell.</summary>
    public void Suspend()
    {
        _windowVisible = false;
        UpdatePollingState();
    }

    /// <summary>
    /// Resumes polling when the window becomes visible again, with an immediate read so consumers catch
    /// up on anything that changed while hidden. Called by the shell.
    /// </summary>
    public void Resume()
    {
        _windowVisible = true;
        if (_auth.State == ConnectionState.Connected)
        {
            _ = RefreshAsync();
        }

        UpdatePollingState();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _auth.ConnectionStateChanged -= OnConnectionStateChanged;
        _pollTimer?.Stop();
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state) =>
        _dispatcher.RunOnUi(() =>
        {
            if (state == ConnectionState.Connected)
            {
                _ = RefreshAsync();
            }
            else
            {
                // Disconnecting clears the player state so consumers drop the device immediately.
                PublishDisconnected();
            }

            UpdatePollingState();
        });

    // Poll only while connected and visible — otherwise it's either useless (not connected) or wasted
    // (nobody can see it).
    private void UpdatePollingState()
    {
        if (_auth.State == ConnectionState.Connected && _windowVisible)
        {
            _pollTimer?.Start();
        }
        else
        {
            _pollTimer?.Stop();
        }
    }

    // Clears the player state and any failure run together: once disconnected the question "can we
    // reach Spotify" no longer applies, and a later reconnect should start from a clean slate rather
    // than inheriting an outage that may be long over.
    private void PublishDisconnected()
    {
        _dispatcher.RunOnUi(() =>
        {
            if (_reachability.Reset())
            {
                ReachabilityChanged?.Invoke(this, false);
            }
        });

        Publish(null);
    }

    // Feeds one read's outcome to the tracker, raising the event only on a transition so a long outage
    // doesn't re-notify every 5 seconds.
    private void RecordReadOutcome(bool succeeded) =>
        _dispatcher.RunOnUi(() =>
        {
            // A read that failed as the account was disconnecting can land here after the disconnect
            // reset; counting it would let a stale run rebuild behind the user's back. A success still
            // counts unconditionally — clearing is always safe.
            bool changed = succeeded
                ? _reachability.RecordSuccess()
                : _auth.State == ConnectionState.Connected && _reachability.RecordFailure();

            if (changed)
            {
                ReachabilityChanged?.Invoke(this, _reachability.IsUnreachable);
            }
        });

    private void Publish(PlayerState? state) =>
        _dispatcher.RunOnUi(() =>
        {
            // Re-checked on the UI thread, which is ordered with the connection-state handler: a
            // disconnect that landed while a read was in flight must win, so never surface a device
            // while disconnected even if the read completed afterwards.
            PlayerState? published = _auth.State == ConnectionState.Connected ? state : null;
            Current = published;
            PlayerStateChanged?.Invoke(this, published);
        });

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't read Spotify player state.")]
    private partial void LogPlayerStateReadFailed(Exception exception);
}
