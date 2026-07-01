namespace Amplify.Core.Tray;

/// <summary>
/// Abstraction over the OS "launch at startup" registration so the reconciliation logic and the
/// settings view-model can be unit-tested without the platform <c>StartupTask</c> API. The concrete
/// implementation wraps that API; tests substitute a fake.
/// </summary>
public interface IStartupTaskManager
{
    /// <summary>Reads the current registration state.</summary>
    Task<StartupState> GetStateAsync();

    /// <summary>
    /// Requests that the startup entry be enabled and returns the resulting state. The request has no
    /// effect (and the returned state reflects that) when the user or a policy has disabled it.
    /// </summary>
    Task<StartupState> TryEnableAsync();

    /// <summary>Disables the startup entry and returns the resulting state.</summary>
    Task<StartupState> DisableAsync();
}
