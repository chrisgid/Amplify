using Amplify.Core.Auth;
using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Microsoft.Extensions.Logging;

namespace Amplify.Core.Spotify;

/// <summary>
/// The volume model and orchestration over <see cref="ISpotifyClient"/>: it turns global hotkey
/// presses and direct UI changes into Web API volume calls, keeps the displayed level responsive by
/// updating optimistically before the call, and coalesces rapid changes into a single trailing write
/// so a burst of key presses doesn't flood the API. Device controllability and the reconciled volume
/// come from the shared <see cref="IPlayerStateProvider"/>, so a device that becomes active is picked
/// up by the next poll and the control enables without a manual refresh.
/// </summary>
/// <remarks>
/// Registered once and resolved as both <see cref="IVolumeController"/> and an
/// <see cref="IStartupInitializer"/> (the latter wires up its subscriptions and seeds from the
/// provider's last-known state). State is guarded by a lock because writes complete on background
/// continuations while hotkey/slider changes and provider pushes arrive on the UI thread; the
/// <see cref="VolumeChanged"/>/<see cref="StateChanged"/> events may therefore be raised off the UI
/// thread, so observers marshal as needed.
/// </remarks>
public sealed partial class VolumeController : IVolumeController, IStartupInitializer, IDisposable
{
    // After a write Spotify accepts, briefly ignore a polled volume that disagrees with it: the poll may
    // have read the device before it reflected the change, and applying it would snap the slider back.
    private static readonly TimeSpan _writeSettleWindow = TimeSpan.FromSeconds(2);

    // When a nudge arrives with nothing controllable (e.g. polling is suspended while minimised), a
    // single on-demand read tries to pick up a device that just became active. If that read still finds
    // nothing controllable, suppress further probes for this long so a held/mashed hotkey doesn't fire a
    // burst of reads. Matches the provider's poll cadence.
    private static readonly TimeSpan _probeInterval = TimeSpan.FromSeconds(5);

    private readonly ISpotifyClient _client;
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settings;
    private readonly IAuthService _auth;
    private readonly IPlayerStateProvider _playerState;
    private readonly ILogger<VolumeController> _logger;
    private readonly TimeProvider _time;

    private readonly object _gate = new();
    private int _volume;            // last-known/optimistic level shown to the UI (0..100)
    private int _confirmedVolume;   // last level Spotify accepted; the value reverted to on failure
    private bool _canControlDevice; // an active device is present *and* it accepts volume commands
    private int? _pendingTarget;    // latest target awaiting a write, collapsing a burst into one call
    private bool _writerRunning;
    private Task _writer = Task.CompletedTask;
    private DateTimeOffset _lastWriteAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastProbeAt = DateTimeOffset.MinValue;
    private bool _disposed;

    public VolumeController(
        ISpotifyClient client,
        IHotkeyService hotkeys,
        ISettingsService settings,
        IAuthService auth,
        IPlayerStateProvider playerState,
        ILogger<VolumeController> logger,
        TimeProvider? timeProvider = null)
    {
        _client = client;
        _hotkeys = hotkeys;
        _settings = settings;
        _auth = auth;
        _playerState = playerState;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    public int Volume
    {
        get { lock (_gate) { return _volume; } }
    }

    public bool CanControl
    {
        get { lock (_gate) { return _auth.State == ConnectionState.Connected && _canControlDevice; } }
    }

    public event EventHandler<int>? VolumeChanged;

    public event EventHandler? StateChanged;

    // After the hotkey registrar (400) and the player-state provider (250): the subscriptions only need
    // to exist before the first press, and seeding reads the provider's last-known state.
    public int Order => 900;

    public Task OnLaunchedAsync(CancellationToken ct)
    {
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        _playerState.PlayerStateChanged += OnPlayerStateChanged;

        // Seed from whatever the provider has already read (it may have polled before this existed);
        // later pushes keep the model current.
        ApplyPlayerState(_playerState.Current);
        return Task.CompletedTask;
    }

    public async Task NudgeAsync(int direction)
    {
        // May do a single on-demand read to pick up a device that became active while polling was
        // suspended; nudging from the freshly-read volume keeps the relative step correct.
        if (!await EnsureControllableAsync().ConfigureAwait(true))
        {
            return;
        }

        int target;
        bool changed;
        lock (_gate)
        {
            target = Math.Clamp(_volume + (direction * Step), 0, 100);
            changed = target != _volume;
            _volume = target;
        }

        // Already at the bound (e.g. nudging down at 0): nothing to apply or announce.
        if (!changed)
        {
            return;
        }

        VolumeChanged?.Invoke(this, target);
        await RequestWrite(target).ConfigureAwait(false);
    }

    public async Task SetVolumeAsync(int percent)
    {
        if (!await EnsureControllableAsync().ConfigureAwait(true))
        {
            return;
        }

        int target = Math.Clamp(percent, 0, 100);
        bool changed;
        lock (_gate)
        {
            changed = target != _volume;
            _volume = target;
        }

        if (!changed)
        {
            return;
        }

        VolumeChanged?.Invoke(this, target);
        await RequestWrite(target).ConfigureAwait(false);
    }

    // Gate before a volume change. Fast path when a controllable device is already known. Otherwise,
    // while connected, does one on-demand read (which works even while the poll is suspended — e.g. the
    // window is minimised) to catch a device that just became active; if it still finds nothing
    // controllable, a short throttle stops a mashed hotkey from firing a burst of reads. Worth reading
    // even when the known device merely doesn't support volume — the active device may have changed.
    private async Task<bool> EnsureControllableAsync()
    {
        if (CanControl)
        {
            return true;
        }

        // A read only helps when connected; disconnected short-circuits with no request.
        if (_auth.State != ConnectionState.Connected)
        {
            return false;
        }

        lock (_gate)
        {
            if (RecentlyProbed())
            {
                return false;
            }
        }

        // Resume on the captured (UI) context so the provider's PlayerStateChanged push — which runs
        // OnPlayerStateChanged and updates _canControlDevice/_volume — is applied before the re-check.
        await _playerState.RefreshAsync().ConfigureAwait(true);

        if (CanControl)
        {
            return true;
        }

        lock (_gate)
        {
            _lastProbeAt = _time.GetUtcNow();
        }

        return false;
    }

    // True while a recent on-demand probe found nothing controllable; called under _gate (reads _lastProbeAt).
    private bool RecentlyProbed() =>
        _time.GetUtcNow() - _lastProbeAt < _probeInterval;

    // Asks the shared provider to re-read now; the resulting push reconciles this controller. Used for
    // an immediate refresh when the main screen is shown, on top of the provider's background poll.
    public Task RefreshAsync() => _playerState.RefreshAsync();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkeys.HotkeyPressed -= OnHotkeyPressed;
        _playerState.PlayerStateChanged -= OnPlayerStateChanged;
    }

    // The configured step, defensively clamped to the valid range in case settings were tampered with.
    private int Step => Math.Clamp(_settings.Current.VolumeStep, AppSettings.MinVolumeStep, AppSettings.MaxVolumeStep);

    private void OnHotkeyPressed(object? sender, HotkeyAction action) =>
        _ = NudgeAsync(action == HotkeyAction.VolumeUp ? 1 : -1);

    private void OnPlayerStateChanged(object? sender, PlayerState? state) => ApplyPlayerState(state);

    // Reconciles the model against a fresh player-state reading from the provider.
    private void ApplyPlayerState(PlayerState? state)
    {
        // A device that can't take volume commands still reports a level worth tracking (so the meter
        // seeds correctly if it later becomes controllable), but it must not enable the control —
        // otherwise every write is rejected and the UI flickers between enabled and disabled.
        bool hasDevice = state is { HasActiveDevice: true };
        bool controllable = state is { HasActiveDevice: true, SupportsVolume: true };
        int volume;
        bool volumeChanged;
        bool controlChanged;
        lock (_gate)
        {
            controlChanged = controllable != _canControlDevice;
            _canControlDevice = controllable;
            if (!controllable)
            {
                _pendingTarget = null;
            }

            // Don't let a reading clobber an optimistic value mid-write, nor one that just landed
            // before Spotify reflected a write we made — both would snap the slider to a stale value.
            if (hasDevice && !_writerRunning && !WroteRecently())
            {
                volumeChanged = _volume != state!.VolumePercent;
                _volume = state.VolumePercent;
                _confirmedVolume = state.VolumePercent;
            }
            else
            {
                volumeChanged = false;
            }

            volume = _volume;
        }

        // Only announce a state change when control availability actually changed — otherwise every
        // steady-state poll would spuriously notify.
        if (controlChanged)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        if (volumeChanged)
        {
            VolumeChanged?.Invoke(this, volume);
        }
    }

    // Records the latest target and ensures exactly one writer drains it. Returns the writer task so
    // callers (and tests) can await the burst settling. The "is a writer running" flag is flipped only
    // inside the lock, so a target enqueued just as the writer drains can never be stranded.
    private Task RequestWrite(int target)
    {
        lock (_gate)
        {
            _pendingTarget = target;
            if (!_writerRunning)
            {
                _writerRunning = true;
                _writer = RunWriterAsync();
            }

            return _writer;
        }
    }

    private async Task RunWriterAsync()
    {
        while (true)
        {
            int target;
            lock (_gate)
            {
                if (_pendingTarget is null)
                {
                    _writerRunning = false;
                    return;
                }

                target = _pendingTarget.Value;
                _pendingTarget = null;
            }

            try
            {
                await _client.SetVolumeAsync(target).ConfigureAwait(false);
                lock (_gate)
                {
                    _confirmedVolume = target;
                    _lastWriteAt = _time.GetUtcNow();
                }
            }
            catch (DeviceNotControllableException ex)
            {
                LogVolumeWriteRejected(ex);
                RevertAndDisableControl();
                return;
            }
            catch (HttpRequestException ex)
            {
                LogVolumeWriteFailed(ex);

                // Decide whether to keep going or revert in a single critical section: if the check and
                // the revert took the lock separately, a nudge landing between them would be nulled out
                // and lost. Holding _gate across the decision blocks any concurrent RequestWrite.
                int reverted;
                lock (_gate)
                {
                    // A newer target means the user has moved on; leave the writer running and loop to
                    // send it rather than reverting — a transient blip shouldn't discard their input.
                    if (_pendingTarget is not null)
                    {
                        continue;
                    }

                    _writerRunning = false;
                    _volume = _confirmedVolume;
                    reverted = _volume;
                }

                VolumeChanged?.Invoke(this, reverted);
                return;
            }
        }
    }

    // Rolls the optimistic value back to the last accepted level, stops the writer, and disables
    // control after Spotify rejects the device (403/404) despite it reporting itself controllable.
    // Control re-enables on the next reading that reports a controllable device.
    private void RevertAndDisableControl()
    {
        int reverted;
        lock (_gate)
        {
            _pendingTarget = null;
            _writerRunning = false;
            _canControlDevice = false;
            _volume = _confirmedVolume;
            reverted = _volume;
        }

        VolumeChanged?.Invoke(this, reverted);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    // True while a just-accepted write may not yet be reflected by the device's reported volume. Called
    // under _gate (reads _lastWriteAt).
    private bool WroteRecently() => _time.GetUtcNow() - _lastWriteAt < _writeSettleWindow;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Couldn't change the Spotify volume; reverted to the last known value.")]
    private partial void LogVolumeWriteFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spotify rejected the volume change (no controllable device); reverted.")]
    private partial void LogVolumeWriteRejected(Exception exception);
}
