using Amplify.Core.Hotkeys;
using Amplify.Core.Startup;

namespace Amplify.App.Dev;

/// <summary>
/// TEMPORARY glue that turns a global hotkey press into a volume nudge on the playback slice, so the
/// shortcuts actually move Spotify's volume before the real volume controller exists. The volume
/// controller subscribes to <see cref="IHotkeyService.HotkeyPressed"/> directly later, at which
/// point this bridge and the slice it drives are removed.
/// </summary>
public sealed class DevHotkeyVolumeBridge : IStartupInitializer
{
    private readonly IHotkeyService _hotkeys;
    private readonly DevPlaybackSlice _playback;

    public DevHotkeyVolumeBridge(IHotkeyService hotkeys, DevPlaybackSlice playback)
    {
        _hotkeys = hotkeys;
        _playback = playback;
    }

    // After the registrar (400); the subscription only needs to exist before the user presses a key.
    public int Order => 900;

    public Task OnLaunchedAsync(CancellationToken ct)
    {
        _hotkeys.HotkeyPressed += OnHotkeyPressed;
        return Task.CompletedTask;
    }

    // Hotkey events arrive on the UI thread; the nudge is fire-and-forget so the handler returns
    // immediately and the (discarded) task drives the Spotify call.
    private void OnHotkeyPressed(object? sender, HotkeyAction action) =>
        _ = _playback.NudgeAsync(action == HotkeyAction.VolumeUp ? 1 : -1);
}
