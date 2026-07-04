namespace Amplify.Core.Tray;

/// <summary>
/// The effective state of the OS "launch at startup" registration, mirroring the platform's own
/// startup-task states. The user is always in ultimate control: they can disable the entry from Task
/// Manager or Settings, and a policy can force it on or off, in which case the app cannot override them.
/// </summary>
public enum StartupState
{
    /// <summary>Enabled: the app will launch when the user signs in.</summary>
    Enabled,

    /// <summary>Disabled by the app (or never enabled); the app may request to enable it.</summary>
    Disabled,

    /// <summary>Disabled by the user via Task Manager/Settings; the app cannot re-enable it programmatically.</summary>
    DisabledByUser,

    /// <summary>Disabled by group policy; the app cannot enable it.</summary>
    DisabledByPolicy,

    /// <summary>Forced on by group policy; the app cannot disable it.</summary>
    EnabledByPolicy,
}
