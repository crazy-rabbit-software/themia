namespace Themia.Sdk.Kit;

/// <summary>
/// [S-14319] A running reading's widest form (owner, 2026-11-20: "the clock right now changes its layout depending on the
/// time that it shows because not all of the chars have the same width"). A face that shows a time or a count that runs
/// (a clock, a stopwatch, a countdown, a timer) is sized and stood by its format's widest value, never by the value it
/// shows now: its figures are tabular (every digit one width) and the value is held to its template, the format's text
/// with every digit the widest (<see cref="Digit"/>) and as many places as the format ever shows; a shorter value (an hour
/// of one digit) is padded at its start with figure spaces, a digit's width each, so its figures stand where the
/// template's do. Pure.
/// </summary>
public static class FigureTemplate
{
    /// <summary>The digit a template is written with: every digit is one width in tabular figures; its ink is a full one.</summary>
    public const char Digit = '8';

    /// <summary>A figure space: as wide as a tabular digit, no ink.</summary>
    public const char FigureSpace = '\u2007';

    /// <summary>
    /// The template of <paramref name="text"/>: every digit its script's 8 (Arabic's ٨ for ٩), a figure space a Latin 8,
    /// everything else kept.
    /// </summary>
    public static string Of(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c == FigureSpace ? Digit
                    : char.IsDigit(c) ? (char)(c - (int)char.GetNumericValue(c) + (Digit - '0'))
                    : c;
            }
        });
    }

    /// <summary>
    /// The template of a format shown as each of <paramref name="forms"/> (a clock's time at a two-digit hour in 12- and
    /// 24-hour time, a stopwatch past its first hour): line by line, the longest line's template, so every value the
    /// format shows fits it.
    /// </summary>
    public static string Widest(IEnumerable<string> forms)
    {
        ArgumentNullException.ThrowIfNull(forms);
        var lines = new List<string>();
        foreach (var form in forms)
        {
            var parts = (form ?? string.Empty).Split('\n');
            for (var i = 0; i < parts.Length; i++)
            {
                var line = Of(parts[i]);
                if (i == lines.Count)
                {
                    lines.Add(line);
                }
                else if (line.Length > lines[i].Length)
                {
                    lines[i] = line;
                }
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// [S-14319] The template the clock's figures are held to: its time at a two-digit hour ("88:88:88", "88:88"), in the
    /// language's own digits (<paramref name="time"/>: the format of 10:58:58, or of 10:58 without seconds).
    /// </summary>
    public static string OfTime(string time) => Of(time ?? string.Empty);

    /// <summary>
    /// <paramref name="value"/> held to <paramref name="template"/>: each line shorter than the template's padded at its
    /// start with figure spaces to its length (an hour of one digit where the template has two), so its figures stand where
    /// the template's do; a line as long or longer, or a value of another shape (a placeholder), as it is.
    /// </summary>
    public static string Pad(string value, string template)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(template);
        if (template.Length == 0)
        {
            return value;
        }

        var lines = value.Split('\n');
        var held = template.Split('\n');
        for (var i = 0; i < lines.Length && i < held.Length; i++)
        {
            var line = lines[i];
            if (line.Length < held[i].Length && line.Length > 0 && char.IsDigit(line[0]))
            {
                // Only where it is the template's shape, its places short at the start ("9:41" of "88:88"); a reading of
                // another shape ("3d 5h" of "88d 88h") stands as it is in the template's room.
                var missing = held[i].Length - line.Length;
                if (held[i].EndsWith(Of(line), StringComparison.Ordinal) && held[i][..missing].All(char.IsDigit))
                {
                    lines[i] = new string(FigureSpace, missing) + line;
                }
            }
        }

        return string.Join('\n', lines);
    }
}
