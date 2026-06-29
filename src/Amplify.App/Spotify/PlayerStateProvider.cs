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

    public event EventHandler<PlayerState?>? PlayerStateChanged;

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
            Publish(null);
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
            // here) just skips this read; the status card owns surfacing persistent failures.
            LogPlayerStateReadFailed(ex);
            return;
        }
        finally
        {
            _refreshing = false;
        }

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
                Publish(null);
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

    private void Publish(PlayerState? state) =>
        _dispatcher.RunOnUi(() =>
        {
            Current = state;
            PlayerStateChanged?.Invoke(this, state);
        });

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't read Spotify player state.")]
    private partial void LogPlayerStateReadFailed(Exception exception);
}
