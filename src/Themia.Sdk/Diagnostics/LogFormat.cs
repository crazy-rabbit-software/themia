using System.Globalization;
using System.Text;

namespace Themia.Core.Diagnostics;

/// <summary>
/// [S-984] How a record reads in the file, in the event log and on the clipboard: one header line
/// (local time with offset, level, scope, message) and the exception, if any, indented under it so
/// a reader can tell where one record ends.
/// </summary>
public static class LogFormat
{
    /// <summary>The level as written: fixed width, so the scopes line up in a text editor.</summary>
    public static string Level(LogLevel level) => level switch
    {
        LogLevel.Debug => "DEBUG",
        LogLevel.Info => "INFO ",
        LogLevel.Warn => "WARN ",
        LogLevel.Error => "ERROR",
        _ => "?    ",
    };

    /// <summary>The whole record, without a trailing newline, cut to <see cref="LogFileRules.MaxRecordChars"/>.</summary>
    public static string Record(LogRecord record)
    {
        var text = new StringBuilder(128)
            .Append(record.At.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(' ')
            .Append(Level(record.Level))
            .Append(' ')
            .Append(record.Scope)
            .Append(": ")
            .Append(OneLine(record.Message));

        if (!string.IsNullOrEmpty(record.Detail))
        {
            foreach (var line in record.Detail.Split('\n'))
            {
                text.Append('\n').Append("    ").Append(line.TrimEnd('\r'));
            }
        }

        if (text.Length > LogFileRules.MaxRecordChars)
        {
            var cut = LogFileRules.MaxRecordChars;
            if (char.IsHighSurrogate(text[cut - 1]))
            {
                cut--;
            }

            text.Length = cut;
            text.Append(" … (record truncated)");
        }

        return text.ToString();
    }

    /// <summary>
    /// A message's own line breaks become " ⏎ " so a message can never forge what looks like the
    /// start of another record in the file.
    /// </summary>
    public static string OneLine(string message)
        => message.Contains('\n', StringComparison.Ordinal) || message.Contains('\r', StringComparison.Ordinal)
            ? message.Replace("\r\n", " ⏎ ", StringComparison.Ordinal).Replace('\r', ' ').Replace("\n", " ⏎ ", StringComparison.Ordinal)
            : message;
}
