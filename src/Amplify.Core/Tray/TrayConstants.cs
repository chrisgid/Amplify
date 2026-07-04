namespace Amplify.Core.Tray;

/// <summary>Centralised, non-magic constants for the tray/background feature.</summary>
public static class TrayConstants
{
    /// <summary>
    /// Identifier for the packaged startup task. Must match the <c>TaskId</c> of the
    /// <c>windows.startupTask</c> extension in the app manifest, since <c>StartupTask.GetAsync</c> looks
    /// the task up by this id.
    /// </summary>
    public const string StartupTaskId = "AmplifyStartupTask";

    /// <summary>Key used to register the single running instance for activation redirection.</summary>
    public const string SingleInstanceKey = "Amplify-Main";
}
