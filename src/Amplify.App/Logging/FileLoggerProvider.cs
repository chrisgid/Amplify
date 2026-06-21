using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Amplify.App.Logging;

/// <summary>
/// Creates <see cref="FileLogger"/> instances that all share one <see cref="FileLogWriter"/>, so
/// every category writes to the same daily file. Registered through <see cref="FileLoggerExtensions"/>.
/// </summary>
internal sealed class FileLoggerProvider(string directory) : ILoggerProvider
{
    private readonly FileLogWriter _writer = new(directory);
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new FileLogger(name, _writer));

    public void Dispose() => _loggers.Clear();
}
