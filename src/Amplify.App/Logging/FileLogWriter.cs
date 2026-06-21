using System.Text;

namespace Amplify.App.Logging;

/// <summary>
/// Appends formatted log lines to a daily-rolling text file in a target directory, serialising
/// writes so loggers on different threads can share one writer safely. One file per UTC day keeps
/// the log small and easy to find; old files are left in place for the user to clear.
/// </summary>
internal sealed class FileLogWriter
{
    private readonly string _directory;
    private readonly object _gate = new();

    public FileLogWriter(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Appends a single already-formatted line (a trailing newline is added).</summary>
    public void AppendLine(string line)
    {
        // Append-and-close per write: log volume is low, and this avoids holding a file handle for
        // the life of the app (which would also block the user from deleting today's log).
        lock (_gate)
        {
            try
            {
                File.AppendAllText(CurrentFilePath(), line + Environment.NewLine, Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging must never take the app down. A transient file error (e.g. the folder is
                // momentarily locked) is dropped rather than propagated to the caller.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private string CurrentFilePath() =>
        Path.Combine(_directory, $"amplify-{DateTime.UtcNow:yyyyMMdd}.log");
}
