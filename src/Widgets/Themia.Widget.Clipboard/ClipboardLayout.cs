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

/// <summary>[S-1442] How a clipboard row is marked; pure, so it is pinned by tests.</summary>
public static class ClipboardLayout
{
    /// <summary>[S-1441] The characters a row's one-line preview keeps before its ellipsis.</summary>
    public const int PreviewLength = 120;

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
