using Microsoft.Extensions.Logging;

namespace Amplify.App.Logging;

/// <summary>
/// An <see cref="ILogger"/> that formats each entry as a single line and hands it to a shared
/// <see cref="FileLogWriter"/>. Deliberately minimal — there is no third-party logging sink — and it
/// never writes tokens or PII beyond what callers pass in their messages.
/// </summary>
internal sealed class FileLogger(string category, FileLogWriter writer) : ILogger
{
    IDisposable? ILogger.BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        string message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        string line = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fffK} [{Level(logLevel)}] {category}: {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + exception;
        }

        writer.AppendLine(line);
    }

    // Fixed-width tags keep the columns aligned and readable in a plain-text viewer.
    private static string Level(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRACE",
        LogLevel.Debug => "DEBUG",
        LogLevel.Information => "INFO ",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Critical => "CRIT ",
        _ => "     ",
    };
}
