using System.Text.Json.Nodes;

namespace Amplify.Core.Settings;

/// <summary>What the migration runner decided to do with a settings file.</summary>
public enum SettingsMigrationOutcome
{
    /// <summary>The file was already at the target version; load it as-is.</summary>
    Loaded,

    /// <summary>The file was migrated up to the target version and should be persisted.</summary>
    Migrated,

    /// <summary>The file can't be trusted (downgrade, gap in migrators, or a faulty migrator); reset.</summary>
    ResetToDefaults,
}

/// <summary>The result of running migration: the decided <see cref="Outcome"/> and, when relevant, the tree.</summary>
/// <param name="Outcome">What the runner decided.</param>
/// <param name="Root">The (possibly migrated) settings object; <c>null</c> when resetting.</param>
public readonly record struct SettingsMigrationResult(SettingsMigrationOutcome Outcome, JsonObject? Root);

/// <summary>
/// The pure decision engine behind settings loading: given a parsed settings object and the file's
/// schema version, it either loads as-is, walks a chain of single-hop migrators up to the target
/// version, or signals a reset to defaults. It performs no I/O, so it is fully unit-testable. Reset
/// is the safe fallback for a newer-than-known file (a downgrade), a missing migrator for some hop,
/// or a migrator that throws — none of which should crash startup.
/// </summary>
public static class SettingsMigrationRunner
{
    /// <summary>Runs the migration decision for a parsed settings object.</summary>
    /// <param name="root">The parsed settings object from disk.</param>
    /// <param name="fromVersion">The schema version recorded in the file.</param>
    /// <param name="targetVersion">The schema version the app currently understands.</param>
    /// <param name="migrators">The available single-hop migrators, in any order.</param>
    public static SettingsMigrationResult Run(
        JsonObject root, int fromVersion, int targetVersion, IReadOnlyList<ISettingsMigrator> migrators)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(migrators);

        if (fromVersion == targetVersion)
        {
            return new SettingsMigrationResult(SettingsMigrationOutcome.Loaded, root);
        }

        // A file from a newer build may use a shape this version can't parse; resetting is safer
        // than guessing at an unknown structure.
        if (fromVersion > targetVersion)
        {
            return new SettingsMigrationResult(SettingsMigrationOutcome.ResetToDefaults, null);
        }

        JsonObject current = root;
        for (int version = fromVersion; version < targetVersion; version++)
        {
            ISettingsMigrator? migrator = migrators.FirstOrDefault(m => m.FromVersion == version);
            if (migrator is null)
            {
                return new SettingsMigrationResult(SettingsMigrationOutcome.ResetToDefaults, null);
            }

            try
            {
                current = migrator.Upgrade(current);
            }
            catch (Exception) // A faulty migrator must degrade to defaults, never crash the launch.
            {
                return new SettingsMigrationResult(SettingsMigrationOutcome.ResetToDefaults, null);
            }

            current["schemaVersion"] = version + 1;
        }

        return new SettingsMigrationResult(SettingsMigrationOutcome.Migrated, current);
    }
}
