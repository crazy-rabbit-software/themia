using System.Globalization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1442] What a clipboard entry looks like it is, so its row can say so with a glyph (or, for a
/// colour, a swatch). A closed set; anything that is not clearly one of the others is
/// <see cref="Text"/>.
/// </summary>
public enum ClipboardContent
{
    Text,
    Lines,
    Link,
    Email,
    Number,
    Colour,
}

/// <summary>
/// [S-1443] What one Clipboard tile shows, from its area: how many lines a row's preview may take,
/// whether the row carries a detail line (when · how much) or a time column. Nothing but the
/// entries: no count footer at any size.
/// </summary>
/// <param name="PreviewLines">1 to 3: wrapped lines of the preview per row.</param>
/// <param name="Detail">A caption under each preview: "4m · 32 characters".</param>
/// <param name="TimeColumn">The relative time in a right-hand column (only when there is no detail line).</param>
/// <param name="Compact">Rows at the compact 32px height (one-line rows on a short tile).</param>
public readonly record struct ClipboardPlan(int PreviewLines, bool Detail, bool TimeColumn, bool Compact);

/// <summary>
/// How a clipboard entry reads at different tile sizes. Pure, so the ladder and the content rules
/// are pinned by tests rather than by looking at screenshots.
/// </summary>
public static class ClipboardLayout
{
    /// <summary>The most lines a row's preview ever takes.</summary>
    public const int MaxPreviewLines = 3;

    /// <summary>
    /// [S-1443] The ladder. One line on the small tiles; a second line once the row is wide (a Wide
    /// strip) or the tile is tall; a third on a large tile. The detail line arrives with height
    /// (Tall, L, XL) and replaces the time column, which only earns its width on M and Wide.
    /// </summary>
    public static ClipboardPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured)
        {
            return new ClipboardPlan(1, Detail: false, TimeColumn: false, Compact: false);
        }

        var lines = 1;

        if (area.WidthClass >= SizeClass.Expanded || (area.WidthClass >= SizeClass.Regular && area.HeightClass >= SizeClass.Expanded))
        {
            lines++;
        }

        if (area.AtLeast(SizeClass.Expanded, SizeClass.Expanded))
        {
            lines++;
        }

        var detail = area.HeightClass >= SizeClass.Expanded;
        var column = !detail && (area.WidthClass >= SizeClass.Expanded || area.AtLeast(SizeClass.Regular, SizeClass.Regular));
        var compact = lines == 1 && area.HeightClass == SizeClass.Compact;

        return new ClipboardPlan(Math.Min(lines, MaxPreviewLines), detail, column, compact);
    }

    /// <summary>
    /// [S-1441] A preview of up to <paramref name="maxLines"/> lines: the entry's own line breaks
    /// are kept (blank lines dropped), whitespace inside a line is collapsed, each line is cut at
    /// <paramref name="maxCharsPerLine"/>, and a line is marked with an ellipsis when more of the
    /// entry follows than is shown. One line is exactly <see cref="ClipboardFormatting.Preview"/>.
    /// </summary>
    public static string PreviewLines(string? text, int maxLines, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0 || maxCharsPerLine <= 0)
        {
            return string.Empty;
        }

        if (maxLines == 1)
        {
            return ClipboardFormatting.Preview(text, maxCharsPerLine);
        }

        var shown = new List<string>(maxLines);
        var more = false;

        foreach (var raw in text.Split('\n'))
        {
            var line = ClipboardFormatting.Preview(raw, maxCharsPerLine);

            if (line.Length == 0)
            {
                continue;
            }

            if (shown.Count == maxLines)
            {
                more = true;
                break;
            }

            shown.Add(line);
        }

        if (more && shown.Count > 0 && !shown[^1].EndsWith('…'))
        {
            shown[^1] += " …";
        }

        return string.Join('\n', shown);
    }

    /// <summary>
    /// [S-1442] What an entry looks like it is. Conservative: a hex colour needs its <c>#</c> (a bare
    /// "123" is a number, not a colour), a link needs a web or mail scheme, and anything spanning
    /// several lines is <see cref="ClipboardContent.Lines"/> whatever it contains.
    /// </summary>
    public static ClipboardContent Classify(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ClipboardContent.Text;
        }

        var trimmed = text.Trim();

        if (trimmed.Contains('\n', StringComparison.Ordinal))
        {
            return ClipboardContent.Lines;
        }

        if (trimmed.Contains(' ', StringComparison.Ordinal))
        {
            // A phrase. rgb(1, 2, 3) is the one spaced form worth recognising.
            return IsColour(trimmed) ? ClipboardContent.Colour : ClipboardContent.Text;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFtp))
        {
            return ClipboardContent.Link;
        }

        if (IsEmail(trimmed))
        {
            return ClipboardContent.Email;
        }

        if (IsColour(trimmed))
        {
            return ClipboardContent.Colour;
        }

        // A leading digit, sign or point: "NaN" and "Infinity" parse as doubles but are words.
        if ((char.IsAsciiDigit(trimmed[0]) || trimmed[0] is '-' or '+' or '.')
            && double.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
        {
            return ClipboardContent.Number;
        }

        return ClipboardContent.Text;
    }

    /// <summary>The colour an entry names, for its swatch; false when it is not one.</summary>
    public static bool TryColour(string? text, out RgbColor colour)
    {
        colour = default;
        return text is not null && IsColour(text.Trim()) && ColorFormats.TryParse(text, out colour);
    }

    private static bool IsColour(string trimmed)
    {
        var looksLikeOne = trimmed.StartsWith('#')
            || trimmed.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("hsl", StringComparison.OrdinalIgnoreCase);

        return looksLikeOne && ColorFormats.TryParse(trimmed, out _);
    }

    private static bool IsEmail(string trimmed)
    {
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at == trimmed.LastIndexOf('@')
            && trimmed.IndexOf('.', at) > at + 1
            && !trimmed.EndsWith('.');
    }
}
