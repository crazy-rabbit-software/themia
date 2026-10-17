namespace Themia.Core.Diagnostics;

/// <summary>What the Application event log mirror does with one error record ([S-986]).</summary>
public enum EventLogEmit
{
    /// <summary>Under the cap: write the record.</summary>
    Record,

    /// <summary>
    /// Exactly at the cap: write one notice that the rest are suppressed, so a reader of Event
    /// Viewer never mistakes the silence for "the errors stopped".
    /// </summary>
    FinalNotice,

    /// <summary>Over the cap: write nothing.</summary>
    Suppressed,
}

/// <summary>
/// The level policy ([S-981]) and the event log mirror's limits ([S-986]): which records
/// are kept at all, and how many of the errors reach the machine-wide event log.
/// </summary>
public static class LogPolicy
{
    /// <summary>
    /// Errors per session mirrored into the Application event log. That log is shared and bounded
    /// machine-wide; an error inside a retry loop must not push other software's events out of it.
    /// The file log is not capped by count (its size is bounded by rotation instead).
    /// </summary>
    public const int EventLogCap = 25;

    /// <summary>
    /// An event's text is cut here. Event Viewer's detail pane is unreadable past a screenful and
    /// the full record is in the file.
    /// </summary>
    public const int EventLogMaxChars = 2_000;

    /// <summary>
    /// [S-981] The lowest level that is recorded at all. Warnings and errors always are — they are
    /// rare, and the one that preceded a "it just stopped working" report has to be on disk already.
    /// Info and debug only in a debug build, where the developer is the reader. A record under the
    /// floor is dropped in <see cref="Log"/> before it is redacted or formatted, so a quiet app pays
    /// nothing for its debug lines.
    /// </summary>
    public static LogLevel Floor(bool debugBuild)
        => debugBuild ? LogLevel.Debug : LogLevel.Warn;

    /// <summary>[S-986] What to do with an error when <paramref name="seenBefore"/> were mirrored before it.</summary>
    public static EventLogEmit EventDecision(int seenBefore) => seenBefore switch
    {
        < EventLogCap => EventLogEmit.Record,
        EventLogCap => EventLogEmit.FinalNotice,
        _ => EventLogEmit.Suppressed,
    };

    /// <summary>
    /// [S-986] Cut a record to <see cref="EventLogMaxChars"/> without splitting a surrogate pair:
    /// half a character would go into the event as a replacement glyph, or be refused.
    /// </summary>
    public static string ForEventLog(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length <= EventLogMaxChars)
        {
            return text;
        }

        var cut = EventLogMaxChars;
        if (char.IsHighSurrogate(text[cut - 1]))
        {
            cut--;
        }

        return text[..cut] + "… (truncated; the log file has the full record)";
    }

    /// <summary>The notice written once the cap is reached.</summary>
    public static string SuppressionNotice(string logFolder)
        => $"Themia stopped writing errors to the event log after {EventLogCap} in this session. The full record is in {logFolder}.";
}
