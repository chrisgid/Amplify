using System.Text.Json.Nodes;
using Amplify.Core.Settings;

namespace Amplify.Tests.Settings;

public class SettingsMigrationRunnerTests
{
    // Records the order in which migrators run and tags the object so the effect is observable.
    private sealed class RecordingMigrator(int fromVersion, List<int> log) : ISettingsMigrator
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject root)
        {
            log.Add(fromVersion);
            root[$"migratedFrom{fromVersion}"] = true;
            return root;
        }
    }

    private sealed class ThrowingMigrator(int fromVersion) : ISettingsMigrator
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject root) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void SameVersionLoadsAsIs()
    {
        var root = new JsonObject { ["schemaVersion"] = 3 };

        SettingsMigrationResult result = SettingsMigrationRunner.Run(root, 3, 3, []);

        Assert.Equal(SettingsMigrationOutcome.Loaded, result.Outcome);
        Assert.Same(root, result.Root);
    }

    [Fact]
    public void NewerFileResetsToDefaults()
    {
        var root = new JsonObject { ["schemaVersion"] = 5 };

        SettingsMigrationResult result = SettingsMigrationRunner.Run(root, 5, 2, []);

        Assert.Equal(SettingsMigrationOutcome.ResetToDefaults, result.Outcome);
        Assert.Null(result.Root);
    }

    [Fact]
    public void SingleHopMigrates()
    {
        var log = new List<int>();
        var root = new JsonObject { ["schemaVersion"] = 1 };

        SettingsMigrationResult result =
            SettingsMigrationRunner.Run(root, 1, 2, [new RecordingMigrator(1, log)]);

        Assert.Equal(SettingsMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal(2, result.Root!["schemaVersion"]!.GetValue<int>());
        Assert.True(result.Root["migratedFrom1"]!.GetValue<bool>());
        Assert.Equal([1], log);
    }

    [Fact]
    public void MultiHopRunsMigratorsInOrder()
    {
        var log = new List<int>();
        var root = new JsonObject { ["schemaVersion"] = 1 };

        // Supplied out of order to prove the runner sequences by version, not list position.
        SettingsMigrationResult result = SettingsMigrationRunner.Run(
            root, 1, 3, [new RecordingMigrator(2, log), new RecordingMigrator(1, log)]);

        Assert.Equal(SettingsMigrationOutcome.Migrated, result.Outcome);
        Assert.Equal([1, 2], log);
        Assert.Equal(3, result.Root!["schemaVersion"]!.GetValue<int>());
    }

    [Fact]
    public void MissingMigratorForHopResetsToDefaults()
    {
        var log = new List<int>();
        var root = new JsonObject { ["schemaVersion"] = 1 };

        // Has a v1 migrator but no v2 migrator to reach v3.
        SettingsMigrationResult result =
            SettingsMigrationRunner.Run(root, 1, 3, [new RecordingMigrator(1, log)]);

        Assert.Equal(SettingsMigrationOutcome.ResetToDefaults, result.Outcome);
    }

    [Fact]
    public void ThrowingMigratorResetsToDefaults()
    {
        var root = new JsonObject { ["schemaVersion"] = 1 };

        SettingsMigrationResult result =
            SettingsMigrationRunner.Run(root, 1, 2, [new ThrowingMigrator(1)]);

        Assert.Equal(SettingsMigrationOutcome.ResetToDefaults, result.Outcome);
        Assert.Null(result.Root);
    }
}
