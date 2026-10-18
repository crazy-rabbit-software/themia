using System.Globalization;
using System.Text;
using Themia.Core.Accounts;

namespace Themia.Core.Mail;

/// <summary>
/// [S-547] What opening or replying to a message hands to the shell.
/// <para>
/// A Microsoft message deep-links into Outlook (<c>ms-outlook://emails/{id}</c>) when Outlook is
/// installed — it only understands Graph ids, so no other provider's id is ever sent there — and
/// otherwise opens Graph's own web link. A Gmail message opens its thread on the web. An IMAP
/// message has no web view: open is a logged no-op, never a shell call with an empty string (which
/// launches Explorer — the classic app's issue #45a). Reply falls back to <c>mailto:</c>.
/// </para>
/// <para>
/// Only three shapes ever leave this class — an https URL from the provider, an
/// <c>ms-outlook://emails/…</c> link with a percent-encoded id, and a <c>mailto:</c> with a checked
/// address (or none, to compose, [S-558]) — and <see cref="IsLaunchable"/> is the gate the launcher
/// applies to them.
/// </para>
/// </summary>
public static class MailLinks
{
    public const string OutlookScheme = "ms-outlook";

    /// <summary>The link that opens a message, or null when there is nothing to open.</summary>
    public static string? Open(MailMessage message, bool outlookInstalled)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Provider == HostProviders.Microsoft && outlookInstalled)
        {
            return $"{OutlookScheme}://emails/{MailLinks.PercentEncode(message.RawId)}";
        }

        return IsHttps(message.WebLink) ? message.WebLink.Trim() : null;
    }

    /// <summary>The link that starts a reply, or null when the sender has no usable address.</summary>
    public static string? Reply(MailMessage message, bool outlookInstalled)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (message.Provider == HostProviders.Microsoft && outlookInstalled)
        {
            return $"{OutlookScheme}://emails/{MailLinks.PercentEncode(message.RawId)}/reply";
        }

        if (!IsAddress(message.FromAddress))
        {
            return null;
        }

        var subject = message.Subject.StartsWith("re:", StringComparison.OrdinalIgnoreCase)
            ? message.Subject
            : "Re: " + message.Subject;

        return $"mailto:{message.FromAddress}?subject={MailLinks.PercentEncode(subject)}";
    }

    /// <summary>A bare <c>mailto:</c>: a new message in the default mail app, to nobody yet.</summary>
    public const string BlankMailto = "mailto:";

    /// <summary>
    /// [S-558] The link that starts a new message from <paramref name="account"/>. Microsoft opens
    /// Outlook on the web's compose page, on outlook.live.com for a personal account and
    /// outlook.office.com for a work or school one; Gmail its compose window for that address
    /// (<c>authuser</c>, as the message links do, [S-545]); IMAP a bare <c>mailto:</c>, so the default
    /// mail app opens with no recipient. Never null: every account can start a message.
    /// </summary>
    /// <param name="known">Messages already held for the account: Graph's web links say which Outlook it lives on.</param>
    public static string Compose(MailAccount account, IEnumerable<MailMessage> known)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(known);

        if (account.Provider == HostProviders.Microsoft)
        {
            return IsPersonalMicrosoft(account, known)
                ? "https://outlook.live.com/mail/0/deeplink/compose"
                : "https://outlook.office.com/mail/deeplink/compose";
        }

        if (account.Provider == HostProviders.Google)
        {
            return IsAddress(account.Address)
                ? $"https://mail.google.com/mail/?authuser={MailLinks.PercentEncode(account.Address)}&view=cm&fs=1"
                : "https://mail.google.com/mail/?view=cm&fs=1";
        }

        return BlankMailto;
    }

    /// <summary>
    /// Whether a Microsoft account is a personal one (Outlook.com) rather than work or school. Graph's
    /// own web link says so best (outlook.live.com against outlook.office365.com); before any message
    /// is held, the address's domain does (outlook.*, hotmail.*, live.*, msn.com). A personal account
    /// on its own domain, with no message yet, lands on outlook.office.com, which signs it in there.
    /// </summary>
    private static bool IsPersonalMicrosoft(MailAccount account, IEnumerable<MailMessage> known)
    {
        foreach (var message in known)
        {
            if (message.AccountId != account.Id
                || !Uri.TryCreate(message.WebLink.Trim(), UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps)
            {
                continue;
            }

            if (uri.Host.Equals("outlook.live.com", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (uri.Host.StartsWith("outlook.office", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var at = account.Address.LastIndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var domain = account.Address[(at + 1)..].ToLowerInvariant();
        var first = domain.Split('.')[0];
        return domain is "msn.com" or "passport.com"
            || (first is "outlook" or "hotmail" or "live" or "windowslive" && domain.Count(c => c == '.') is 1 or 2);
    }

    /// <summary>
    /// Whether a link is one of the three shapes this class makes. Re-checked on the literal string
    /// at the moment of launch, so nothing built elsewhere can ride through.
    /// </summary>
    public static bool IsLaunchable(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Any(char.IsControl))
        {
            return false;
        }

        if (IsHttps(link))
        {
            return true;
        }

        if (link.StartsWith(OutlookScheme + "://emails/", StringComparison.Ordinal))
        {
            // The id was percent-encoded: nothing but unreserved characters and escapes may follow.
            var rest = link[(OutlookScheme.Length + "://emails/".Length)..];
            if (rest.EndsWith("/reply", StringComparison.Ordinal))
            {
                rest = rest[..^"/reply".Length];
            }

            return rest.Length > 0 && rest.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '%');
        }

        if (link == BlankMailto)
        {
            return true;
        }

        if (link.StartsWith("mailto:", StringComparison.Ordinal))
        {
            var rest = link["mailto:".Length..];
            var query = rest.IndexOf('?', StringComparison.Ordinal);
            var address = query < 0 ? rest : rest[..query];
            return IsAddress(address)
                && (query < 0 || rest[(query + 1)..].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or '~' or '%' or '='));
        }

        return false;
    }

    /// <summary>A plausible single address: one '@', something either side, no spaces or URL syntax.</summary>
    public static bool IsAddress(string? address)
    {
        if (string.IsNullOrEmpty(address) || address.Length > 320)
        {
            return false;
        }

        var at = address.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && at == address.LastIndexOf('@') && at < address.Length - 1
            && !address.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c is '?' or '&' or '#' or '/' or '\\' or '<' or '>' or '"' or ',' or ';' or ':');
    }

    private static bool IsHttps(string? link)
        => Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0
            && string.IsNullOrEmpty(uri.UserInfo);

    /// <summary>Percent-encode every byte outside RFC 3986's unreserved set.</summary>
    public static string PercentEncode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var output = new StringBuilder(value.Length * 3);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9')
                or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                output.Append((char)b);
            }
            else
            {
                output.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return output.ToString();
    }
}
