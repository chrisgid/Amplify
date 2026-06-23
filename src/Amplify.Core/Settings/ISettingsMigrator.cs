using System.Text.Json.Nodes;

namespace Amplify.Core.Settings;

/// <summary>
/// A single-hop upgrade of the persisted settings JSON, from <see cref="FromVersion"/> to the next
/// version. Migrators operate on a tolerant JSON tree so they can rename, move, split, or retype
/// fields before the final typed deserialisation. Each release that makes a <em>structural</em>
/// change adds exactly one new migrator and never edits older ones; additive-only changes need no
/// migrator, since missing keys fall back to their defaults on deserialisation.
/// </summary>
public interface ISettingsMigrator
{
    /// <summary>The schema version this migrator upgrades from; it produces version + 1.</summary>
    int FromVersion { get; }

    /// <summary>Transforms the settings object into the next version's shape.</summary>
    /// <param name="root">The settings JSON object at <see cref="FromVersion"/>.</param>
    /// <returns>The upgraded settings object.</returns>
    JsonObject Upgrade(JsonObject root);
}
