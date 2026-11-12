using System.Globalization;
using Themia.Sdk.Kit;

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
/// [S-11405] What a Clipboard widget shows, on the family's rows ([S-6711]): each entry one row, its
/// preview the name on one line, how much it is ("32 characters") on the second where the config gives
/// two; its marker and when it was copied at every width. Nothing but the entries: no count footer.
/// </summary>
/// <param name="Lines">The rows' lines: 1, or 2 with how much it is.</param>
/// <param name="Lead">The marker (what the entry looks like it is, [S-1442]) before the preview.</param>
/// <param name="Time">When it was copied, as the row's value (when the user shows it).</param>
/// <param name="Rows">[S-2440] The rows the list shows at a time, as its config states (more scroll inside them).</param>
public readonly record struct ClipboardPlan(int Lines, bool Lead, bool Time, int Rows)
{
    /// <summary>The rows' one height (28 or 40).</summary>
    public double RowHeight => FamilyShape.RowHeight(Lines);
}

/// <summary>[S-1442] [S-11405] The clipboard's plan and how a row is marked; pure, so both are pinned by tests.</summary>
public static class ClipboardLayout
{
    /// <summary>[S-1441] The characters a row's one-line preview keeps before its ellipsis.</summary>
    public const int PreviewLength = 120;

    /// <summary>[S-11405] The config's plan: its rows and lines, the marker and the time at every size.</summary>
    public static ClipboardPlan Plan(Themia.Core.Model.ClipboardConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new ClipboardPlan(config.RowsShown.Lines, Lead: true, Time: true, Math.Max(1, config.RowsShown.Rows));
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
