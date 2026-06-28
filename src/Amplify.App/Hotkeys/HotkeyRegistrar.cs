using Amplify.App.ViewModels;
using Amplify.Core.Hotkeys;
using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace Amplify.App.Hotkeys;

/// <summary>
/// Registers the persisted volume hotkeys at launch and keeps the live registrations in step with
/// settings. Runs in the hotkey initialisation band (after settings are loaded) and re-registers
/// whenever the stored combinations change — for example when the user rebinds one or a reset
/// restores the defaults — so what's registered always matches what's persisted.
/// </summary>
public sealed partial class HotkeyRegistrar : IStartupInitializer
{
    private readonly IHotkeyService _hotkeys;
    private readonly ISettingsService _settings;
    private readonly ILogger<HotkeyRegistrar> _logger;

    // Captured on the UI thread (this initialiser is constructed there). ISettingsService.Changed can
    // be raised on any thread, so registration is marshalled back here — the hotkey service's
    // registration state is touched by the keyboard-hook callback on the UI thread and isn't
    // synchronised, so all of its mutations must stay on that thread.
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();

    public HotkeyRegistrar(IHotkeyService hotkeys, ISettingsService settings, ILogger<HotkeyRegistrar> logger)
    {
        _hotkeys = hotkeys;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Hotkeys register after settings load; see the initialiser order bands.</summary>
    public int Order => 400;

    /// <inheritdoc />
    public Task OnLaunchedAsync(CancellationToken ct)
    {
        // Runs on the UI thread already (the launch sequence), so apply directly.
        ApplyFromSettings();
        _settings.Changed += OnSettingsChanged;
        return Task.CompletedTask;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        _dispatcher.RunOnUi(ApplyFromSettings);

    private void ApplyFromSettings()
    {
        AppSettings settings = _settings.Current;
        Apply(HotkeyAction.VolumeUp, settings.HotkeyVolumeUp);
        Apply(HotkeyAction.VolumeDown, settings.HotkeyVolumeDown);
    }

    private void Apply(HotkeyAction action, string canonical)
    {
        Hotkey combo = HotkeyDefaults.Resolve(canonical, action);

        // Registration only fails if the keyboard hook can't be installed; log and carry on so the
        // app still runs (with hotkeys inactive) rather than failing startup.
        if (!_hotkeys.TryRegister(action, combo))
        {
            LogRegistrationFailed(action, combo.ToCanonicalString());
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Couldn't register the {Action} hotkey '{Combo}'; the keyboard hook is unavailable.")]
    private partial void LogRegistrationFailed(HotkeyAction action, string combo);
}
