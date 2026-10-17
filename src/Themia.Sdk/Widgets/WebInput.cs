using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>Why a web address was refused. <see cref="None"/> means it was accepted.</summary>
public enum WebUrlProblem
{
    None = 0,

    /// <summary>Nothing was typed. Not an error — the widget has simply not been pointed anywhere.</summary>
    Empty,

    /// <summary>Longer than any address a feed reader has business following.</summary>
    TooLong,

    /// <summary>Not a URL at all, or a relative one. Deliberately <b>not</b> repaired by prefixing a scheme.</summary>
    Malformed,

    /// <summary>A scheme other than http or https — <c>file:</c>, <c>ftp:</c>, <c>javascript:</c>.</summary>
    UnsupportedScheme,

    /// <summary>A UNC path (<c>\\server\share</c> or <c>//server/share</c>).</summary>
    UncPath,

    /// <summary>Credentials embedded in the authority. A feed URL is persisted in cleartext.</summary>
    Credentials,

    /// <summary>A scheme and no host.</summary>
    NoHost,
}

/// <summary>A checked web address: the accepted <see cref="Url"/>, or the reason it was refused.</summary>
/// <param name="Problem">Why it was refused; <see cref="WebUrlProblem.None"/> when it was not.</param>
/// <param name="Url">The address to request. Empty unless the check passed.</param>
public readonly record struct WebUrlResult(WebUrlProblem Problem, string Url)
{
    public bool IsValid => Problem == WebUrlProblem.None;
}

/// <summary>
/// Everything the four network widgets accept from outside themselves, checked before it reaches a
/// socket or a URL.
/// <para>
/// A feed address, a ticker symbol and a place name all arrive from two directions: a text box in
/// settings, and the JSON state file, which anybody can hand-edit. Both are trust boundaries and
/// both come through here, so a value that the box would reject cannot be smuggled in through the
/// file. The checks are pure functions with no network and no filesystem, which is why they live in
/// Core and are tested there.
/// </para>
/// <para>
/// A refused value is <b>refused</b>, never quietly repaired. "example.com/feed" is not turned into
/// "https://example.com/feed": guessing what somebody meant by an address is how a widget ends up
/// fetching something other than what is written in its settings, and the user cannot see the
/// difference.
/// </para>
/// </summary>
public static class WebInput
{
    /// <summary>Browsers stop honouring addresses well before this; a feed has no reason to reach it.</summary>
    public const int MaxUrlLength = 2048;

    /// <summary>Long enough for any real ticker or CoinGecko id, short enough to bound the path.</summary>
    public const int MaxSymbolLength = 24;

    /// <summary>Long enough for "Sault Ste. Marie, Ontario"; a query, not an essay.</summary>
    public const int MaxPlaceLength = 80;

    /// <summary>
    /// Check a URL the user typed, or one restored from the state file.
    /// <para>
    /// Surrounding whitespace is trimmed — that is not a repair, it is what a paste leaves behind —
    /// and nothing else about the string is changed.
    /// </para>
    /// </summary>
    public static WebUrlResult Url(string? raw)
    {
        var text = raw?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return new WebUrlResult(WebUrlProblem.Empty, string.Empty);
        }

        if (text.Length > MaxUrlLength)
        {
            return new WebUrlResult(WebUrlProblem.TooLong, string.Empty);
        }

        // Checked before Uri sees it: "\\\\server\\share" parses as a perfectly good file: URI, and
        // "//server/share" parses as a scheme-relative reference that Uri will happily complete
        // against nothing. Both are network paths to somewhere on the LAN, and neither is a feed.
        if (text.StartsWith(@"\\", StringComparison.Ordinal) || text.StartsWith("//", StringComparison.Ordinal))
        {
            return new WebUrlResult(WebUrlProblem.UncPath, string.Empty);
        }

        // A control character in a URL is either an injection attempt or a mangled paste; a raw
        // space means the string was never one URL.
        foreach (var c in text)
        {
            if (char.IsControl(c) || c == ' ')
            {
                return new WebUrlResult(WebUrlProblem.Malformed, string.Empty);
            }
        }

        if (!System.Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return new WebUrlResult(WebUrlProblem.Malformed, string.Empty);
        }

        if (uri.IsUnc || uri.IsFile)
        {
            return new WebUrlResult(WebUrlProblem.UncPath, string.Empty);
        }

        if (uri.Scheme != System.Uri.UriSchemeHttp && uri.Scheme != System.Uri.UriSchemeHttps)
        {
            return new WebUrlResult(WebUrlProblem.UnsupportedScheme, string.Empty);
        }

        if (string.IsNullOrEmpty(uri.Host))
        {
            return new WebUrlResult(WebUrlProblem.NoHost, string.Empty);
        }

        // "https://user:pass@host/feed" would put a password in the state file and in every log
        // line that names the URL. Refused rather than stripped, because stripping it silently
        // changes which resource is fetched.
        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return new WebUrlResult(WebUrlProblem.Credentials, string.Empty);
        }

        return new WebUrlResult(WebUrlProblem.None, uri.AbsoluteUri);
    }

    /// <summary>What to tell the user about a refused address. Empty when there is nothing wrong.</summary>
    public static string Explain(WebUrlProblem problem, Locale? locale = null)
    {
        if (problem == WebUrlProblem.None)
        {
            return string.Empty;
        }

        var id = problem switch
        {
            WebUrlProblem.Empty => Str.WidgetsC_WebUrl_Empty,
            WebUrlProblem.TooLong => Str.WidgetsC_WebUrl_TooLong,
            WebUrlProblem.Malformed => Str.WidgetsC_WebUrl_Malformed,
            WebUrlProblem.UnsupportedScheme => Str.WidgetsC_WebUrl_UnsupportedScheme,
            WebUrlProblem.UncPath => Str.WidgetsC_WebUrl_UncPath,
            WebUrlProblem.Credentials => Str.WidgetsC_WebUrl_Credentials,
            WebUrlProblem.NoHost => Str.WidgetsC_WebUrl_NoHost,
            _ => Str.WidgetsC_WebUrl_Refused,
        };

        return Strings.Get(locale ?? Strings.Current, id);
    }

    /// <summary>
    /// Whether a ticker symbol or CoinGecko id is safe to put in a URL path and worth asking about.
    /// <para>
    /// This is the check that matters most of the four, because the symbol is interpolated into the
    /// <i>path</i> of the quote endpoint rather than into a query value. Yahoo's own grammar uses
    /// only letters, digits and <c>. - ^ =</c>, and CoinGecko ids only lower-case letters, digits
    /// and hyphens, so anything outside that set — a slash, a dot-dot, a question mark, a percent
    /// escape — is not a symbol and is refused rather than escaped.
    /// </para>
    /// </summary>
    public static bool IsPlausibleSymbol(string? symbol)
    {
        var text = symbol?.Trim() ?? string.Empty;

        if (text.Length == 0 || text.Length > MaxSymbolLength)
        {
            return false;
        }

        var alphanumeric = false;

        foreach (var c in text)
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                alphanumeric = true;
                continue;
            }

            if (c is not ('.' or '-' or '^' or '=' or '_'))
            {
                return false;
            }
        }

        // "^^^" and "---" pass every rule above and name nothing.
        return alphanumeric;
    }

    /// <summary>The symbol as it is sent and shown: trimmed, and upper-cased for everything but a coin id.</summary>
    public static string NormaliseSymbol(string? symbol, bool crypto)
    {
        var text = symbol?.Trim() ?? string.Empty;

        // CoinGecko ids are lower-case by definition ("bitcoin", "ethereum"); Yahoo symbols are
        // upper-case by convention and its endpoint is case-sensitive about the suffixes.
        return crypto ? text.ToLowerInvariant() : text.ToUpperInvariant();
    }

    /// <summary>
    /// Whether a place name is worth sending to the geocoder. It goes into a query value and is
    /// escaped there, so this is about being a name rather than about being safe.
    /// </summary>
    public static bool IsPlausiblePlace(string? place)
    {
        var text = place?.Trim() ?? string.Empty;

        if (text.Length is 0 or > MaxPlaceLength)
        {
            return false;
        }

        var letter = false;

        foreach (var c in text)
        {
            if (char.IsControl(c))
            {
                return false;
            }

            if (char.IsLetter(c))
            {
                letter = true;
            }
        }

        return letter;
    }

    /// <summary>
    /// Whether a stored coordinate pair names a point on Earth.
    /// <para>
    /// Zero is a real place — the Gulf of Guinea — so "has a location" cannot be a truthiness test
    /// on the numbers, which is the bug the original app carries a spec bullet about. Absent is
    /// <c>null</c>; present is any finite pair inside the ranges.
    /// </para>
    /// </summary>
    public static bool AreCoordinates(double? latitude, double? longitude)
        => latitude is { } lat
            && longitude is { } lon
            && double.IsFinite(lat)
            && double.IsFinite(lon)
            && lat is >= -90 and <= 90
            && lon is >= -180 and <= 180;
}
