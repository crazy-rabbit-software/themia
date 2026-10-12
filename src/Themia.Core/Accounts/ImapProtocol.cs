using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Accounts;

/// <summary>The status of a tagged IMAP response.</summary>
public enum ImapStatus
{
    Ok,
    No,
    Bad,
}

/// <summary>A tagged IMAP response: the status and the human text after it.</summary>
public readonly record struct ImapTagged(ImapStatus Status, string Text);

/// <summary>How a server greeted the connection.</summary>
public enum ImapGreeting
{
    /// <summary><c>* OK</c>: ready for LOGIN.</summary>
    Ok,

    /// <summary><c>* PREAUTH</c>: already authenticated (unusual, but valid).</summary>
    PreAuth,

    /// <summary><c>* BYE</c>: the server is refusing the connection.</summary>
    Bye,

    /// <summary>Not an IMAP greeting: the port speaks something else.</summary>
    NotImap,
}

/// <summary>
/// [S-521] The small part of IMAP (RFC 3501) that proving a login needs, as pure functions: reading a
/// greeting, recognising the tagged answer to a command, and writing a user name and password so no
/// character in them can end the command early.
/// </summary>
public static class ImapProtocol
{
    public static ImapGreeting ParseGreeting(string? line)
    {
        var text = line?.TrimStart() ?? string.Empty;

        if (text.StartsWith("* OK", StringComparison.OrdinalIgnoreCase))
        {
            return ImapGreeting.Ok;
        }

        if (text.StartsWith("* PREAUTH", StringComparison.OrdinalIgnoreCase))
        {
            return ImapGreeting.PreAuth;
        }

        return text.StartsWith("* BYE", StringComparison.OrdinalIgnoreCase) ? ImapGreeting.Bye : ImapGreeting.NotImap;
    }

    /// <summary>The tagged response for <paramref name="tag"/>, or null when the line is something else.</summary>
    public static ImapTagged? ParseTagged(string? line, string tag)
    {
        ArgumentException.ThrowIfNullOrEmpty(tag);

        if (line is null || !line.StartsWith(tag + " ", StringComparison.Ordinal))
        {
            return null;
        }

        var rest = line[(tag.Length + 1)..];
        var space = rest.IndexOf(' ', StringComparison.Ordinal);
        var word = space < 0 ? rest : rest[..space];
        var text = space < 0 ? string.Empty : rest[(space + 1)..].Trim();

        ImapStatus? status = word.ToUpperInvariant() switch
        {
            "OK" => ImapStatus.Ok,
            "NO" => ImapStatus.No,
            "BAD" => ImapStatus.Bad,
            _ => null,
        };

        return status is { } s ? new ImapTagged(s, text) : null;
    }

    /// <summary>Whether a line is a command continuation request (<c>+ …</c>).</summary>
    public static bool IsContinuation(string? line) => line is not null && line.StartsWith('+');

    /// <summary>
    /// A value as an IMAP quoted string, with <c>\</c> and <c>"</c> escaped; null when it holds a
    /// character a quoted string cannot carry (CR, LF, NUL, anything outside 7-bit ASCII), in which
    /// case it must go as a literal (<see cref="LiteralPrefix"/>).
    /// </summary>
    public static string? Quote(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var quoted = new StringBuilder(value.Length + 2).Append('"');

        foreach (var ch in value)
        {
            if (ch is '\r' or '\n' or '\0' || ch > 0x7E || ch < 0x20)
            {
                return null;
            }

            if (ch is '\\' or '"')
            {
                quoted.Append('\\');
            }

            quoted.Append(ch);
        }

        return quoted.Append('"').ToString();
    }

    /// <summary>
    /// [S-553] The RFC 2971 <c>ID</c> the client sends after LOGIN to a server that advertises it.
    /// NetEase (163.com, 126.com) refuses SELECT with "Unsafe Login" from a client that has not named
    /// itself (classic issue #169). Name and vendor only: no version or OS that would fingerprint.
    /// </summary>
    public const string IdCommand = "ID (\"name\" \"Themia\" \"vendor\" \"Lometics GmbH\")";

    /// <summary>The <c>{n}</c> that announces a literal of <paramref name="bytes"/> octets.</summary>
    public static string LiteralPrefix(int bytes)
        => "{" + bytes.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}

/// <summary>A mail provider's published IMAP settings.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Host">The IMAP host.</param>
/// <param name="Port">The implicit-TLS port.</param>
/// <param name="NoteId">What the user has to do first, when the provider refuses an account password.</param>
public sealed record ImapPreset(string Name, string Host, int Port, Str? NoteId)
{
    /// <summary>The provider's sign-in advice in the current language, read when shown (the catalog is static).</summary>
    public string? Note => NoteId?.Text();
}

/// <summary>
/// [S-522] Filling the IMAP server from the address the user typed. A shortcut, never a cage: every
/// field stays editable, and an address that matches nothing leaves the form as it was.
/// <para>
/// Matched on the exact domain or a subdomain of it, never a suffix (<c>notgmail.com</c> is not
/// Gmail). There is deliberately no Outlook.com entry: Microsoft sign-in needs no password and is
/// the better way in.
/// </para>
/// </summary>
public static class ImapPresets
{
    private const Str AppPasswordNote = Str.Shell_Accounts_Imap_AppPassword;

    private static readonly (string[] Domains, ImapPreset Preset)[] Catalog =
    [
        (["gmail.com", "googlemail.com"], new ImapPreset("Gmail", "imap.gmail.com", 993,
            Str.Shell_Accounts_Imap_Gmail)),
        (["yahoo.com", "yahoo.co.uk", "yahoo.de", "yahoo.fr", "ymail.com", "rocketmail.com"],
            new ImapPreset("Yahoo Mail", "imap.mail.yahoo.com", 993, AppPasswordNote)),
        (["icloud.com", "me.com", "mac.com"], new ImapPreset("iCloud Mail", "imap.mail.me.com", 993,
            Str.Shell_Accounts_Imap_ICloud)),
        (["fastmail.com", "fastmail.fm", "messagingengine.com"], new ImapPreset("Fastmail", "imap.fastmail.com", 993, AppPasswordNote)),
        (["zoho.com", "zohomail.com"], new ImapPreset("Zoho Mail", "imap.zoho.com", 993, AppPasswordNote)),
        (["aol.com", "aim.com"], new ImapPreset("AOL Mail", "imap.aol.com", 993, AppPasswordNote)),
        (["gmx.com", "gmx.net", "gmx.de", "gmx.at", "gmx.ch"], new ImapPreset("GMX", "imap.gmx.net", 993,
            Str.Shell_Accounts_Imap_Gmx)),
        (["web.de"], new ImapPreset("WEB.DE", "imap.web.de", 993, Str.Shell_Accounts_Imap_WebDe)),
        (["posteo.de", "posteo.net"], new ImapPreset("Posteo", "posteo.de", 993, null)),
        (["mailbox.org"], new ImapPreset("mailbox.org", "imap.mailbox.org", 993, null)),
        (["yandex.com", "yandex.ru", "ya.ru"], new ImapPreset("Yandex Mail", "imap.yandex.com", 993, AppPasswordNote)),
    ];

    /// <summary>The preset for an address's domain, or null.</summary>
    public static ImapPreset? ForAddress(string? address) => Match(address, Catalog);

    /// <summary>The first entry whose domains include the address's domain or a parent of it.</summary>
    internal static T? Match<T>(string? address, (string[] Domains, T Value)[] catalog)
        where T : class
    {
        var at = address?.LastIndexOf('@') ?? -1;
        if (address is null || at < 0 || at == address.Length - 1)
        {
            return null;
        }

        var domain = address[(at + 1)..].Trim().ToLowerInvariant();

        foreach (var (domains, value) in catalog)
        {
            foreach (var known in domains)
            {
                if (domain == known || domain.EndsWith("." + known, StringComparison.Ordinal))
                {
                    return value;
                }
            }
        }

        return null;
    }
}

/// <summary>
/// [S-522] The same shortcut for CalDAV and CardDAV: the provider's DAV address from the user name,
/// which for these providers is the email address. Discovery ([S-517]) walks from there.
/// </summary>
public static class DavPresets
{
    private static readonly (string[] Domains, string Value)[] Calendars =
    [
        (["icloud.com", "me.com", "mac.com"], "https://caldav.icloud.com"),
        (["fastmail.com", "fastmail.fm", "messagingengine.com"], "https://caldav.fastmail.com/dav/"),
        (["mailbox.org"], "https://dav.mailbox.org/"),
        (["posteo.de", "posteo.net"], "https://posteo.de:8443/"),
        (["yahoo.com", "yahoo.co.uk", "ymail.com", "rocketmail.com"], "https://caldav.calendar.yahoo.com"),
    ];

    private static readonly (string[] Domains, string Value)[] AddressBooks =
    [
        (["icloud.com", "me.com", "mac.com"], "https://contacts.icloud.com"),
        (["fastmail.com", "fastmail.fm", "messagingengine.com"], "https://carddav.fastmail.com/dav/"),
        (["mailbox.org"], "https://dav.mailbox.org/"),
        (["posteo.de", "posteo.net"], "https://posteo.de:8443/"),
        (["yahoo.com", "yahoo.co.uk", "ymail.com", "rocketmail.com"], "https://carddav.address.yahoo.com"),
    ];

    /// <summary>The DAV address for a user name that is an address at a known provider, or null.</summary>
    public static string? ForAddress(string? address, DavKind kind)
        => ImapPresets.Match(address, kind == DavKind.Calendar ? Calendars : AddressBooks);
}
