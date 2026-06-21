using Microsoft.Extensions.Logging;
using Windows.Storage;

namespace Amplify.App.Logging;

/// <summary>Registration helpers for the minimal file logger.</summary>
internal static class FileLoggerExtensions
{
    /// <summary>
    /// Adds the rolling file logger, writing to <c>logs\</c> under the app's local data folder. Falls
    /// back to a temp directory if the local folder can't be resolved (e.g. running unpackaged).
    /// </summary>
    public static ILoggingBuilder AddFileLogging(this ILoggingBuilder builder)
    {
        builder.AddProvider(new FileLoggerProvider(ResolveLogDirectory()));
        return builder;
    }

    private static string ResolveLogDirectory()
    {
        try
        {
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, "logs");
        }
        catch (InvalidOperationException)
        {
            // No package identity (unpackaged run): keep logging working off a temp location.
            return Path.Combine(Path.GetTempPath(), "Amplify", "logs");
        }
    }
}
