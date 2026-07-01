using System.Runtime.InteropServices;
using Amplify.Core.Tray;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;

namespace Amplify.App.Tray;

/// <summary>
/// Wraps the packaged <see cref="StartupTask"/> API behind <see cref="IStartupTaskManager"/>. The task
/// itself is declared in the app manifest (a <c>windows.startupTask</c> extension) and looked up here by
/// the shared id. Where the platform API is unavailable — an unpackaged or headless run — every call
/// degrades to <see cref="StartupState.Disabled"/> so the rest of the app keeps working.
/// </summary>
public sealed partial class StartupTaskManager : IStartupTaskManager
{
    private readonly ILogger<StartupTaskManager> _logger;

    public StartupTaskManager(ILogger<StartupTaskManager> logger) => _logger = logger;

    /// <inheritdoc />
    public Task<StartupState> GetStateAsync() =>
        WithTaskAsync(async task => Map(task.State));

    /// <inheritdoc />
    public Task<StartupState> TryEnableAsync() =>
        WithTaskAsync(async task => Map(await task.RequestEnableAsync()));

    /// <inheritdoc />
    public Task<StartupState> DisableAsync() =>
        WithTaskAsync(task =>
        {
            task.Disable();
            return Task.FromResult(Map(task.State));
        });

    private async Task<StartupState> WithTaskAsync(Func<StartupTask, Task<StartupState>> operation)
    {
        try
        {
            StartupTask task = await StartupTask.GetAsync(TrayConstants.StartupTaskId);
            return await operation(task);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or ArgumentException)
        {
            // No package identity (unpackaged/headless run) or the task isn't registered: report it as
            // simply not enabled rather than surfacing a platform error to the user.
            LogStartupTaskUnavailable(_logger, ex);
            return StartupState.Disabled;
        }
    }

    private static StartupState Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Enabled => StartupState.Enabled,
        StartupTaskState.EnabledByPolicy => StartupState.EnabledByPolicy,
        StartupTaskState.DisabledByPolicy => StartupState.DisabledByPolicy,
        StartupTaskState.DisabledByUser => StartupState.DisabledByUser,
        _ => StartupState.Disabled,
    };

    [LoggerMessage(Level = LogLevel.Debug, Message = "Startup task is unavailable; treating launch-at-startup as disabled.")]
    private static partial void LogStartupTaskUnavailable(ILogger logger, Exception exception);
}
