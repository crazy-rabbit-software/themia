using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Sdk;

namespace Themia.Core.Office;

/// <summary>
/// [S-1268] What opening a calendar event hands to the shell.
/// <para>
/// A Microsoft event opens in Outlook for Windows (<c>ms-outlook://events/{id}</c>) when Outlook's
/// protocol is registered, as the classic app's <c>open_calendar_event</c> did; otherwise, and for
/// Google, the provider's own https page. The classic app guessed "Microsoft" from the link's
/// domain; here the event's account says which provider it came from, so a Google event whose page
/// happens to mention office.com can never be sent to Outlook. CalDAV has no page: nothing to open.
/// </para>
/// <para>
/// Only two shapes ever leave this class — an https URL with no user info, and an
/// <c>ms-outlook://events/…</c> link with a percent-encoded id — and <see cref="IsLaunchable"/> is
/// the gate the launcher re-applies to the literal string.
/// </para>
/// </summary>
public static class CalendarLinks
{
    private const string EventsPrefix = MailLinks.OutlookScheme + "://events/";

    /// <summary>The link that opens an event, or null when there is nothing to open.</summary>
    public static string? Open(CalendarEvent item, ProviderId? provider, bool outlookInstalled)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (provider == HostProviders.Microsoft && outlookInstalled && !string.IsNullOrWhiteSpace(item.Id))
        {
            return EventsPrefix + MailLinks.PercentEncode(item.Id.Trim());
        }

        return IsHttps(item.WebLink) ? item.WebLink!.Trim() : null;
    }

    /// <summary>Whether a link is one of the two shapes this class makes, checked on the literal string.</summary>
    public static bool IsLaunchable(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Any(char.IsControl))
        {
            return false;
        }

        if (link.StartsWith(EventsPrefix, StringComparison.Ordinal))
        {
            var rest = link[EventsPrefix.Length..];
            return rest.Length > 0 && rest.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '%');
        }

        return IsHttps(link);
    }

    private static bool IsHttps(string? link)
        => Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0
            && string.IsNullOrEmpty(uri.UserInfo);
}
