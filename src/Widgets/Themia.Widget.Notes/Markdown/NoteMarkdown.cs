using System.Text.RegularExpressions;

namespace Themia.Core.Notes;

/// <summary>[S-1350] The block kind of one source line.</summary>
public enum NoteLineKind
{
    /// <summary>Plain text, and blank lines.</summary>
    Paragraph,
    Heading1,
    Heading2,
    Heading3,

    /// <summary><c>- item</c>, <c>* item</c>, <c>+ item</c>.</summary>
    Bullet,

    /// <summary><c>1. item</c>, <c>1) item</c>.</summary>
    Ordered,

    /// <summary><c>- [ ] item</c> / <c>- [x] item</c>.</summary>
    Task,

    /// <summary><c>&gt; quoted</c>.</summary>
    Quote,

    /// <summary><c>---</c>, <c>***</c>, <c>___</c>.</summary>
    Rule,

    /// <summary>The <c>```</c> / <c>~~~</c> line itself.</summary>
    Fence,

    /// <summary>A line inside a fenced block.</summary>
    Code,
}

/// <summary>Presentation flags of one run of source text.</summary>
[Flags]
public enum NoteSpanStyle
{
    None = 0,

    /// <summary>Markdown syntax rather than content: drawn dimmed, never hidden.</summary>
    Marker = 1,
    Bold = 2,
    Italic = 4,
    Strike = 8,
    Code = 16,

    /// <summary>The <c>[ ]</c> / <c>[x]</c> of a task line: the run a click toggles.</summary>
    Box = 32,
}

/// <summary>One styled run. <see cref="Text"/> is verbatim source; the style is presentation only.</summary>
public readonly record struct NoteSpan(string Text, NoteSpanStyle Style)
{
    public bool Has(NoteSpanStyle flag) => (Style & flag) == flag;
}

/// <summary>One parsed source line.</summary>
/// <param name="Indent">Leading indent depth, two spaces or one tab a level, capped at 6.</param>
/// <param name="Checked">Task lines only.</param>
public sealed record NoteLine(NoteLineKind Kind, int Indent, bool Checked, IReadOnlyList<NoteSpan> Spans)
{
    /// <summary>The line's source, rebuilt from its spans ([S-1352]: always the exact source).</summary>
    public string Source => string.Concat(Spans.Select(s => s.Text));

    /// <summary>Same kind, indent, state and spans — i.e. it would be drawn identically.</summary>
    public bool Matches(NoteLine? other) =>
        other is not null && Kind == other.Kind && Indent == other.Indent && Checked == other.Checked && Spans.SequenceEqual(other.Spans);
}

/// <summary>
/// [S-1350] The note's markdown, tokenised for drawing in place: the user always edits the source,
/// and the syntax markers stay on screen, dimmed, while their effect is applied to the text around
/// them. A port of the original <c>markdown.ts</c>.
/// <para>
/// [S-1352] The invariant the editor stands on: <b>the spans of a line, concatenated, are exactly the
/// source line.</b> No span adds, drops, reorders or rewrites a character; a delimiter with no
/// partner on the same line (<c>**oops</c>) degrades to literal text. That is what lets the editor
/// apply a span's formatting at the span's own offsets, and what keeps the caret and the saved note
/// in step.
/// </para>
/// </summary>
public static partial class NoteMarkdown
{
    /// <summary>The deepest indent level drawn differently.</summary>
    public const int MaxIndent = 6;

    [GeneratedRegex(@"^\s*(?:```|~~~)")]
    private static partial Regex FenceRegex();

    // A heading needs a space after the hashes, so `#tag` stays plain text.
    [GeneratedRegex(@"^(\s*)(#{1,3})(\s+)(.*)$", RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s*(?:-{3,}|\*{3,}|_{3,})\s*$")]
    private static partial Regex RuleRegex();

    [GeneratedRegex(@"^(\s*)(>\s?)(.*)$", RegexOptions.Singleline)]
    private static partial Regex QuoteRegex();

    [GeneratedRegex(@"^(\s*)([-*+] )(\[[ xX]\])( ?)(.*)$", RegexOptions.Singleline)]
    private static partial Regex TaskRegex();

    [GeneratedRegex(@"^(\s*)([-*+] )(.*)$", RegexOptions.Singleline)]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"^(\s*)([0-9]{1,9}[.)] )(.*)$", RegexOptions.Singleline)]
    private static partial Regex OrderedRegex();

    /// <summary>
    /// Tokenise a note: always one line per source line (a trailing newline gives a final empty
    /// line), so a line's index is its source line's index.
    /// </summary>
    public static List<NoteLine> Parse(string? text)
    {
        var lines = (text ?? string.Empty).Split('\n');
        var parsed = new List<NoteLine>(lines.Length);
        var inFence = false;

        foreach (var line in lines)
        {
            var one = ParseLine(line, inFence);
            if (one.Kind == NoteLineKind.Fence)
            {
                inFence = !inFence;
            }

            parsed.Add(one);
        }

        return parsed;
    }

    /// <summary>Indent level of a run of leading whitespace: two spaces or one tab a level.</summary>
    private static int IndentOf(string whitespace)
    {
        var columns = 0;
        foreach (var c in whitespace)
        {
            columns += c == '\t' ? 2 : 1;
        }

        return Math.Min(MaxIndent, columns / 2);
    }

    private static NoteLine ParseLine(string line, bool inFence)
    {
        // Before the in-fence branch, or the closing fence would be swallowed as code and the block
        // would run to the end of the note.
        if (FenceRegex().IsMatch(line))
        {
            return new NoteLine(NoteLineKind.Fence, 0, false, line.Length == 0 ? [] : [new NoteSpan(line, NoteSpanStyle.Marker)]);
        }

        if (inFence)
        {
            return new NoteLine(NoteLineKind.Code, 0, false, line.Length == 0 ? [] : [new NoteSpan(line, NoteSpanStyle.Code)]);
        }

        if (RuleRegex().IsMatch(line) && line.Trim().Length >= 3)
        {
            return new NoteLine(NoteLineKind.Rule, 0, false, [new NoteSpan(line, NoteSpanStyle.Marker)]);
        }

        var spans = new List<NoteSpan>();

        if (HeadingRegex().Match(line) is { Success: true } heading)
        {
            Push(spans, heading.Groups[1].Value + heading.Groups[2].Value + heading.Groups[3].Value, NoteSpanStyle.Marker);
            Inline(heading.Groups[4].Value, spans, NoteSpanStyle.None);
            var kind = heading.Groups[2].Length switch
            {
                1 => NoteLineKind.Heading1,
                2 => NoteLineKind.Heading2,
                _ => NoteLineKind.Heading3,
            };
            return new NoteLine(kind, 0, false, spans);
        }

        if (QuoteRegex().Match(line) is { Success: true } quote)
        {
            Push(spans, quote.Groups[1].Value + quote.Groups[2].Value, NoteSpanStyle.Marker);
            Inline(quote.Groups[3].Value, spans, NoteSpanStyle.None);
            return new NoteLine(NoteLineKind.Quote, IndentOf(quote.Groups[1].Value), false, spans);
        }

        if (TaskRegex().Match(line) is { Success: true } task)
        {
            var box = task.Groups[3].Value;
            var isChecked = box[1] != ' ';
            Push(spans, task.Groups[1].Value + task.Groups[2].Value, NoteSpanStyle.Marker);
            Push(spans, box, NoteSpanStyle.Marker | NoteSpanStyle.Box);
            Push(spans, task.Groups[4].Value, NoteSpanStyle.Marker);

            // A done item reads as done: struck through (and dimmed by the editor).
            Inline(task.Groups[5].Value, spans, isChecked ? NoteSpanStyle.Strike : NoteSpanStyle.None);
            return new NoteLine(NoteLineKind.Task, IndentOf(task.Groups[1].Value), isChecked, spans);
        }

        if (BulletRegex().Match(line) is { Success: true } bullet)
        {
            Push(spans, bullet.Groups[1].Value + bullet.Groups[2].Value, NoteSpanStyle.Marker);
            Inline(bullet.Groups[3].Value, spans, NoteSpanStyle.None);
            return new NoteLine(NoteLineKind.Bullet, IndentOf(bullet.Groups[1].Value), false, spans);
        }

        if (OrderedRegex().Match(line) is { Success: true } ordered)
        {
            Push(spans, ordered.Groups[1].Value + ordered.Groups[2].Value, NoteSpanStyle.Marker);
            Inline(ordered.Groups[3].Value, spans, NoteSpanStyle.None);
            return new NoteLine(NoteLineKind.Ordered, IndentOf(ordered.Groups[1].Value), false, spans);
        }

        Inline(line, spans, NoteSpanStyle.None);
        return new NoteLine(NoteLineKind.Paragraph, 0, false, spans);
    }

    /// <summary>Append a run, merging it into the previous one when the styles match.</summary>
    private static void Push(List<NoteSpan> spans, string text, NoteSpanStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (spans.Count > 0 && spans[^1].Style == style)
        {
            spans[^1] = spans[^1] with { Text = spans[^1].Text + text };
            return;
        }

        spans.Add(new NoteSpan(text, style));
    }

    /// <summary>
    /// <c>_</c> opens and closes only at a word boundary, so <c>snake_case_name</c> stays plain;
    /// <c>*</c> has no such rule (what every editor does in practice).
    /// </summary>
    private static bool UnderscoreOk(string src, int open, int close)
    {
        var before = open > 0 && char.IsLetterOrDigit(src[open - 1]);
        var after = close + 1 < src.Length && char.IsLetterOrDigit(src[close + 1]);
        return !before && !after;
    }

    /// <summary>
    /// [S-1351] Inline emphasis inside <paramref name="src"/>. A delimiter counts only when its closer
    /// is on the same line with at least one character between them; otherwise it is literal text,
    /// so the concatenation invariant holds for any input.
    /// </summary>
    private static void Inline(string src, List<NoteSpan> spans, NoteSpanStyle emphasis)
    {
        var plainStart = 0;
        var i = 0;

        void Flush(int upTo)
        {
            Push(spans, src[plainStart..upTo], emphasis);
        }

        while (i < src.Length)
        {
            var c = src[i];
            var two = i + 1 < src.Length ? src.Substring(i, 2) : string.Empty;

            // `code`: highest precedence, never nests.
            if (c == '`')
            {
                var close = src.IndexOf('`', i + 1);
                if (close > i + 1)
                {
                    Flush(i);
                    Push(spans, "`", emphasis | NoteSpanStyle.Marker);
                    Push(spans, src[(i + 1)..close], emphasis | NoteSpanStyle.Code);
                    Push(spans, "`", emphasis | NoteSpanStyle.Marker);
                    i = close + 1;
                    plainStart = i;
                    continue;
                }
            }

            if (two is "**" or "__" && (emphasis & NoteSpanStyle.Bold) == 0)
            {
                var close = src.IndexOf(two, i + 2, StringComparison.Ordinal);
                if (close > i + 2 && (two == "**" || UnderscoreOk(src, i, close + 1)))
                {
                    Flush(i);
                    Push(spans, two, emphasis | NoteSpanStyle.Marker);
                    Inline(src[(i + 2)..close], spans, emphasis | NoteSpanStyle.Bold);
                    Push(spans, two, emphasis | NoteSpanStyle.Marker);
                    i = close + 2;
                    plainStart = i;
                    continue;
                }
            }

            if (two == "~~" && (emphasis & NoteSpanStyle.Strike) == 0)
            {
                var close = src.IndexOf("~~", i + 2, StringComparison.Ordinal);
                if (close > i + 2)
                {
                    Flush(i);
                    Push(spans, "~~", emphasis | NoteSpanStyle.Marker);
                    Inline(src[(i + 2)..close], spans, emphasis | NoteSpanStyle.Strike);
                    Push(spans, "~~", emphasis | NoteSpanStyle.Marker);
                    i = close + 2;
                    plainStart = i;
                    continue;
                }
            }

            if (c is '*' or '_' && (emphasis & NoteSpanStyle.Italic) == 0)
            {
                var close = src.IndexOf(c, i + 1);

                // A `**` run is handled above; a lone `*` must not eat into one.
                if (close > i + 1 && src[i + 1] != c && src[close - 1] != c && (c == '*' || UnderscoreOk(src, i, close)))
                {
                    Flush(i);
                    Push(spans, c.ToString(), emphasis | NoteSpanStyle.Marker);
                    Inline(src[(i + 1)..close], spans, emphasis | NoteSpanStyle.Italic);
                    Push(spans, c.ToString(), emphasis | NoteSpanStyle.Marker);
                    i = close + 1;
                    plainStart = i;
                    continue;
                }
            }

            i++;
        }

        Flush(src.Length);
    }
}
