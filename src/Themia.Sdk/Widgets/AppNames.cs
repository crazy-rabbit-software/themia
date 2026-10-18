namespace Themia.Core.Widgets;

/// <summary>
/// What to call a running program, from what its executable says about itself.
/// <para>
/// Shared by the mixer and screen time, which both put an app's name in front of the user and both
/// learned the same lessons the hard way: a version resource's string can carry bytes past its end,
/// some apps ship an internal entry-point identifier as their description ("WhatsApp.Root"), and a
/// session's own display name is often an unexpanded resource reference nobody should read.
/// </para>
/// </summary>
public static class AppNames
{
    /// <summary>Trailing segments that mark an identifier rather than a product name.</summary>
    private static readonly string[] JunkTails = ["root", "app", "exe", "main", "entrypoint", "program"];

    /// <summary>
    /// [S-458] A display name as one clean line: cut at the first control character, trimmed, and with
    /// an entry-point tail removed. Null when nothing is left, so the caller falls back.
    /// <para>
    /// The cut is the fix for names like <c>Spotify\0…FileV</c>: trusting the length the version API
    /// returns leaked the bytes after the string into the name.
    /// </para>
    /// </summary>
    public static string? Sanitize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var end = 0;
        while (end < raw.Length && raw[end] >= ' ')
        {
            end++;
        }

        var trimmed = raw[..end].Trim();
        return trimmed.Length == 0 ? null : StripEntrypointSuffix(trimmed);
    }

    /// <summary>
    /// [S-458] Drop a meaningless dotted tail from an identifier-shaped name — no whitespace, and a
    /// tail from a short known list — so <c>WhatsApp.Root</c> reads as <c>WhatsApp</c>. Real dotted
    /// names keep their dots: "Node.js JavaScript Runtime" has spaces and "Microsoft.Todos" has a
    /// meaningful tail.
    /// </summary>
    public static string StripEntrypointSuffix(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (name.Any(char.IsWhiteSpace))
        {
            return name;
        }

        var dot = name.LastIndexOf('.');
        if (dot <= 0)
        {
            return name;
        }

        var tail = name[(dot + 1)..];
        foreach (var junk in JunkTails)
        {
            if (string.Equals(tail, junk, StringComparison.OrdinalIgnoreCase))
            {
                return name[..dot];
            }
        }

        return name;
    }

    /// <summary>[S-458] Dotted with no whitespace: an internal name, so the product name is preferred.</summary>
    public static bool IsIdentifierLike(string name)
        => !string.IsNullOrEmpty(name) && !name.Any(char.IsWhiteSpace) && name.Contains('.', StringComparison.Ordinal);

    /// <summary>
    /// [S-458] The name to show for an executable, from its version strings: the description, unless
    /// it is still identifier-shaped after cleaning, in which case the product name if that is any
    /// better. Null when neither is usable.
    /// </summary>
    public static string? FromVersionInfo(string? description, string? productName)
    {
        var desc = Sanitize(description);
        if (desc is not null && !IsIdentifierLike(desc))
        {
            return desc;
        }

        var product = Sanitize(productName);
        return product is not null && !IsIdentifierLike(product) ? product : desc;
    }

    /// <summary>The file name without its extension: <c>C:\…\chrome.exe</c> → <c>chrome</c>. Null when empty.</summary>
    public static string? ExeStem(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var slash = path.LastIndexOfAny(['\\', '/']);
        var file = slash < 0 ? path : path[(slash + 1)..];
        var dot = file.LastIndexOf('.');
        var stem = dot > 0 ? file[..dot] : file;

        return stem.Length == 0 ? null : stem;
    }

    /// <summary>[S-458] The fallback name for a bucket with nothing better: the key, capitalised.</summary>
    public static string Prettify(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "Unknown";
        }

        return char.ToUpperInvariant(key[0]) + key[1..];
    }
}
