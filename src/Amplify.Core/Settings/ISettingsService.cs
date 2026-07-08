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

    /// <summary>
    /// Replaces all settings with their defaults, persists the result atomically, and raises
    /// <see cref="Changed"/>. This is the settings half of a full app reset: it clears the stored
    /// Spotify Client ID and restores the default hotkeys, volume step, and every other preference.
    /// Serialised against <see cref="Update"/> the same way, so a reset can't interleave with a write.
    /// </summary>
    void Reset();

    /// <summary>Raised after a successful <see cref="Update"/> or <see cref="Reset"/>, carrying the new settings.</summary>
    event EventHandler<AppSettings> Changed;

    /// <summary>
    /// Loads the settings from disk, migrating older files and resetting from unreadable or
    /// future-versioned ones.
    /// </summary>
    /// <remarks>
    /// Must be called <b>exactly once</b> during the launch sequence, before any feature can call
    /// <see cref="Update"/>. It is not safe to interleave with <see cref="Update"/>: the file read
    /// happens outside the write lock, so a concurrent update could be clobbered by the load's final
    /// assignment. The fixed launch order (settings load → session restore → ordered initializers)
    /// guarantees this single-caller contract.
    /// </remarks>
    Task LoadAsync();
}
