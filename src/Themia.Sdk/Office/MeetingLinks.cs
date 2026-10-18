using System.Text.RegularExpressions;

namespace Themia.Core.Office;

/// <summary>[S-4822] The online-meeting services a join link is recognised as.</summary>
public enum MeetingService
{
    /// <summary>A link the provider itself named as the meeting's (Graph's <c>onlineMeeting</c>), on a host not listed here.</summary>
    Other = 0,
    Teams = 1,
    Zoom = 2,
    Meet = 3,
    Webex = 4,
}

/// <summary>
/// [S-4822] An event's online-meeting link: where it comes from, and the one gate it passes before it
/// is ever handed to the shell.
/// <para>
/// The provider's own field wins (Graph <c>onlineMeeting.joinUrl</c>, Google <c>hangoutLink</c> or a
/// <c>conferenceData</c> video entry point, an iCalendar <c>X-MICROSOFT-SKYPETEAMSMEETINGURL</c> /
/// <c>X-GOOGLE-CONFERENCE</c>); failing that, the first link to a known meeting service found in
/// the location, then the description. A link in free text only counts when it is a meeting
/// service's (a Teams <c>meetup-join</c>, a Zoom <c>/j/</c>, a Meet room, a Webex meeting), so an
/// agenda's "see https://wiki/…" is never offered as Join. Outlook's Safe Links wrapping is unwrapped
/// to the link it carries before it is judged.
/// </para>
/// <para>
/// Only <c>https</c>, <c>http</c>, <c>msteams:</c> and <c>zoommtg:</c> pass <see cref="IsLaunchable"/>,
/// with no user info, no control characters, no whitespace and at most <see cref="MaxLength"/>
/// characters; the launcher re-applies it to the literal string.
/// </para>
/// </summary>
public static partial class MeetingLinks
{
    /// <summary>The longest link kept or launched.</summary>
    public const int MaxLength = 2048;

    private static readonly string[] Schemes = ["https", "http", "msteams", "zoommtg"];

    /// <summary>[S-4822] The join link for an event: the first usable explicit one, else one found in the texts.</summary>
    /// <param name="explicitLinks">Links the provider named as the meeting's, best first.</param>
    /// <param name="texts">Free text to search, in order (location, then the body).</param>
    public static string? Find(IEnumerable<string?> explicitLinks, params string?[] texts)
    {
        ArgumentNullException.ThrowIfNull(explicitLinks);

        foreach (var link in explicitLinks)
        {
            if (Clean(link) is { } clean && IsLaunchable(clean))
            {
                return clean;
            }
        }

        foreach (var text in texts)
        {
            if (FromText(text) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>[S-4822] The first link to a known meeting service in <paramref name="text"/>, or null.</summary>
    public static string? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (Match match in LinkPattern().Matches(text))
        {
            if (Clean(match.Value) is { } clean && IsLaunchable(clean) && Recognise(clean) != MeetingService.Other)
            {
                return clean;
            }
        }

        return null;
    }

    /// <summary>[S-4822] Which service a link is, by its scheme, host and path; Other when none.</summary>
    public static MeetingService Recognise(string? link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return MeetingService.Other;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme == "msteams")
        {
            return MeetingService.Teams;
        }

        if (scheme == "zoommtg")
        {
            return MeetingService.Zoom;
        }

        var host = uri.Host.ToLowerInvariant();
        var path = uri.AbsolutePath.ToLowerInvariant();

        if ((Is(host, "teams.microsoft.com") && path.StartsWith("/l/meetup-join", StringComparison.Ordinal))
            || (Is(host, "teams.live.com") && path.StartsWith("/meet", StringComparison.Ordinal)))
        {
            return MeetingService.Teams;
        }

        if ((Is(host, "zoom.us") || Is(host, "zoomgov.com"))
            && (path.StartsWith("/j/", StringComparison.Ordinal) || path.StartsWith("/w/", StringComparison.Ordinal) || path.StartsWith("/my/", StringComparison.Ordinal)))
        {
            return MeetingService.Zoom;
        }

        if (host == "meet.google.com" && path.Length > 1)
        {
            return MeetingService.Meet;
        }

        if (Is(host, "webex.com")
            && (path.Contains("/meet", StringComparison.Ordinal) || path.Contains("/join", StringComparison.Ordinal)
                || path.Contains("j.php", StringComparison.Ordinal) || path.Contains("/wbxmjs/", StringComparison.Ordinal)))
        {
            return MeetingService.Webex;
        }

        return MeetingService.Other;
    }

    /// <summary>[S-4822] Whether a link may be handed to the shell, checked on the literal string.</summary>
    public static bool IsLaunchable(string? link)
    {
        if (string.IsNullOrEmpty(link) || link.Length > MaxLength || link.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return false;
        }

        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (!Schemes.Contains(scheme))
        {
            return false;
        }

        // A web link needs a host; the app schemes carry their meeting in the path or query.
        return scheme is "msteams" or "zoommtg" || uri.Host.Length > 0;
    }

    /// <summary>A link as found: trimmed of the punctuation prose puts after it, HTML's <c>&amp;amp;</c> undone, Safe Links unwrapped.</summary>
    private static string? Clean(string? link)
    {
        var text = link?.Trim().TrimEnd('.', ',', ';', ':', ')', ']', '>', '"', '\'', '!', '?');
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        text = text.Replace("&amp;", "&", StringComparison.OrdinalIgnoreCase);

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.EndsWith(".safelinks.protection.outlook.com", StringComparison.OrdinalIgnoreCase)
            && Query(uri.Query, "url") is { } inner)
        {
            return inner;
        }

        return text;
    }

    private static string? Query(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq > 0 && string.Equals(pair[..eq], name, StringComparison.OrdinalIgnoreCase))
            {
                var value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
                return value.Length > 0 ? value : null;
            }
        }

        return null;
    }

    private static bool Is(string host, string domain)
        => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);

    [GeneratedRegex(@"(?:https?://|msteams:/|zoommtg://)[^\s<>""'\]\[]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LinkPattern();
}
