namespace Amplify.Core.Hotkeys;

/// <summary>
/// Registers system-wide keyboard shortcuts and raises <see cref="HotkeyPressed"/> when one fires —
/// even while Amplify is in the background or minimised to the tray. Each <see cref="HotkeyAction"/>
/// holds at most one binding at a time; registering an action again replaces its previous binding.
/// Shortcuts are <b>observed, not consumed</b>: a bound key still reaches the foreground app, so the
/// service does not reserve a combination and does not report cross-application conflicts —
/// registration fails only if the shortcut mechanism itself can't be set up. Implementations are not
/// thread-safe; call the registration methods from a single thread (the UI thread).
/// </summary>
public interface IHotkeyService
{
    /// <summary>
    /// Binds <paramref name="combo"/> to <paramref name="action"/>, replacing any existing binding
    /// for that action.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The shortcut couldn't be established (e.g. the underlying keyboard hook can't be installed).
    /// The previous binding for the action is left in place.
    /// </exception>
    void Register(HotkeyAction action, Hotkey combo);

    /// <summary>
    /// Attempts to bind <paramref name="combo"/> to <paramref name="action"/>, replacing any existing
    /// binding for that action. The non-throwing counterpart to <see cref="Register"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if bound; <c>false</c> only if the shortcut mechanism couldn't be set up, in which
    /// case the previous binding for the action is left in place. It does not return <c>false</c> for
    /// a combination used by another application — such a combination is still observed.
    /// </returns>
    bool TryRegister(HotkeyAction action, Hotkey combo);

    /// <summary>Releases the binding for <paramref name="action"/>, if any. A no-op when unbound.</summary>
    void Unregister(HotkeyAction action);

    /// <summary>
    /// When <c>true</c>, <see cref="HotkeyPressed"/> is not raised — bindings stay registered and keys
    /// are still passed through, their actions just don't fire. Set while the user is recording a new
    /// combination so capturing it doesn't also trigger the action.
    /// </summary>
    bool IsSuspended { get; set; }

    /// <summary>Raised on the UI thread when a registered hotkey is pressed.</summary>
    event EventHandler<HotkeyAction> HotkeyPressed;
}
