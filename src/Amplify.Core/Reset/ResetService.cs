using Amplify.Core.Auth;
using Amplify.Core.Settings;

namespace Amplify.Core.Reset;

/// <summary>
/// Default <see cref="IResetService"/>. It owns no state of its own — the reset is just the two
/// underlying operations run in a deliberate order: reset the settings store (which clears the Client
/// ID and, via the settings-change listeners, re-registers the default hotkeys), then disconnect the
/// account (clearing the stored tokens and flipping the connection state so the shell returns to
/// onboarding).
/// </summary>
public sealed class ResetService : IResetService
{
    private readonly ISettingsService _settings;
    private readonly IAuthService _auth;

    public ResetService(ISettingsService settings, IAuthService auth)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(auth);

        _settings = settings;
        _auth = auth;
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        // Settings first: even if the disconnect below fails, the app is left with defaults applied
        // (shortcuts, step, and a blank Client ID) rather than half-reset.
        _settings.Reset();
        await _auth.DisconnectAsync().ConfigureAwait(false);
    }
}
