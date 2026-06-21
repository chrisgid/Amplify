namespace Amplify.Core.Navigation;

/// <summary>
/// The three top-level screens the application shell can show. The shell hosts exactly one at a
/// time and swaps between them; which one is shown at launch is derived from the connection state,
/// never persisted.
/// </summary>
public enum ShellRoute
{
    /// <summary>First-run / connect screen, shown when no account is connected.</summary>
    Onboarding,

    /// <summary>The main control screen, shown once an account is connected.</summary>
    Main,

    /// <summary>The settings screen, reached from <see cref="Main"/> and returning to it.</summary>
    Settings,
}
