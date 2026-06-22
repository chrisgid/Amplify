using System.Diagnostics.CodeAnalysis;

namespace Amplify.Core.Settings;

/// <summary>
/// Typed access to the persisted <see cref="AppSettings"/>, plus change notifications so features can
/// react live. The single store every other feature reads and writes. Implementations load and
/// migrate the file once at startup, persist atomically on every change, and never throw during
/// load — a missing, corrupt, or unreadable file falls back to defaults so the app always launches.
/// </summary>
public interface ISettingsService
{
    /// <summary>The current in-memory settings; valid after <see cref="LoadAsync"/> completes.</summary>
    AppSettings Current { get; }

    /// <summary>Reads a single value from <see cref="Current"/> via a selector.</summary>
    /// <typeparam name="T">The value type to project.</typeparam>
    /// <param name="selector">Projects the value from the settings.</param>
    [SuppressMessage(
        "Naming", "CA1716:Identifiers should not match keywords",
        Justification = "Name is fixed by the shared cross-feature contract; the app is C#-only.")]
    T Get<T>(Func<AppSettings, T> selector);

    /// <summary>
    /// Applies a mutation to the settings, persists the result atomically, and raises
    /// <see cref="Changed"/>. Writes are serialised so concurrent updates can't corrupt the file.
    /// </summary>
    /// <param name="mutate">Mutates the settings in place.</param>
    void Update(Action<AppSettings> mutate);

    /// <summary>Raised after a successful <see cref="Update"/>, carrying the new settings.</summary>
    event EventHandler<AppSettings> Changed;

    /// <summary>
    /// Loads the settings from disk, migrating older files and resetting from unreadable or
    /// future-versioned ones. Call once during the launch sequence before features initialise.
    /// </summary>
    Task LoadAsync();
}
