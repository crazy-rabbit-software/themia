using System.Globalization;
using System.Text.RegularExpressions;

namespace Themia.Core.Notes;

/// <summary>A note after an edit, and the selection to put back. Collapsed when Start == End.</summary>
public readonly record struct NoteEdit(string Text, int Start, int End);

/// <summary>The block markers the toolbar toggles. Headings cycle separately.</summary>
public enum NoteBlock
{
    Bullet,
    Ordered,
    Task,
    Quote,
}

/// <summary>What is in force at the caret, for lighting the toolbar's buttons.</summary>
/// <param name="Heading">0 for body text, else 1 to 3.</param>
public readonly record struct NoteMarks(bool Bold, bool Italic, bool Strike, bool Code, int Heading, NoteBlock? Block);

/// <summary>
/// [S-1353] Every keystroke and toolbar command the note editor intercepts, as a pure
/// <c>(text, selection) → (text, selection)</c>: Enter, Tab, the wrap shortcuts, the task box, the
/// block and heading commands. A port of the original <c>editing.ts</c>.
/// <para>
/// Keeping them out of the editor control is what makes them testable, and leaves the control one
/// job — apply the text and place the caret. Offsets are UTF-16 offsets into the note with
/// <c>\n</c> as one character, which is also how the editor counts.
/// </para>
/// </summary>
public static partial class NoteEditing
{
    [GeneratedRegex(@"^(\s*)([-*+] )\[[ xX]\]( ?)")]
    private static partial Regex TaskMarker();

    [GeneratedRegex(@"^(\s*)([-*+] )")]
    private static partial Regex BulletMarker();

    [GeneratedRegex(@"^(\s*)([0-9]{1,9})([.)] )")]
    private static partial Regex OrderedMarker();

    [GeneratedRegex(@"^(\s*[-*+] )\[([ xX])\]")]
    private static partial Regex TaskBox();

    [GeneratedRegex(@"^( {1,2}|\t)")]
    private static partial Regex OneIndent();

    [GeneratedRegex(@"^\s*")]
    private static partial Regex LeadingSpace();

    [GeneratedRegex(@"^\s*(#{1,3}) ")]
    private static partial Regex HeadingMarker();

    // Block markers, peeled in this order: a task marker starts with a bullet marker.
    [GeneratedRegex(@"^#{1,3} ")]
    private static partial Regex PeelHeading();

    [GeneratedRegex(@"^>\s?")]
    private static partial Regex PeelQuote();

    [GeneratedRegex(@"^[-*+] \[[ xX]\] ?")]
    private static partial Regex PeelTask();

    [GeneratedRegex(@"^[-*+] ")]
    private static partial Regex PeelBullet();

    [GeneratedRegex(@"^[0-9]{1,9}[.)] ")]
    private static partial Regex PeelOrdered();

    [GeneratedRegex(@"^[-*+] \[[ xX]\]")]
    private static partial Regex IsTask();

    // -----------------------------------------------------------------------------------------
    // Offsets
    // -----------------------------------------------------------------------------------------

    /// <summary>The bounds of the line holding <paramref name="offset"/>: start inclusive, end at its newline.</summary>
    public static (int Start, int End) LineBounds(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);

        var at = Math.Clamp(offset, 0, text.Length);
        var start = at == 0 ? 0 : text.LastIndexOf('\n', at - 1) + 1;
        var newline = text.IndexOf('\n', at);
        return (start, newline < 0 ? text.Length : newline);
    }

    /// <summary>The 0-based index of the line holding <paramref name="offset"/>.</summary>
    public static int LineIndexAt(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);

        var at = Math.Clamp(offset, 0, text.Length);
        var n = 0;
        for (var i = 0; i < at; i++)
        {
            if (text[i] == '\n')
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Where line <paramref name="index"/> starts; past the last line, the end of the text.</summary>
    public static int LineStart(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var at = 0;
        for (var n = 0; n < index; n++)
        {
            var newline = text.IndexOf('\n', at);
            if (newline < 0)
            {
                return text.Length;
            }

            at = newline + 1;
        }

        return at;
    }

    private static string Replace(string text, int start, int end, string insert) => string.Concat(text.AsSpan(0, start), insert, text.AsSpan(end));

    private static (int Start, int End) Order(string text, int start, int end)
    {
        var a = Math.Clamp(start, 0, text.Length);
        var b = Math.Clamp(end, 0, text.Length);
        return a <= b ? (a, b) : (b, a);
    }

    // -----------------------------------------------------------------------------------------
    // Keys
    // -----------------------------------------------------------------------------------------

    /// <summary>The list marker a new line inherits, or empty when the line is not a list item.</summary>
    private static string ContinuationPrefix(string line)
    {
        if (TaskMarker().Match(line) is { Success: true } task)
        {
            var gap = task.Groups[3].Length > 0 ? task.Groups[3].Value : " ";
            return $"{task.Groups[1].Value}{task.Groups[2].Value}[ ]{gap}";
        }

        if (OrderedMarker().Match(line) is { Success: true } ordered)
        {
            var next = long.Parse(ordered.Groups[2].Value, CultureInfo.InvariantCulture) + 1;
            return string.Create(CultureInfo.InvariantCulture, $"{ordered.Groups[1].Value}{next}{ordered.Groups[3].Value}");
        }

        return BulletMarker().Match(line) is { Success: true } bullet ? bullet.Groups[1].Value + bullet.Groups[2].Value : string.Empty;
    }

    /// <summary>The length of the leading list marker, or 0.</summary>
    private static int MarkerLength(string line)
    {
        if (TaskMarker().Match(line) is { Success: true } task)
        {
            return task.Length;
        }

        if (OrderedMarker().Match(line) is { Success: true } ordered)
        {
            return ordered.Length;
        }

        return BulletMarker().Match(line) is { Success: true } bullet ? bullet.Length : 0;
    }

    /// <summary>
    /// [S-1354] Enter: continue the list item (<c>- </c>, <c>- [ ] </c>, <c>1. </c> numbered on), or,
    /// on an item holding nothing but its marker, clear the marker — how every markdown editor lets
    /// you leave a list. The marker is inherited only when the caret is in the item's content.
    /// </summary>
    public static NoteEdit PressEnter(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        var (lineStart, lineEnd) = LineBounds(text, start);
        var line = text[lineStart..lineEnd];
        var mark = MarkerLength(line);

        if (mark > 0 && start == end && start >= lineStart + mark && line[mark..].Trim().Length == 0)
        {
            return new NoteEdit(Replace(text, lineStart, lineEnd, string.Empty), lineStart, lineStart);
        }

        var prefix = start >= lineStart + mark ? ContinuationPrefix(line) : string.Empty;
        var insert = "\n" + prefix;
        var caret = start + insert.Length;
        return new NoteEdit(Replace(text, start, end, insert), caret, caret);
    }

    /// <summary>[S-1355] Tab / Shift+Tab: indent or outdent every line the selection touches by two spaces.</summary>
    public static NoteEdit ShiftIndent(string text, int start, int end, bool outdent)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        return MapLines(text, start, end, (line, _) =>
        {
            if (!outdent)
            {
                return "  " + line;
            }

            var strip = OneIndent().Match(line);
            return strip.Success ? line[strip.Length..] : line;
        });
    }

    /// <summary>
    /// [S-1356] Ctrl+B / Ctrl+I (and the toolbar): wrap the selection in <paramref name="marker"/>,
    /// or unwrap it when it already is — inside the selection or just outside it, so pressing Ctrl+B
    /// again on the word just bolded takes it off rather than doubling it. With nothing selected,
    /// an empty pair with the caret between.
    /// </summary>
    public static NoteEdit ToggleWrap(string text, int start, int end, string marker)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(marker);
        (start, end) = Order(text, start, end);

        var n = marker.Length;

        if (start == end)
        {
            return new NoteEdit(Replace(text, start, start, marker + marker), start + n, start + n);
        }

        var inner = text[start..end];
        if (inner.Length > 2 * n && inner.StartsWith(marker, StringComparison.Ordinal) && inner.EndsWith(marker, StringComparison.Ordinal))
        {
            return new NoteEdit(Replace(text, start, end, inner[n..^n]), start, end - (2 * n));
        }

        if (start >= n && end + n <= text.Length
            && string.CompareOrdinal(text, start - n, marker, 0, n) == 0
            && string.CompareOrdinal(text, end, marker, 0, n) == 0)
        {
            return new NoteEdit(Replace(text, start - n, end + n, inner), start - n, end - n);
        }

        return new NoteEdit(Replace(text, start, end, marker + inner + marker), start + n, end + n);
    }

    /// <summary>[S-1357] Insert over the selection (paste), caret after it. Line endings become <c>\n</c>.</summary>
    public static NoteEdit InsertText(string text, int start, int end, string insert)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        var clean = NormalizeNewlines(insert ?? string.Empty);
        var caret = start + clean.Length;
        return new NoteEdit(Replace(text, start, end, clean), caret, caret);
    }

    /// <summary>[S-1357] CRLF and lone CR become LF: the note's one line ending.</summary>
    public static string NormalizeNewlines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains('\r', StringComparison.Ordinal) ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') : text;
    }

    /// <summary>
    /// [S-1358] Flip <c>[ ]</c> ↔ <c>[x]</c> on line <paramref name="index"/>; null when that line is
    /// not a task. Length-preserving, so the caret stays where it was.
    /// </summary>
    public static string? ToggleTaskLine(string text, int index)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lineStart = LineStart(text, index);
        var (_, lineEnd) = LineBounds(text, lineStart);
        var match = TaskBox().Match(text[lineStart..lineEnd]);
        if (!match.Success)
        {
            return null;
        }

        var at = lineStart + match.Groups[1].Length + 1;
        return string.Concat(text.AsSpan(0, at), text[at] == ' ' ? "x" : " ", text.AsSpan(at + 1));
    }

    /// <summary>
    /// [S-1358] The task box under <paramref name="offset"/>, as the line index to toggle: the caret a
    /// click leaves anywhere from just before <c>[</c> to just after <c>]</c>. Null elsewhere.
    /// </summary>
    public static int? TaskBoxAt(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);

        var (lineStart, lineEnd) = LineBounds(text, offset);
        var match = TaskBox().Match(text[lineStart..lineEnd]);
        if (!match.Success)
        {
            return null;
        }

        var boxStart = lineStart + match.Groups[1].Length;
        return offset >= boxStart && offset <= boxStart + 3 ? LineIndexAt(text, offset) : null;
    }

    // -----------------------------------------------------------------------------------------
    // Toolbar: block commands and mark detection
    // -----------------------------------------------------------------------------------------

    /// <summary>A line's indent and its content, with one block marker peeled off.</summary>
    private static (string Indent, string Body) SplitBlock(string line)
    {
        var indent = LeadingSpace().Match(line).Value;
        var rest = line[indent.Length..];

        foreach (var peel in new[] { PeelHeading(), PeelQuote(), PeelTask(), PeelBullet(), PeelOrdered() })
        {
            if (peel.Match(rest) is { Success: true } hit)
            {
                return (indent, rest[hit.Length..]);
            }
        }

        return (indent, rest);
    }

    private static NoteBlock? BlockOf(string line)
    {
        var rest = line[LeadingSpace().Match(line).Length..];

        if (PeelQuote().IsMatch(rest))
        {
            return NoteBlock.Quote;
        }

        if (IsTask().IsMatch(rest))
        {
            return NoteBlock.Task;
        }

        if (PeelBullet().IsMatch(rest))
        {
            return NoteBlock.Bullet;
        }

        return PeelOrdered().IsMatch(rest) ? NoteBlock.Ordered : null;
    }

    private static int HeadingOf(string line) => HeadingMarker().Match(line) is { Success: true } hit ? hit.Groups[1].Length : 0;

    /// <summary>
    /// Rewrite every line the selection touches, keeping the selection over the same words: its start
    /// moves by the first line's change in length, its end by the total.
    /// </summary>
    private static NoteEdit MapLines(string text, int start, int end, Func<string, int, string> rewrite)
    {
        var first = LineBounds(text, start).Start;
        var last = LineBounds(text, end).End;
        var lines = text[first..last].Split('\n');

        var deltaFirst = 0;
        var deltaTotal = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var next = rewrite(lines[i], i);
            var delta = next.Length - lines[i].Length;
            if (i == 0)
            {
                deltaFirst = delta;
            }

            deltaTotal += delta;
            lines[i] = next;
        }

        return new NoteEdit(
            Replace(text, first, last, string.Join('\n', lines)),
            Math.Max(first, start + deltaFirst),
            Math.Max(first, end + deltaTotal));
    }

    /// <summary>
    /// [S-1359] Apply a block marker to every line the selection touches, or take it off when every
    /// one of them already has it. Any other block marker (or heading) is replaced, so the buttons are
    /// one exclusive group rather than markers that stack.
    /// </summary>
    public static NoteEdit ToggleBlock(string text, int start, int end, NoteBlock kind)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        var first = LineBounds(text, start).Start;
        var last = LineBounds(text, end).End;
        var alreadySet = text[first..last].Split('\n').All(line => BlockOf(line) == kind);

        var ordinal = 0;
        return MapLines(text, start, end, (line, _) =>
        {
            var (indent, rest) = SplitBlock(line);
            if (alreadySet)
            {
                return indent + rest;
            }

            ordinal++;
            return kind switch
            {
                NoteBlock.Bullet => $"{indent}- {rest}",
                NoteBlock.Ordered => string.Create(CultureInfo.InvariantCulture, $"{indent}{ordinal}. {rest}"),
                NoteBlock.Task => $"{indent}- [ ] {rest}",
                _ => $"{indent}> {rest}",
            };
        });
    }

    /// <summary>
    /// [S-1359] Step the touched lines H1 → H2 → H3 → body → H1. One button for three levels; the
    /// level comes from the first touched line, so a selection moves as a block.
    /// </summary>
    public static NoteEdit CycleHeading(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        var (lineStart, lineEnd) = LineBounds(text, start);
        var next = (HeadingOf(text[lineStart..lineEnd]) + 1) % 4;

        return MapLines(text, start, end, (line, _) =>
        {
            var (indent, rest) = SplitBlock(line);
            return next == 0 ? indent + rest : $"{indent}{new string('#', next)} {rest}";
        });
    }

    /// <summary>The content (non-marker) span covering a line offset.</summary>
    private static NoteSpan? ContentSpanAt(IReadOnlyList<NoteSpan> spans, int offset)
    {
        var at = 0;
        foreach (var span in spans)
        {
            var end = at + span.Text.Length;
            if (!span.Has(NoteSpanStyle.Marker) && offset >= at && offset < end)
            {
                return span;
            }

            at = end;
        }

        return null;
    }

    /// <summary>
    /// [S-1359] What formatting applies at the selection. For a caret this is the character before it
    /// (what was just typed), falling back to the one after, so sitting just inside <c>**|bold**</c>
    /// reads as bold. A checked task's strikethrough is the line's state, not a mark, so it does not
    /// light the strikethrough button.
    /// </summary>
    public static NoteMarks ActiveMarks(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);
        (start, end) = Order(text, start, end);

        var lines = NoteMarkdown.Parse(text);
        var index = LineIndexAt(text, start);
        var line = index < lines.Count ? lines[index] : null;
        var (lineStart, _) = LineBounds(text, start);
        var probe = start == end ? start - lineStart - 1 : start - lineStart;
        var span = line is null ? null : ContentSpanAt(line.Spans, probe) ?? ContentSpanAt(line.Spans, probe + 1);
        var style = span?.Style ?? NoteSpanStyle.None;
        var kind = line?.Kind ?? NoteLineKind.Paragraph;

        return new NoteMarks(
            Bold: (style & NoteSpanStyle.Bold) != 0,
            Italic: (style & NoteSpanStyle.Italic) != 0,
            Strike: (style & NoteSpanStyle.Strike) != 0 && kind != NoteLineKind.Task,
            Code: (style & NoteSpanStyle.Code) != 0,
            Heading: kind switch
            {
                NoteLineKind.Heading1 => 1,
                NoteLineKind.Heading2 => 2,
                NoteLineKind.Heading3 => 3,
                _ => 0,
            },
            Block: kind switch
            {
                NoteLineKind.Bullet => NoteBlock.Bullet,
                NoteLineKind.Ordered => NoteBlock.Ordered,
                NoteLineKind.Task => NoteBlock.Task,
                NoteLineKind.Quote => NoteBlock.Quote,
                _ => null,
            });
    }
}
