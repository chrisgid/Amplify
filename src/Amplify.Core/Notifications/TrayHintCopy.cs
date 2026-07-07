namespace Amplify.Core.Notifications;

/// <summary>
/// The localised text of the one-time "still running in the tray" hint. Kept as an injected value so
/// the show-once policy in <see cref="INotificationService"/> stays free of any UI/resource
/// dependency (and unit-testable); the app layer supplies the strings from its resource file.
/// </summary>
/// <param name="Title">The balloon title.</param>
/// <param name="Message">The balloon body.</param>
public sealed record TrayHintCopy(string Title, string Message);
