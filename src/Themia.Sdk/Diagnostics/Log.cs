namespace Themia.Core.Diagnostics;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// One record as every sink receives it. <see cref="Message"/> and <see cref="Detail"/> (the
/// exception's full text, inner exceptions and stack included) are already redacted
/// (<see cref="LogRedactor"/>): no sink can write a secret, because no sink is given one.
/// </summary>
public readonly record struct LogRecord(DateTimeOffset At, LogLevel Level, string Scope, string Message, string? Detail);

/// <summary>
/// Minimal sink-based log. Nothing here writes to disk by default — the host installs a sink. The
/// point is that no error path is allowed to be silent: "handled" means logged and, where the user
/// would notice, surfaced.
/// <para>
/// [S-981] Records under <see cref="Floor"/> are dropped before anything else happens to them.
/// [S-983] The rest are redacted here, once, before any sink sees them.
/// </para>
/// </summary>
public static class Log
{
    private static readonly List<Action<LogRecord>> Sinks = [];
    private static readonly Lock Gate = new();
    private static volatile int _floor = (int)LogLevel.Debug;

    /// <summary>
    /// The lowest level recorded (<see cref="LogPolicy.Floor"/>). Everything until the host decides,
    /// so a test or a tool that never sets it loses nothing.
    /// </summary>
    public static LogLevel Floor
    {
        get => (LogLevel)_floor;
        set => _floor = (int)value;
    }

    /// <summary>Whether a record at <paramref name="level"/> would be kept; for a caller whose message is costly to build.</summary>
    public static bool IsEnabled(LogLevel level) => (int)level >= _floor;

    public static void AddSink(Action<LogRecord> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        lock (Gate)
        {
            Sinks.Add(sink);
        }
    }

    public static void Debug(string scope, string message) => Write(LogLevel.Debug, scope, message, null);

    public static void Info(string scope, string message) => Write(LogLevel.Info, scope, message, null);

    public static void Warn(string scope, string message, Exception? error = null) => Write(LogLevel.Warn, scope, message, error);

    public static void Error(string scope, string message, Exception? error = null) => Write(LogLevel.Error, scope, message, error);

    private static void Write(LogLevel level, string scope, string message, Exception? error)
    {
        if ((int)level < _floor)
        {
            return;
        }

        var record = new LogRecord(
            DateTimeOffset.Now,
            level,
            scope,
            LogRedactor.Redact(message),
            error is null ? null : LogRedactor.Redact(error.ToString()));

        Action<LogRecord>[] snapshot;
        lock (Gate)
        {
            snapshot = [.. Sinks];
        }

        foreach (var sink in snapshot)
        {
            try
            {
                sink(record);
            }
            catch (Exception ex)
            {
                // A failing sink must never take down the caller's error path, and must never
                // recurse back into Log. Last resort only.
                System.Diagnostics.Debug.WriteLine($"[themia] log sink threw: {ex}");
            }
        }
    }
}
