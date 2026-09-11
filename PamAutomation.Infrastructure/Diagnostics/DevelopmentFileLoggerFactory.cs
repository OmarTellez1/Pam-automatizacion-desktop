using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace PamAutomation.Infrastructure.Diagnostics;

/// <summary>Creates development-only loggers that write redacted diagnostics under the repository root.</summary>
public sealed class DevelopmentFileLoggerFactory : ILoggerFactory
{
    private const string EnableEnvironmentVariable = "PAM_AUTOMATION_DEVELOPMENT_LOGGING";
    private static readonly TimeSpan Retention = TimeSpan.FromDays(2);
    private readonly DevelopmentLogSink sink;
    private bool disposed;

    /// <summary>Creates a logger factory for an explicitly supplied development log directory.</summary>
    /// <param name="logDirectory">Directory that will contain daily log files.</param>
    /// <param name="timeProvider">Clock used for timestamps and retention.</param>
    public DevelopmentFileLoggerFactory(string logDirectory, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        sink = new DevelopmentLogSink(Path.GetFullPath(logDirectory), timeProvider ?? TimeProvider.System, Retention);
    }

    /// <summary>
    /// Creates a repository logger when development diagnostics are explicitly active; otherwise returns a null logger.
    /// </summary>
    /// <returns>A development file logger or a no-op logger factory.</returns>
    public static ILoggerFactory CreateForCurrentProcess()
    {
        bool explicitlyEnabled = Debugger.IsAttached ||
            string.Equals(Environment.GetEnvironmentVariable(EnableEnvironmentVariable), "1", StringComparison.Ordinal);
        if (!explicitlyEnabled || !TryFindRepositoryRoot(out string repositoryRoot))
        {
            return NullLoggerFactory.Instance;
        }

        return new DevelopmentFileLoggerFactory(Path.Combine(repositoryRoot, "logs"));
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(categoryName);
        return new DevelopmentFileLogger(categoryName, sink);
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(provider);
        throw new NotSupportedException("La fábrica de diagnóstico utiliza exclusivamente su proveedor de archivo seguro.");
    }

    /// <inheritdoc />
    public void Dispose() => disposed = true;

    private static bool TryFindRepositoryRoot(out string repositoryRoot)
    {
        foreach (string startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            DirectoryInfo? current = new(Path.GetFullPath(startPath));
            while (current is not null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
                    File.Exists(Path.Combine(current.FullName, "PamAutomation.slnx")))
                {
                    repositoryRoot = current.FullName;
                    return true;
                }

                current = current.Parent;
            }
        }

        repositoryRoot = string.Empty;
        return false;
    }

    private sealed class DevelopmentFileLogger(string categoryName, DevelopmentLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            ArgumentNullException.ThrowIfNull(formatter);
            sink.Write(logLevel, categoryName, eventId, formatter(state, exception), exception);
        }
    }

    private sealed class DevelopmentLogSink
    {
        private readonly object gate = new();
        private readonly string directory;
        private readonly TimeProvider timeProvider;
        private readonly TimeSpan retention;

        public DevelopmentLogSink(string directory, TimeProvider timeProvider, TimeSpan retention)
        {
            this.directory = directory;
            this.timeProvider = timeProvider;
            this.retention = retention;
            lock (gate)
            {
                Directory.CreateDirectory(directory);
                DeleteExpiredFiles(timeProvider.GetUtcNow());
            }
        }

        public void Write(LogLevel level, string category, EventId eventId, string message, Exception? exception)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string filePath = Path.Combine(directory, $"pam-automation-{now:yyyy-MM-dd}.log");
            var line = new StringBuilder(256)
                .Append(now.ToString("O", CultureInfo.InvariantCulture))
                .Append('|').Append(ToLabel(level))
                .Append('|').Append(Sanitize(category))
                .Append("|EventId=").Append(eventId.Id)
                .Append('|').Append(Sanitize(message));
            AppendExceptionMetadata(line, exception);
            line.AppendLine();

            lock (gate)
            {
                Directory.CreateDirectory(directory);
                DeleteExpiredFiles(now);
                File.AppendAllText(filePath, line.ToString(), new UTF8Encoding(false));
            }
        }

        private void DeleteExpiredFiles(DateTimeOffset now)
        {
            DateTime cutoffUtc = now.UtcDateTime.Subtract(retention);
            foreach (string filePath in Directory.EnumerateFiles(directory, "pam-automation-*.log", SearchOption.TopDirectoryOnly))
            {
                if (File.GetLastWriteTimeUtc(filePath) < cutoffUtc)
                {
                    File.Delete(filePath);
                }
            }
        }

        private static void AppendExceptionMetadata(StringBuilder line, Exception? exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                line.Append("|ExceptionType=").Append(Sanitize(current.GetType().FullName ?? current.GetType().Name));
                line.Append("|HResult=0x").Append(current.HResult.ToString("X8", CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(current.StackTrace))
                {
                    line.Append("|StackTrace=").Append(Sanitize(current.StackTrace));
                }
            }
        }

        private static string Sanitize(string value) => value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('|', '/');

        private static string ToLabel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            LogLevel.Critical => "CRITICAL",
            _ => "NONE",
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();
        public void Dispose() { }
    }
}
