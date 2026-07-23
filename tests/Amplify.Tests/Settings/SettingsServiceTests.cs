using System.Text.Json.Nodes;
using Amplify.Core.Settings;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amplify.Tests.Settings;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SettingsServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "amplify-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a leftover temp directory is harmless.
        }
    }

    private SettingsService NewService() => new(_dir, NullLogger<SettingsService>.Instance);

    // A migrator that bumps the volume step so its effect is visible after load.
    private sealed class StepMigrator(int fromVersion) : ISettingsMigrator
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject root)
        {
            root["volumeStep"] = 10;
            return root;
        }
    }

    private sealed class ThrowingMigrator(int fromVersion) : ISettingsMigrator
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject root) => throw new InvalidOperationException("boom");
    }

    private sealed class OutOfRangeStepMigrator(int fromVersion) : ISettingsMigrator
    {
        public int FromVersion => fromVersion;

        public JsonObject Upgrade(JsonObject root)
        {
            root["volumeStep"] = 999;
            return root;
        }
    }

    [Fact]
    public async Task MissingFileLoadsDefaultsAndWritesCleanFile()
    {
        SettingsService service = NewService();

        await service.LoadAsync();

        Assert.True(File.Exists(_file));
        Assert.Equal(AppSettings.CurrentSchemaVersion, service.Current.SchemaVersion);
        Assert.Equal(5, service.Current.VolumeStep);
        Assert.False(service.Current.TrayHintShown);
        Assert.False(service.Current.LaunchAtStartup);
        Assert.True(service.Current.StartMinimizedToTray);
        Assert.Equal(ThemeMode.System, service.Current.ThemeMode);
    }

    [Fact]
    public async Task DefaultsMatchDocumentedValues()
    {
        SettingsService service = NewService();
        await service.LoadAsync();
        AppSettings s = service.Current;

        Assert.False(s.LaunchAtStartup);
        Assert.True(s.StartMinimizedToTray);
        Assert.True(s.MinimizeToTrayOnClose);
        Assert.False(s.TrayHintShown);
        Assert.Equal("", s.SpotifyClientId);
        Assert.Equal(ThemeMode.System, s.ThemeMode);
        Assert.Equal(5, s.VolumeStep);
        Assert.Equal("ctrl+alt+arrowup", s.HotkeyVolumeUp);
        Assert.Equal("ctrl+alt+arrowdown", s.HotkeyVolumeDown);
        Assert.Null(s.Window);
    }

    [Fact]
    public async Task UpdatePersistsAndReloadRoundTrips()
    {
        SettingsService service = NewService();
        await service.LoadAsync();

        service.Update(s =>
        {
            s.VolumeStep = 12;
            s.TrayHintShown = true;
            s.ThemeMode = ThemeMode.Dark;
            s.SpotifyClientId = "abc123";
            s.Window = new WindowState(400, 700, 10, 20);
        });

        SettingsService reloaded = NewService();
        await reloaded.LoadAsync();

        Assert.Equal(12, reloaded.Current.VolumeStep);
        Assert.True(reloaded.Current.TrayHintShown);
        Assert.Equal(ThemeMode.Dark, reloaded.Current.ThemeMode);
        Assert.Equal("abc123", reloaded.Current.SpotifyClientId);
        Assert.Equal(new WindowState(400, 700, 10, 20), reloaded.Current.Window);
    }

    [Fact]
    public async Task ResetRestoresDefaultsAndPersistsThem()
    {
        SettingsService service = NewService();
        await service.LoadAsync();
        service.Update(s =>
        {
            s.VolumeStep = 20;
            s.SpotifyClientId = "abc123";
            s.HotkeyVolumeUp = "ctrl+f1";
            s.ThemeMode = ThemeMode.Dark;
            s.TrayHintShown = true;
            s.Window = new WindowState(400, 700, 10, 20);
        });

        service.Reset();

        Assert.Equal(5, service.Current.VolumeStep);
        Assert.Equal("", service.Current.SpotifyClientId);
        Assert.Equal("ctrl+alt+arrowup", service.Current.HotkeyVolumeUp);
        Assert.Equal("ctrl+alt+arrowdown", service.Current.HotkeyVolumeDown);
        Assert.Equal(ThemeMode.System, service.Current.ThemeMode);
        Assert.False(service.Current.TrayHintShown);
        Assert.Null(service.Current.Window);

        // Defaults are persisted, so a fresh load sees the reset state too.
        SettingsService reloaded = NewService();
        await reloaded.LoadAsync();
        Assert.Equal("", reloaded.Current.SpotifyClientId);
        Assert.Equal(5, reloaded.Current.VolumeStep);
    }

    [Fact]
    public async Task ResetRaisesChangedWithDefaults()
    {
        SettingsService service = NewService();
        await service.LoadAsync();
        service.Update(s => s.SpotifyClientId = "abc123");

        AppSettings? raised = null;
        service.Changed += (_, s) => raised = s;

        service.Reset();

        Assert.NotNull(raised);
        Assert.Equal("", raised!.SpotifyClientId);
        Assert.Same(service.Current, raised);
    }

    [Fact]
    public async Task UpdateRaisesChangedWithNewSettings()
    {
        SettingsService service = NewService();
        await service.LoadAsync();

        AppSettings? raised = null;
        service.Changed += (_, s) => raised = s;

        service.Update(s => s.VolumeStep = 9);

        Assert.NotNull(raised);
        Assert.Equal(9, raised!.VolumeStep);
        Assert.Same(service.Current, raised);
    }

    [Fact]
    public async Task SchemaVersionIsTheFirstPropertyInTheFile()
    {
        SettingsService service = NewService();
        await service.LoadAsync();

        string json = await File.ReadAllTextAsync(_file, TestContext.Current.CancellationToken);
        int schemaIndex = json.IndexOf("\"schemaVersion\"", StringComparison.Ordinal);
        int volumeIndex = json.IndexOf("\"volumeStep\"", StringComparison.Ordinal);

        Assert.True(schemaIndex >= 0);
        Assert.True(volumeIndex < 0 || schemaIndex < volumeIndex);
    }

    [Fact]
    public async Task CorruptFileRecoversToDefaultsAndBacksUp()
    {
        await File.WriteAllTextAsync(_file, "{ this is not valid json", TestContext.Current.CancellationToken);

        SettingsService service = NewService();
        await service.LoadAsync();

        Assert.Equal(5, service.Current.VolumeStep);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.corrupt.bak")));

        // The clean file is valid JSON again.
        string rewritten = await File.ReadAllTextAsync(_file, TestContext.Current.CancellationToken);
        Assert.NotNull(JsonNode.Parse(rewritten));
    }

    [Fact]
    public async Task UpdateLeavesNoTempFiles()
    {
        SettingsService service = NewService();
        await service.LoadAsync();

        service.Update(s => s.VolumeStep = 7);

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task NewerVersionFileResetsToDefaultsAndBacksUp()
    {
        // currentVersion = 1, file claims v2 (a downgrade scenario).
        await File.WriteAllTextAsync(
            _file, "{ \"schemaVersion\": 2, \"volumeStep\": 20 }", TestContext.Current.CancellationToken);

        var service = new SettingsService(_dir, NullLogger<SettingsService>.Instance, [], 1);
        await service.LoadAsync();

        Assert.Equal(5, service.Current.VolumeStep);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.v2.bak")));
    }

    [Fact]
    public async Task OlderVersionFileMigratesAndBacksUp()
    {
        await File.WriteAllTextAsync(
            _file, "{ \"schemaVersion\": 1, \"volumeStep\": 3 }", TestContext.Current.CancellationToken);

        var service = new SettingsService(
            _dir, NullLogger<SettingsService>.Instance, [new StepMigrator(1)], 2);
        await service.LoadAsync();

        Assert.Equal(10, service.Current.VolumeStep); // migrator overwrote the step
        Assert.Equal(2, service.Current.SchemaVersion);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.v1.bak")));
    }

    [Theory]
    [InlineData(9999, 25)]
    [InlineData(30, 25)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public async Task OutOfRangeVolumeStepIsClampedOnLoad(int stored, int expected)
    {
        await File.WriteAllTextAsync(
            _file,
            $"{{ \"schemaVersion\": 1, \"volumeStep\": {stored} }}",
            TestContext.Current.CancellationToken);

        SettingsService service = NewService();
        await service.LoadAsync();

        Assert.Equal(expected, service.Current.VolumeStep);
    }

    [Fact]
    public async Task MigratedOutOfRangeVolumeStepIsClamped()
    {
        await File.WriteAllTextAsync(
            _file, "{ \"schemaVersion\": 1, \"volumeStep\": 3 }", TestContext.Current.CancellationToken);

        // The migrator pushes the step out of range; the load must still clamp it.
        var service = new SettingsService(
            _dir, NullLogger<SettingsService>.Instance, [new OutOfRangeStepMigrator(1)], 2);
        await service.LoadAsync();

        Assert.Equal(25, service.Current.VolumeStep);
    }

    [Fact]
    public async Task ThrowingMigratorResetsToDefaults()
    {
        await File.WriteAllTextAsync(
            _file, "{ \"schemaVersion\": 1, \"volumeStep\": 3 }", TestContext.Current.CancellationToken);

        var service = new SettingsService(
            _dir, NullLogger<SettingsService>.Instance, [new ThrowingMigrator(1)], 2);
        await service.LoadAsync();

        // A faulty migrator degrades to a clean defaults instance rather than crashing.
        Assert.Equal(5, service.Current.VolumeStep);
    }
}
