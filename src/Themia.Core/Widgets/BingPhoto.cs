using System.Text.Json;
using System.Text.Json.Serialization;

namespace Themia.Core.Widgets;

/// <summary>[S-762] Bing's <c>HPImageArchive</c> reply, as far as the gallery reads it.</summary>
public sealed class BingArchiveWire
{
    [JsonPropertyName("images")]
    public List<BingImageWire>? Images { get; set; }
}

public sealed class BingImageWire
{
    /// <summary>A path on www.bing.com, e.g. <c>/th?id=OHR.Example_EN-US123_1920x1080.jpg&amp;pid=hp</c>.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>[S-3112] Bing's credit line, e.g. <c>Lake Bled, Slovenia (© Photographer/Agency)</c>.</summary>
    [JsonPropertyName("copyright")]
    public string? Copyright { get; set; }
}

/// <summary>
/// [S-3112] The photo of the day's copyright credit, shown only as the picture's hover tooltip.
/// Bing's pictures are copyrighted and Bing credits each one, so the widget does too; it is the one
/// exception to "no words on a picture" ([S-2760]) and it is never drawn on the tile.
/// </summary>
public sealed record BingCredit(string Text)
{
    /// <summary>Longer than any real credit; a longer field is cut, not trusted.</summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The trust boundary for the credit: control characters (line breaks included) become spaces,
    /// runs of white space collapse and the ends are trimmed. Bing's field is usually
    /// <c>Place, Country (© Photographer/Agency)</c>; only the <c>© …</c> part is kept, so the
    /// tooltip is the credit and not the picture's title. A field with no © is kept whole. The
    /// result is capped at <see cref="MaxLength"/>. Null when nothing is left, so a missing or blank
    /// field is no tooltip rather than an empty one.
    /// </summary>
    public static BingCredit? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = Clean(raw);
        var mark = text.LastIndexOf('©');

        if (mark >= 0)
        {
            // "(© Someone)": from the mark to its closing bracket, which a credit at the very end
            // of the field may lack.
            var close = text.IndexOf(')', mark);
            text = (close < 0 ? text[mark..] : text[mark..close]).Trim();
        }

        if (text.Length > MaxLength)
        {
            // A cap that lands between a surrogate pair would leave half a character.
            var cut = char.IsHighSurrogate(text[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
            text = text[..cut].TrimEnd();
        }

        // A lone "©" names nobody: no credit, so no tooltip.
        return text.Length == 0 || text == "©" ? null : new BingCredit(text);
    }

    /// <summary>
    /// Control characters and white space runs become one space, format characters go, the ends
    /// are trimmed.
    /// </summary>
    private static string Clean(string raw)
    {
        var text = new System.Text.StringBuilder(raw.Length);
        var space = false;

        foreach (var c in raw)
        {
            if (char.IsControl(c) || char.IsWhiteSpace(c))
            {
                space = text.Length > 0;
                continue;
            }

            // Invisible format characters (bidi overrides, zero-width marks) could make the
            // credit read as something else; they are dropped, not spaced.
            if (char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format)
            {
                continue;
            }

            if (space)
            {
                text.Append(' ');
                space = false;
            }

            text.Append(c);
        }

        return text.ToString();
    }
}

/// <summary>Source-generated contract for the photo of the day. Native AOT has no reflection serializer.</summary>
[JsonSerializable(typeof(BingArchiveWire))]
public sealed partial class BingWireContext : JsonSerializerContext;

/// <summary>
/// [S-762] Today's photo: where the picture is, and [S-3112] its credit for the hover tooltip (null
/// when Bing sent none). Its title is not read: the widget shows no words on a picture ([S-2760]).
/// </summary>
public sealed record BingPhoto(Uri Image, BingCredit? Credit)
{
    /// <summary>
    /// The archive endpoint the original widget used: today's photo (<c>idx=0</c>), one of them,
    /// the US market's metadata.
    /// </summary>
    public static Uri Endpoint { get; } = new("https://www.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=en-US");

    private const string Host = "www.bing.com";

    /// <summary>Parse a reply body. Null for anything that is not a usable photo.</summary>
    public static BingPhoto? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return From(JsonSerializer.Deserialize(json, BingWireContext.Default.BingArchiveWire));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The trust boundary: the reply names a path, and only a path on Bing's own host is accepted.
    /// A protocol-relative <c>//elsewhere</c>, an absolute URL or anything that resolves off
    /// www.bing.com is refused rather than handed to the image decoder, which would fetch it.
    /// </summary>
    public static BingPhoto? From(BingArchiveWire? wire)
    {
        var first = wire?.Images is { Count: > 0 } images ? images[0] : null;
        var path = first?.Url?.Trim();

        if (string.IsNullOrEmpty(path) || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\'))
        {
            return null;
        }

        if (!Uri.TryCreate("https://" + Host + path, UriKind.Absolute, out var image)
            || image.Scheme != Uri.UriSchemeHttps
            || !string.Equals(image.Host, Host, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new BingPhoto(image, BingCredit.Parse(first?.Copyright));
    }
}
