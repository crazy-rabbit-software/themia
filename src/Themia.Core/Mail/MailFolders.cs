using System.Globalization;
using Themia.Core.Accounts;

namespace Themia.Core.Mail;

/// <summary>
/// [S-554] One folder, named for each provider: Graph's well-known name, Gmail's system label, and
/// for IMAP the SPECIAL-USE attribute (RFC 6154) plus the names servers use when they advertise none.
/// </summary>
public static class MailFolders
{
    /// <summary>Microsoft Graph's well-known folder name.</summary>
    public static string GraphName(this MailFolder folder) => folder.Wire();

    /// <summary>Gmail's system label. Archive has none (it is "not in the inbox"), so it is never listed by label.</summary>
    public static string GmailLabel(this MailFolder folder) => folder switch
    {
        MailFolder.Sent => "SENT",
        MailFolder.Drafts => "DRAFT",
        MailFolder.Deleted => "TRASH",
        MailFolder.Junk => "SPAM",
        _ => "INBOX",
    };

    /// <summary>The IMAP SPECIAL-USE attribute for a folder, or null for the inbox.</summary>
    public static string? SpecialUse(this MailFolder folder) => folder switch
    {
        MailFolder.Sent => @"\Sent",
        MailFolder.Drafts => @"\Drafts",
        MailFolder.Deleted => @"\Trash",
        MailFolder.Junk => @"\Junk",
        MailFolder.Archive => @"\Archive",
        _ => null,
    };

    /// <summary>
    /// Mailbox names to try, in order, when the server tags none with SPECIAL-USE. Covers the common
    /// layouts including Gmail's <c>[Gmail]/…</c> hierarchy.
    /// </summary>
    public static IReadOnlyList<string> Candidates(this MailFolder folder) => folder switch
    {
        MailFolder.Sent => ["Sent", "Sent Items", "Sent Mail", "Sent Messages", "[Gmail]/Sent Mail", "INBOX.Sent"],
        MailFolder.Drafts => ["Drafts", "[Gmail]/Drafts", "INBOX.Drafts"],
        MailFolder.Deleted => ["Trash", "Deleted", "Deleted Items", "Deleted Messages", "Bin", "[Gmail]/Trash", "[Gmail]/Bin", "INBOX.Trash"],
        MailFolder.Junk => ["Junk", "Spam", "Junk E-mail", "Junk Email", "[Gmail]/Spam", "INBOX.Junk", "INBOX.Spam"],
        MailFolder.Archive => ["Archive", "Archives", "[Gmail]/All Mail", "All Mail", "INBOX.Archive"],
        _ => [],
    };

    /// <summary>
    /// The real mailbox for a folder on this server: INBOX is constant; otherwise the mailbox tagged
    /// with the matching SPECIAL-USE attribute (Gmail's <c>[Gmail]/Sent Mail</c>, Outlook's
    /// <c>Sent Items</c>, Dovecot's <c>Sent</c> all work without per-provider cases), else the first
    /// candidate name that exists, else the first candidate.
    /// </summary>
    public static string ResolveImap(this MailFolder folder, IReadOnlyList<ImapMailbox> mailboxes)
    {
        ArgumentNullException.ThrowIfNull(mailboxes);

        if (folder == MailFolder.Inbox)
        {
            return "INBOX";
        }

        if (folder.SpecialUse() is { } flag)
        {
            foreach (var mailbox in mailboxes)
            {
                if (mailbox.Attributes.Any(a => a.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    && !mailbox.Attributes.Any(a => a.Equals(@"\Noselect", StringComparison.OrdinalIgnoreCase)))
                {
                    return mailbox.Name;
                }
            }
        }

        var candidates = folder.Candidates();

        foreach (var candidate in candidates)
        {
            foreach (var mailbox in mailboxes)
            {
                if (mailbox.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    return mailbox.Name;
                }
            }
        }

        return candidates.Count > 0 ? candidates[0] : "INBOX";
    }
}

/// <summary>
/// [S-555] An IMAP message's handle: the folder it was listed in, its UID, and the mailbox's
/// UIDVALIDITY when it was listed (<c>folder|uid|uidvalidity</c>).
/// <para>
/// The folder makes an action land in the mailbox the message was shown from, not a hard-coded
/// INBOX. The UIDVALIDITY pins the mailbox's incarnation: a mailbox deleted and recreated reuses
/// UIDs for different messages, so an action refuses instead of hitting the wrong one — for a
/// delete that is the difference between a no-op and silent data loss.
/// </para>
/// </summary>
public readonly record struct ImapMessageId(MailFolder Folder, uint Uid, uint? UidValidity)
{
    public override string ToString() => UidValidity is { } validity
        ? string.Create(CultureInfo.InvariantCulture, $"{Folder.Wire()}|{Uid}|{validity}")
        : string.Create(CultureInfo.InvariantCulture, $"{Folder.Wire()}|{Uid}");

    /// <summary>
    /// Read a handle: <c>folder|uid|uidvalidity</c>, or <c>folder|uid</c> when the server reported no
    /// validity (the guard is then skipped). Anything else is refused, never coerced.
    /// </summary>
    public static ImapMessageId? Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var parts = raw.Split('|');
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }

        if (MailNames.ParseFolder(parts[0]) is not { } f || !uint.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var uid))
        {
            return null;
        }

        if (parts.Length < 3)
        {
            return new ImapMessageId(f, uid, null);
        }

        return uint.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var validity)
            ? new ImapMessageId(f, uid, validity)
            : null;
    }

    /// <summary>
    /// Whether a UID-targeted change may go ahead in a mailbox whose UIDVALIDITY is now
    /// <paramref name="current"/>. Refused only on a known mismatch: an id minted without a validity,
    /// or a server that reports none, cannot be checked and proceeds (never worse than no guard).
    /// </summary>
    public bool StillValidIn(uint? current) => UidValidity is not { } expected || current is not { } actual || expected == actual;
}

/// <summary>[S-553] The small IMAP decisions the mail client makes, kept pure for tests.</summary>
public static class ImapRules
{
    /// <summary>
    /// The sequence range of the <paramref name="count"/> newest messages in a mailbox holding
    /// <paramref name="exists"/>: <c>[exists-count+1 .. exists]</c>, never below 1 and never inverted
    /// (servers reject a high:low range, which silently returned nothing in the classic app's issue
    /// #45). A single message is <c>"n"</c>. Null when there is nothing to fetch.
    /// </summary>
    public static string? RecentRange(long exists, int count)
    {
        if (exists <= 0 || count <= 0)
        {
            return null;
        }

        var start = Math.Max(1, exists - count + 1);
        return start >= exists
            ? exists.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{start}:{exists}");
    }

    /// <summary>
    /// The FETCH items: the header, the flags, the UID, and the first 32 KB of the text — enough for
    /// a 200-character preview, and bounded so a message with a large attachment costs 32 KB rather
    /// than the attachment. PEEK, so listing never marks anything read. The list is parenthesised:
    /// RFC 3501 allows a bare item only when there is one, and a strict server (Dovecot) refuses the
    /// bare form with "Invalid arguments" (the classic app's issue #45).
    /// </summary>
    public const string FetchItems = "(UID FLAGS RFC822.HEADER BODY.PEEK[TEXT]<0.32768>)";

    /// <summary>
    /// What a refused LOGIN means. Only the user can mend wrong credentials; a server that says it is
    /// temporarily unavailable (RFC 5530 <c>[UNAVAILABLE]</c>) or in use is a transient failure and
    /// must not mark the account as needing a sign-in.
    /// </summary>
    public static AccountError LoginRefusal(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Contains("[UNAVAILABLE]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("[INUSE]", StringComparison.OrdinalIgnoreCase)
            || text.Contains("[LIMIT]", StringComparison.OrdinalIgnoreCase)
                ? AccountError.ServerError
                : AccountError.SignInRequired;
    }

    /// <summary>A mailbox name as a command argument: a quoted string, or null when it needs a literal.</summary>
    public static string? QuoteMailbox(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return ImapProtocol.Quote(name);
    }
}
