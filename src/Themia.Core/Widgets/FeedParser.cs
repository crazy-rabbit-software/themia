using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Themia.Core.Widgets;

/// <summary>One article. Every field is text from a stranger: displayed, never interpreted.</summary>
/// <param name="Title">The headline. Shown as text — never as markup, never as a path.</param>
/// <param name="Link">The article address, already checked by <see cref="WebInput.Url"/>, or empty.</param>
/// <param name="Published">When it was published, if the feed said so in a form that parsed.</param>
/// <param name="Summary">A plain-text summary with any markup stripped out, or empty.</param>
public sealed record FeedEntry(string Title, string Link, DateTimeOffset? Published, string Summary);

/// <summary>A parsed feed: the channel's own title and its items, newest first.</summary>
public sealed record FeedDocument(string Title, IReadOnlyList<FeedEntry> Entries)
{
    public static FeedDocument Empty { get; } = new(string.Empty, []);
}

/// <summary>How a parse ended.</summary>
public enum FeedParseStatus
{
    /// <summary>Parsed, and there is at least one item.</summary>
    Ok = 0,

    /// <summary>Not well-formed XML, or it used a construct that is refused (a DTD).</summary>
    Malformed,

    /// <summary>Well-formed XML that is not a feed — most often an HTML page served in its place.</summary>
    NotFeed,

    /// <summary>A real feed with nothing in it. Different from a failure, and said differently.</summary>
    Empty,
}

/// <summary>The outcome of one parse.</summary>
public readonly record struct FeedParseResult(FeedParseStatus Status, FeedDocument Document);

/// <summary>
/// RSS 2.0 and Atom, parsed defensively.
/// <para>
/// A feed is the only document this app reads that is written entirely by somebody else, fetched
/// over the network, and re-read every few minutes for as long as the widget exists. It is the
/// textbook XXE target, so the reader is configured to refuse a DTD outright
/// (<see cref="DtdProcessing.Prohibit"/>) and is given no resolver, which means no external entity,
/// no parameter entity, no billion-laughs expansion and no <c>file:///</c> read dressed up as an
/// entity reference. A document carrying a DTD is <b>rejected</b>, not stripped: a feed has no
/// legitimate reason to carry one, and quietly accepting half of a hostile document is worse than
/// refusing it.
/// </para>
/// <para>
/// Everything past that is about not throwing. A missing element, a date in a format nobody has
/// seen, an item with no title: each of those is a field that ends up empty or null, never an
/// exception, because this runs on the way to a render.
/// </para>
/// </summary>
public static class FeedParser
{
    /// <summary>
    /// What to ask a host for. A server that sees <c>*/*</c> will often hand back the human HTML
    /// page instead of the feed, which then parses as "not a feed" and is reported as a broken feed.
    /// </summary>
    public const string Accept =
        "application/rss+xml, application/atom+xml, application/xml;q=0.9, text/xml;q=0.9, */*;q=0.8";

    /// <summary>
    /// A hard ceiling on the document, independent of the transport's own cap. A parser handed an
    /// unbounded document is a memory bug waiting for a hostile or broken host.
    /// </summary>
    private const long MaxCharacters = 8L * 1024 * 1024;

    /// <summary>
    /// How much of a summary is worth keeping: enough for the reading pane of a large tile ([S-1306]).
    /// Past this it is an article, not a summary.
    /// </summary>
    private const int MaxSummary = 600;

    /// <summary>How long a title is allowed to be before it is trimmed. Defensive, not cosmetic.</summary>
    private const int MaxTitle = 400;

    /// <summary>Parse a feed document and keep at most <paramref name="maxItems"/> entries.</summary>
    public static FeedParseResult Parse(string? xml, int maxItems)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return new FeedParseResult(FeedParseStatus.Malformed, FeedDocument.Empty);
        }

        XDocument document;

        try
        {
            var settings = new XmlReaderSettings
            {
                // The whole security posture of this file, in three lines. Prohibit rather than
                // Ignore: Ignore would skip the DTD and carry on parsing a document that was
                // trying something, and there is no reason to accept the rest of it.
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 0,

                MaxCharactersInDocument = MaxCharacters,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = true,
            };

            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            // Not well-formed, a DTD, or past the size cap. All of them mean the same thing to the
            // reader: what came back was not a usable feed.
            return new FeedParseResult(FeedParseStatus.Malformed, FeedDocument.Empty);
        }
        catch (InvalidOperationException)
        {
            return new FeedParseResult(FeedParseStatus.Malformed, FeedDocument.Empty);
        }

        var root = document.Root;

        if (root is null)
        {
            return new FeedParseResult(FeedParseStatus.Malformed, FeedDocument.Empty);
        }

        var cap = Math.Clamp(maxItems, 1, 200);

        // Namespaces are ignored throughout and elements are matched on local name. Feeds in the
        // wild put RSS elements in a dozen namespaces, declare none at all, or mix Atom links into
        // an RSS channel; matching the namespace as well would reject most of the internet.
        var channel = Descendant(root, "channel") ?? (Local(root) == "channel" ? root : null);

        if (channel is not null)
        {
            return Build(
                ChildText(channel, "title"),
                Children(channel, "item"),
                ReadRssItem,
                cap);
        }

        if (Local(root) == "feed")
        {
            return Build(
                ChildText(root, "title"),
                Children(root, "entry"),
                ReadAtomEntry,
                cap);
        }

        // Well-formed XML that is not a feed. Almost always an HTML page, a login wall or an error
        // document served with a 200, which is why this is its own status.
        return new FeedParseResult(FeedParseStatus.NotFeed, FeedDocument.Empty);
    }

    private static FeedParseResult Build(
        string title,
        IEnumerable<XElement> items,
        Func<XElement, FeedEntry?> read,
        int cap)
    {
        var entries = new List<FeedEntry>(Math.Min(cap, 32));

        foreach (var item in items)
        {
            if (entries.Count >= cap)
            {
                break;
            }

            var entry = read(item);

            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        var document = new FeedDocument(FeedText.Plain(title, MaxTitle), entries);

        return entries.Count == 0
            ? new FeedParseResult(FeedParseStatus.Empty, document)
            : new FeedParseResult(FeedParseStatus.Ok, document);
    }

    private static FeedEntry? ReadRssItem(XElement item)
    {
        var title = FeedText.Plain(ChildText(item, "title"), MaxTitle);
        var link = LinkOf(ChildText(item, "link"));
        var summary = FeedText.Plain(ChildText(item, "description"), MaxSummary);
        var published = FeedText.Date(ChildText(item, "pubDate"))
            ?? FeedText.Date(ChildText(item, "date"));

        // An item with no title, no link and no summary is not an article; it is padding, or the
        // remains of an element this parser does not understand. Dropped rather than drawn as a
        // blank row.
        if (title.Length == 0 && link.Length == 0 && summary.Length == 0)
        {
            return null;
        }

        return new FeedEntry(title, link, published, summary);
    }

    private static FeedEntry? ReadAtomEntry(XElement entry)
    {
        var title = FeedText.Plain(ChildText(entry, "title"), MaxTitle);
        var link = LinkOf(AtomLink(entry));
        var summary = FeedText.Plain(ChildText(entry, "summary"), MaxSummary);

        if (summary.Length == 0)
        {
            summary = FeedText.Plain(ChildText(entry, "content"), MaxSummary);
        }

        var published = FeedText.Date(ChildText(entry, "updated")) ?? FeedText.Date(ChildText(entry, "published"));

        if (title.Length == 0 && link.Length == 0 && summary.Length == 0)
        {
            return null;
        }

        return new FeedEntry(title, link, published, summary);
    }

    /// <summary>
    /// Atom's link is an attribute, and an entry usually has several: the article, a comments feed,
    /// an enclosure. The one worth opening is <c>rel="alternate"</c>, or the first with no rel at
    /// all, which the spec says means alternate.
    /// </summary>
    private static string AtomLink(XElement entry)
    {
        string? fallback = null;

        foreach (var link in Children(entry, "link"))
        {
            var href = Attribute(link, "href");

            if (href.Length == 0)
            {
                continue;
            }

            var rel = Attribute(link, "rel");

            if (rel.Length == 0 || rel.Equals("alternate", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }

            fallback ??= href;
        }

        return fallback ?? string.Empty;
    }

    /// <summary>
    /// A link is only kept when it is an http or https address.
    /// <para>
    /// This is the second half of "displayed, never executed". The link ends up at the shell, so a
    /// <c>javascript:</c>, a <c>file:///</c> or a UNC path in a feed somebody else controls is the
    /// whole attack; the same gate the settings box uses refuses all three, and an item with an
    /// unusable link simply does not open.
    /// </para>
    /// </summary>
    private static string LinkOf(string raw)
    {
        var checkedUrl = WebInput.Url(raw);
        return checkedUrl.IsValid ? checkedUrl.Url : string.Empty;
    }

    private static string Local(XElement element) => element.Name.LocalName;

    private static XElement? Descendant(XElement root, string name)
    {
        foreach (var element in root.Descendants())
        {
            if (Local(element).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        return null;
    }

    private static IEnumerable<XElement> Children(XElement parent, string name)
    {
        foreach (var element in parent.Elements())
        {
            if (Local(element).Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                yield return element;
            }
        }
    }

    private static string ChildText(XElement parent, string name)
    {
        foreach (var element in Children(parent, name))
        {
            var value = element.Value;

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string Attribute(XElement element, string name)
    {
        foreach (var attribute in element.Attributes())
        {
            if (attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute.Value.Trim();
            }
        }

        return string.Empty;
    }
}

/// <summary>
/// Turning what a feed puts in a title or a summary into something safe to put in a text block.
/// <para>
/// Feeds ship HTML inside XML text, usually escaped, sometimes in CDATA, occasionally both. The XML
/// parser unescapes one layer, which turns <c>&amp;lt;p&amp;gt;</c> back into a real tag — so by
/// the time a summary reaches here it can contain markup even though nothing about the XML was
/// malformed. None of it is rendered, so this is not about sanitising HTML for a browser; it is
/// about not showing the reader a paragraph of angle brackets.
/// </para>
/// </summary>
public static class FeedText
{
    /// <summary>
    /// A feed's markup as one line of plain text: tags removed, entities resolved once, runs of
    /// whitespace collapsed, and trimmed to <paramref name="max"/> characters.
    /// </summary>
    public static string Plain(string? markup, int max = 300)
    {
        if (string.IsNullOrWhiteSpace(markup))
        {
            return string.Empty;
        }

        var stripped = new StringBuilder(Math.Min(markup.Length, max * 4));
        var inTag = false;
        var lastWasSpace = true;

        foreach (var c in markup)
        {
            if (c == '<')
            {
                inTag = true;
                continue;
            }

            if (c == '>')
            {
                // A tag boundary is a word boundary: "<p>one</p><p>two</p>" must not read "onetwo".
                inTag = false;

                if (!lastWasSpace)
                {
                    stripped.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            if (inTag)
            {
                continue;
            }

            // Newlines and tabs are whitespace here; a preview is one line.
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                if (!lastWasSpace)
                {
                    stripped.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            stripped.Append(c);
            lastWasSpace = false;
        }

        // Once, not in a loop: repeatedly decoding is how "&amp;lt;script&amp;gt;" eventually
        // becomes a tag again, and a preview has no business resolving an escape twice.
        var text = Decode(stripped.ToString()).Trim();

        if (text.Length <= max)
        {
            return text;
        }

        // Cut on a word boundary where there is one nearby, so the preview does not end mid-word.
        var cut = text.LastIndexOf(' ', Math.Min(max, text.Length - 1));
        var end = cut > max / 2 ? cut : max;
        return string.Concat(text.AsSpan(0, end).TrimEnd(), "…");
    }

    /// <summary>
    /// The handful of entities a feed still ships after one XML decode. Anything else is left
    /// exactly as written rather than guessed at.
    /// </summary>
    private static string Decode(string text)
    {
        if (!text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }

        var output = new StringBuilder(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '&')
            {
                output.Append(text[i]);
                continue;
            }

            var end = text.IndexOf(';', i + 1);

            // An unterminated '&', or one so far from its ';' that it is not an entity.
            if (end < 0 || end - i > 10)
            {
                output.Append('&');
                continue;
            }

            var name = text[(i + 1)..end];
            var replacement = Entity(name);

            if (replacement is null)
            {
                output.Append('&');
                continue;
            }

            output.Append(replacement);
            i = end;
        }

        return output.ToString();
    }

    private static string? Entity(string name)
    {
        switch (name)
        {
            case "amp":
                return "&";
            case "lt":
                return "<";
            case "gt":
                return ">";
            case "quot":
                return "\"";
            case "apos":
            case "#39":
                return "'";
            case "nbsp":
                return " ";
            case "hellip":
                return "…";
            case "mdash":
                return "—";
            case "ndash":
                return "–";
            default:
                break;
        }

        if (name.Length < 2 || name[0] != '#')
        {
            return null;
        }

        var hex = name[1] is 'x' or 'X';
        var digits = hex ? name[2..] : name[1..];

        if (digits.Length == 0 || digits.Length > 6)
        {
            return null;
        }

        var style = hex ? NumberStyles.HexNumber : NumberStyles.None;

        if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out var code))
        {
            return null;
        }

        // Surrogates, control characters and anything past the last code point are not text.
        if (code is < 0x20 or > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF))
        {
            return null;
        }

        return char.ConvertFromUtf32(code);
    }

    /// <summary>
    /// A publication date in any of the shapes feeds actually use, or null.
    /// <para>
    /// RSS says RFC 822, Atom says RFC 3339, and real feeds ship both plus several near misses —
    /// a two-digit year, a missing weekday, a literal "GMT" where an offset belongs. Every format
    /// is tried explicitly first, because the app builds with <c>InvariantGlobalization</c> and
    /// there is no culture to fall back on. A date that will not parse is <b>null</b>, not "now":
    /// an undated item that claims to be current is worse than one that admits it has no date.
    /// </para>
    /// </summary>
    public static DateTimeOffset? Date(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return null;
        }

        // "Tue, 23 Sep 2026 10:00:00 GMT" is RFC 1123 with a zone name rather than an offset, and
        // that spelling is by far the most common thing in an RSS feed.
        if (text.EndsWith(" GMT", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith(" UTC", StringComparison.OrdinalIgnoreCase))
        {
            text = string.Concat(text.AsSpan(0, text.Length - 4), " +00:00");
        }

        text = WithColonInOffset(WithoutWeekday(text));

        foreach (var format in Formats)
        {
            if (DateTimeOffset.TryParseExact(
                    text,
                    format,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
                    out var exact))
            {
                return exact;
            }
        }

        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out var loose)
            ? loose
            : null;
    }

    /// <summary>
    /// Drop the leading weekday, because it is redundant and feeds get it wrong.
    /// <para>
    /// <c>ddd</c> is <i>validated</i> on an exact parse, so "Tue, 23 Sep 2026" — where the 23rd was
    /// a Wednesday — fails to parse entirely and the article silently loses its date. The day name
    /// carries no information the rest of the string does not already carry, so the safe reading is
    /// to ignore it rather than to let a publisher's off-by-one throw the timestamp away.
    /// </para>
    /// </summary>
    private static string WithoutWeekday(string text)
    {
        var comma = text.IndexOf(',', StringComparison.Ordinal);

        // Only a genuine leading weekday token: "Tue," or "Tuesday,". A comma further in belongs
        // to something else and the string is left alone.
        return comma is >= 3 and <= 9 ? text[(comma + 1)..].TrimStart() : text;
    }

    /// <summary>
    /// Turn RFC 822's <c>+0200</c> into the <c>+02:00</c> that .NET's <c>zzz</c> specifier reads.
    /// <para>
    /// There is no custom format specifier for a four-digit offset with no colon, which is the
    /// spelling RFC 822 mandates and the one nearly every RSS feed in existence uses. Without this
    /// step every dated item in a normal feed parsed as undated — and an undated item is not a
    /// visible failure, it just quietly sorts to the bottom with no timestamp.
    /// </para>
    /// </summary>
    private static string WithColonInOffset(string text)
    {
        if (text.Length < 5)
        {
            return text;
        }

        var sign = text[^5];

        if (sign is not ('+' or '-'))
        {
            return text;
        }

        for (var i = text.Length - 4; i < text.Length; i++)
        {
            if (!char.IsAsciiDigit(text[i]))
            {
                return text;
            }
        }

        return string.Concat(text.AsSpan(0, text.Length - 2), ":", text.AsSpan(text.Length - 2));
    }

    private static readonly string[] Formats =
    [
        "ddd, dd MMM yyyy HH:mm:ss zzz",
        "ddd, dd MMM yyyy HH:mm:ss zz",
        "ddd, dd MMM yyyy HH:mm:ss K",
        "ddd, d MMM yyyy HH:mm:ss zzz",
        "ddd, d MMM yyyy HH:mm:ss K",
        "ddd, dd MMM yyyy HH:mm zzz",
        "dd MMM yyyy HH:mm:ss zzz",
        "d MMM yyyy HH:mm:ss zzz",
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFzzz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd",
    ];
}
