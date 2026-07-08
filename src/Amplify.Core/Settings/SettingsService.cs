using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Amplify.Core.Settings;

/// <summary>
/// File-backed <see cref="ISettingsService"/>. Persists <see cref="AppSettings"/> as JSON in a given
/// directory, loading and migrating it once at startup and writing atomically on every change. It is
/// UI- and platform-free (plain <c>System.IO</c> + <c>System.Text.Json</c>) so it can be tested
/// against a temporary directory; the application supplies the real per-user data directory.
/// </summary>
/// <remarks>
/// Loading never throws: a missing, unreadable, corrupt, or future-versioned file degrades to
/// defaults (backing up the original first) so the app always launches. Writes are serialised so
/// concurrent updates can't interleave.
/// </remarks>
public sealed partial class SettingsService : ISettingsService
{
    private const string _fileName = "settings.json";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly string _directory;
    private readonly ILogger<SettingsService> _logger;
    private readonly IReadOnlyList<ISettingsMigrator> _migrators;
    private readonly int _currentVersion;
    private readonly Lock _gate = new();

    private AppSettings _current = new();

    /// <summary>Creates a service that reads and writes <c>settings.json</c> in <paramref name="directory"/>.</summary>
    /// <param name="directory">The per-user data directory holding the settings file and backups.</param>
    /// <param name="logger">Sink for load/migration diagnostics; never used to surface UI errors.</param>
    public SettingsService(string directory, ILogger<SettingsService> logger)
        : this(directory, logger, [], AppSettings.CurrentSchemaVersion)
    {
    }

    /// <summary>
    /// Test/extension constructor that injects the migrator set and target version so the
    /// load/migration paths can be exercised without depending on the shipped schema version.
    /// </summary>
    internal SettingsService(
        string directory,
        ILogger<SettingsService> logger,
        IReadOnlyList<ISettingsMigrator> migrators,
        int currentVersion)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(migrators);

        _directory = directory;
        _filePath = Path.Combine(directory, _fileName);
        _logger = logger;
        _migrators = migrators;
        _currentVersion = currentVersion;
    }

    /// <inheritdoc />
    public AppSettings Current => _current;

    /// <inheritdoc />
    public event EventHandler<AppSettings>? Changed;

    /// <inheritdoc />
    public T Get<T>(Func<AppSettings, T> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(_current);
    }

    /// <inheritdoc />
    public void Update(Action<AppSettings> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        AppSettings updated;
        lock (_gate)
        {
            updated = Clone(_current);
            mutate(updated);
            updated.SchemaVersion = _currentVersion;
            Persist(updated);
            _current = updated;
        }

        Changed?.Invoke(this, updated);
    }

    /// <inheritdoc />
    public void Reset()
    {
        AppSettings defaults;
        lock (_gate)
        {
            defaults = new AppSettings { SchemaVersion = _currentVersion };
            Persist(defaults);
            _current = defaults;
        }

        Changed?.Invoke(this, defaults);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Single-caller by contract: the file read runs outside <c>_gate</c>, so this must not be
    /// interleaved with <see cref="Update"/> (see the interface remarks).
    /// </remarks>
    public async Task LoadAsync()
    {
        AppSettings loaded;
        lock (_gate)
        {
            // Establish defaults up front so any early return still leaves a usable Current.
            _current = new AppSettings();
        }

        try
        {
            if (!File.Exists(_filePath))
            {
                loaded = new AppSettings();
                Directory.CreateDirectory(_directory);
                Persist(loaded);
            }
            else
            {
                string json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
                loaded = LoadFromJson(json);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LogLoadFailed(_logger, ex);
            loaded = ResetToDefaults("corrupt");
        }

        lock (_gate)
        {
            _current = loaded;
        }
    }

    private AppSettings LoadFromJson(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
        {
            LogUnreadable(_logger);
            return ResetToDefaults("corrupt");
        }

        int fileVersion = ReadSchemaVersion(root);
        if (fileVersion < 0)
        {
            LogUnreadable(_logger);
            return ResetToDefaults("corrupt");
        }

        SettingsMigrationResult result =
            SettingsMigrationRunner.Run(root, fileVersion, _currentVersion, _migrators);

        switch (result.Outcome)
        {
            case SettingsMigrationOutcome.Loaded:
                AppSettings? loaded = Deserialize(root);
                return loaded is null ? HandleUndeserialisable(fileVersion) : Normalize(loaded);

            case SettingsMigrationOutcome.Migrated:
                AppSettings? migrated = Deserialize(result.Root!);
                if (migrated is null)
                {
                    return HandleUndeserialisable(fileVersion);
                }

                Normalize(migrated);
                Backup($"v{fileVersion}");
                Persist(migrated);
                LogMigrated(_logger, fileVersion, _currentVersion);
                return migrated;

            case SettingsMigrationOutcome.ResetToDefaults:
            default:
                LogResetFromVersion(_logger, fileVersion, _currentVersion);
                return ResetToDefaults($"v{fileVersion}");
        }
    }

    private AppSettings HandleUndeserialisable(int fileVersion)
    {
        LogUnreadable(_logger);
        return ResetToDefaults($"v{fileVersion}");
    }

    // Backs up any existing file, then writes a clean defaults file so the next launch is healthy.
    private AppSettings ResetToDefaults(string? backupLabel = null)
    {
        if (backupLabel is not null)
        {
            Backup(backupLabel);
        }

        var defaults = new AppSettings();
        try
        {
            Persist(defaults);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the clean file is best-effort; an in-memory defaults instance is enough to run.
            LogPersistFailed(_logger, ex);
        }

        return defaults;
    }

    private static int ReadSchemaVersion(JsonObject root)
    {
        if (root.TryGetPropertyValue("schemaVersion", out JsonNode? node) &&
            node is not null &&
            node.GetValueKind() == JsonValueKind.Number)
        {
            try
            {
                return node.GetValue<int>();
            }
            catch (FormatException)
            {
                return -1;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        return -1;
    }

    private static AppSettings? Deserialize(JsonObject root)
    {
        try
        {
            return root.Deserialize<AppSettings>(_serializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Backup(string label)
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            string backupPath = Path.Combine(_directory, $"settings.{label}.bak");
            File.Copy(_filePath, backupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed backup must not stop recovery; the app still loads with sound settings.
            LogBackupFailed(_logger, ex);
        }
    }

    // Atomic write: serialise to a sibling temp file, then move it over the target so a crash
    // mid-write can never leave a half-written settings file.
    private void Persist(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        string json = JsonSerializer.Serialize(settings, _serializerOptions);
        string tempPath = Path.Combine(_directory, $"settings.{Guid.NewGuid():N}.tmp");

        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }

    // Enforces value invariants the rest of the app relies on, so a hand-edited or mis-migrated file
    // can't surface an out-of-range value to readers that don't re-validate (e.g. the hotkey/step math).
    private static AppSettings Normalize(AppSettings settings)
    {
        settings.VolumeStep = Math.Clamp(settings.VolumeStep, AppSettings.MinVolumeStep, AppSettings.MaxVolumeStep);
        return settings;
    }

    private static AppSettings Clone(AppSettings source) =>
        JsonSerializer.Deserialize<AppSettings>(
            JsonSerializer.Serialize(source, _serializerOptions), _serializerOptions)!;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Could not read settings; falling back to defaults.")]
    private static partial void LogLoadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Settings file was unreadable or malformed; falling back to defaults.")]
    private static partial void LogUnreadable(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Migrated settings from schema v{FromVersion} to v{ToVersion}.")]
    private static partial void LogMigrated(ILogger logger, int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Settings schema v{FromVersion} cannot be loaded by v{ToVersion}; resetting to defaults.")]
    private static partial void LogResetFromVersion(ILogger logger, int fromVersion, int toVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not back up the settings file.")]
    private static partial void LogBackupFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not persist the settings file.")]
    private static partial void LogPersistFailed(ILogger logger, Exception exception);
}
