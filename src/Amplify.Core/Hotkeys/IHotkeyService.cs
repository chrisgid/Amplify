namespace Amplify.Core.Hotkeys;

/// <summary>
/// Registers system-wide keyboard shortcuts and raises <see cref="HotkeyPressed"/> when one fires —
/// even while Amplify is in the background or minimised to the tray. Each <see cref="HotkeyAction"/>
/// holds at most one registration at a time; registering an action again replaces its previous
/// binding. A combination already owned by another application can't be registered.
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Binds <paramref name="combo"/> to <paramref name="action"/>, replacing any existing binding
    /// for that action.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The combination couldn't be registered (e.g. it's already owned by another app). The previous
    /// binding for the action is left in place.
    /// </exception>
    void Register(HotkeyAction action, Hotkey combo);

    /// <summary>
    /// Attempts to bind <paramref name="combo"/> to <paramref name="action"/>, replacing any existing
    /// binding for that action. The non-throwing counterpart to <see cref="Register"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if registered; <c>false</c> if the combination is unavailable, in which case the
    /// previous binding for the action is left in place.
    /// </returns>
    bool TryRegister(HotkeyAction action, Hotkey combo);

    /// <summary>Releases the binding for <paramref name="action"/>, if any. A no-op when unbound.</summary>
    void Unregister(HotkeyAction action);

    /// <summary>Raised on the UI thread when a registered hotkey is pressed.</summary>
    event EventHandler<HotkeyAction> HotkeyPressed;
}
