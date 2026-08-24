using Amplify.Core.Settings;
using Amplify.Core.Startup;
using Amplify.Core.Theming;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace Amplify.App.Theming;

/// <summary>
/// Applies the stored appearance preference to the app and keeps it in sync with the system. It
/// resolves the preference to a concrete <see cref="ElementTheme"/> (which the shell window applies
/// to its content root), re-applying whenever the user changes the override or — while following the
/// system — when Windows switches its theme or accent colour.
/// </summary>
/// <remarks>
/// The service holds no UI reference of its own: it exposes the resolved <see cref="CurrentTheme"/>
/// and raises <see cref="ThemeChanged"/>, and the window listens and applies the value to its root.
/// This keeps the platform-specific <c>RequestedTheme</c> plumbing in the window that owns the
/// content, while the preference/OS-watching logic lives here as a singleton.
/// </remarks>
public sealed class ThemeService : IThemeService, IStartupInitializer
{
    private readonly ISettingsService _settings;
    private readonly DispatcherQueue? _dispatcher;

    // Kept alive for the lifetime of the service: the ColorValuesChanged subscription is dropped if
    // the UISettings instance is collected. Null when the platform API is unavailable (e.g. an
    // unpackaged or headless run), in which case live OS-following is simply not wired.
    private readonly UISettings? _uiSettings;

    // The preference CurrentTheme was resolved from. Kept because ElementTheme.Default is lossy: it
    // says "follow the OS" without saying which OS theme is in force, and EffectiveTheme has to
    // answer that for the consumers the framework can't theme (the system caption buttons).
    private ThemeMode _mode;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;

        // Captured on the UI thread (the host is built during App construction) so settings/OS
        // notifications raised on other threads can be marshalled back before touching the UI.
        _dispatcher = DispatcherQueue.GetForCurrentThread();

        _mode = settings.Current.ThemeMode;
        CurrentTheme = ToElementTheme(ThemeResolver.Resolve(_mode));

        _settings.Changed += OnSettingsChanged;

        try
        {
            _uiSettings = new UISettings();
            _uiSettings.ColorValuesChanged += OnColorValuesChanged;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // No view context for UISettings (unpackaged/headless): the manual override still works;
            // only the automatic OS theme/accent following is unavailable.
            _uiSettings = null;
        }
    }

    /// <summary>The framework theme the window should apply to its content root.</summary>
    public ElementTheme CurrentTheme { get; private set; }

    /// <summary>
    /// The appearance actually in force, always a concrete <see cref="ResolvedTheme.Light"/> or
    /// <see cref="ResolvedTheme.Dark"/>: while following the system this reads the current Windows
    /// theme, so it is evaluated on each call rather than cached. For UI the framework themes itself,
    /// prefer <see cref="CurrentTheme"/> — this is for surfaces drawn outside the XAML tree (the
    /// system caption buttons) that have to be given explicit colours.
    /// </summary>
    public ResolvedTheme EffectiveTheme => ThemeResolver.ResolveEffective(_mode, IsSystemDark);

    /// <inheritdoc />
    public event EventHandler? ThemeChanged;

    /// <summary>Runs before the first frame so <see cref="CurrentTheme"/> reflects the saved override.</summary>
    public int Order => 100;

    /// <inheritdoc />
    public Task OnLaunchedAsync(CancellationToken ct)
    {
        Apply(_settings.Current.ThemeMode);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Apply(ThemeMode mode)
    {
        // Recorded before the idempotence guard below: System and an unknown value both resolve to
        // ElementTheme.Default, so the guard can short-circuit on a preference change that
        // EffectiveTheme still needs to see.
        _mode = mode;

        ElementTheme resolved = ToElementTheme(ThemeResolver.Resolve(mode));
        if (resolved == CurrentTheme)
        {
            return;
        }

        CurrentTheme = resolved;
        RaiseThemeChanged();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => Apply(settings.ThemeMode);

    // While following the system, the resolved ElementTheme stays Default, so Apply would no-op —
    // but the window still needs to re-assert its appearance (and any accent-driven surfaces refresh),
    // so notify unconditionally when Windows reports a colour/theme change.
    private void OnColorValuesChanged(UISettings sender, object args) => RaiseThemeChanged();

    private void RaiseThemeChanged()
    {
        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _dispatcher.TryEnqueue(() => ThemeChanged?.Invoke(this, EventArgs.Empty));
        }
    }

    // Whether Windows is currently in dark mode. UISettings reports the theme through its colour
    // palette rather than a flag: the Background value is black under the dark theme and white under
    // the light one, so the standard perceived-brightness test on it is the read. Passed to
    // ResolveEffective as a delegate, so a pinned Light/Dark preference never calls it at all.
    //
    // Falls back to light where UISettings is unavailable or the call fails — the same conditions the
    // constructor already tolerates. Guarded for the same reason it is constructed inside a try: this
    // runs from a ThemeChanged callback on the dispatcher, where a throw would take the app down.
    private bool IsSystemDark()
    {
        if (_uiSettings is null)
        {
            return false;
        }

        try
        {
            Windows.UI.Color background = _uiSettings.GetColorValue(UIColorType.Background);
            return ((5 * background.G) + (2 * background.R) + background.B) <= (8 * 128);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static ElementTheme ToElementTheme(ResolvedTheme resolved) => resolved switch
    {
        ResolvedTheme.Light => ElementTheme.Light,
        ResolvedTheme.Dark => ElementTheme.Dark,
        _ => ElementTheme.Default,
    };
}
