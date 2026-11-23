using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Widget.Email;

/// <summary>
/// [S-6420] [S-16971] Mail on the kit and the item base: its spec, a message as a <see cref="KitItem"/> and as the base's item,
/// and its actions. Pure, so the menu's lines and the items' fields are tested here and not in a view.
/// </summary>
public static class MailSpec
{
    /// <summary>The action ids the view's handler carries out.</summary>
    public const string OpenId = "open";

    public const string ReplyId = "reply";
    public const string ReadId = "read";
    public const string UnreadId = "unread";
    public const string ArchiveId = "archive";
    public const string JunkId = "junk";
    public const string DeleteId = "delete";

    /// <summary>[S-16971] The parts a message has, in its own order: the sender, the subject, the account, the time.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Title, ItemPart.Subtitle, ItemPart.Note, ItemPart.End];

    /// <summary>
    /// [S-16971] The kit's spec of the list: its screens' words (an empty folder says which folder it is, "No messages in
    /// Inbox"), its compact ages; the items are the item base's.
    /// </summary>
    /// <param name="folder">The folder the widget lists now; Inbox when null.</param>
    public static KitSpec Spec(Func<MailFolder>? folder = null)
    {
        string Empty() => Str.WidgetsB_Email_NoMessagesIn.Format(("folder", (folder?.Invoke() ?? MailFolder.Inbox).Label()));
        return new(KitRowLook.Line, KitShow.All, () => string.Empty, Empty, KitTime.Compact) { Unread = true };
    }

    /// <summary>
    /// [S-16971] [S-2270] A message as the item base's item at <paramref name="now"/>: the sender the title (unread strong and
    /// primary, read plain and secondary: no lead), the subject under it (the preview after it where
    /// <paramref name="preview"/>), when it came at the end (the kit's compact age), the account's name the note.
    /// </summary>
    public static Themia.Sdk.Items.Item Base(KitItem message, bool preview, DateTimeOffset now, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(message);
        var subject = message.Subtitle ?? string.Empty;
        var line = preview && message.Preview is { Length: > 0 } text ? subject.Length > 0 ? $"{subject} – {text}" : text : subject;
        var when = KitText.When(locale, message.When, KitTime.Compact, now);
        return new Themia.Sdk.Items.Item(
            message.Id,
            message.Title,
            null,
            line,
            when.Length > 0 ? ItemEnd.Text(when) : null,
            Note: message.Badges is [{ Text: { Length: > 0 } account }, ..] ? account : string.Empty,
            Tooltip: message.Tooltip ?? string.Empty,
            Strong: message.Emphasis,
            Muted: !message.Emphasis);
    }

    /// <summary>[S-16971] A message's words for its parts: From, Subject, Account, Date.</summary>
    public static string PartName(ItemPart part) => Strings.Get(Strings.Current, part switch
    {
        ItemPart.Subtitle => Str.Email_Col_Subject,
        ItemPart.Note => Str.Email_Col_Account,
        ItemPart.End => Str.Email_Col_Date,
        ItemPart.Title => Str.Email_Col_From,
        _ => Str.Items_Part_Lead,
    });

    /// <summary>
    /// [S-1282] [S-1284] A message as a row: the sender, the subject, the preview, its time; unread is
    /// the strong name in the primary role, read the plain name in the secondary ([S-2270]); no lead.
    /// The account's name is its badge, at every size, never its provider.
    /// </summary>
    /// <param name="account">The account it came to, for its name and the tooltip; null when it is gone.</param>
    public static KitItem Item(MailMessage message, MailAccount? account)
    {
        ArgumentNullException.ThrowIfNull(message);

        var unread = !message.IsRead;
        return new KitItem(
            message.Key,
            message.Sender,
            message.SubjectLine,
            message.Preview,
            KitLead.None,
            message.Received is { } at ? KitWhen.Instant(at) : null,
            unread ? KitTone.Accent : KitTone.Neutral,
            unread,
            account is null ? null : [new KitBadge(account.Label)],
            Tooltip: Tooltip(message, account));
    }

    /// <summary>
    /// [S-544] A message's own actions, offered only where its account allows changes: Open, Reply,
    /// Mark read or unread, Archive and Move to Junk (not in Junk), Copy subject, Delete. The quick
    /// actions on the widest rows are the mark, Archive and Delete ([S-1284]).
    /// </summary>
    /// <param name="canChange">The account lets Themia change it (Gmail is read-only).</param>
    public static IReadOnlyList<KitAction> Actions(MailMessage message, bool canChange)
    {
        ArgumentNullException.ThrowIfNull(message);

        var actions = new List<KitAction>
        {
            new(KitActionKind.Open, Str.CtxMenu_Open.Text(), Id: OpenId),
            new(KitActionKind.OpenTo, EmailStr.CtxMenu_Reply.Text(), KitIcon.Reply, Id: ReplyId),
        };

        if (canChange)
        {
            actions.Add(message.IsRead
                ? new(KitActionKind.Change, EmailStr.WidgetsB_Email_MarkUnread.Text(), KitIcon.MailUnread, Id: UnreadId, Inline: true)
                : new(KitActionKind.Change, EmailStr.WidgetsB_Email_MarkRead.Text(), KitIcon.MailRead, Id: ReadId, Inline: true));

            if (message.Folder != MailFolder.Junk)
            {
                actions.Add(new(KitActionKind.Change, EmailStr.CtxMenu_Archive.Text(), KitIcon.Archive, Id: ArchiveId, Inline: true));
                actions.Add(new(KitActionKind.Change, EmailStr.WidgetsB_Email_MoveToJunk.Text(), KitIcon.Junk, Id: JunkId));
            }
        }

        actions.Add(new(KitActionKind.CopyText, EmailStr.CtxMenu_CopySubject.Text(), Text: message.SubjectLine));

        if (canChange)
        {
            actions.Add(new(KitActionKind.Delete, DeleteLabel(message), Id: DeleteId, Inline: true));
        }

        return actions;
    }

    /// <summary>The change an action id asks for, or null for one that is not a change.</summary>
    public static MailChange? Change(string? id) => id switch
    {
        ReadId => MailChange.MarkRead,
        UnreadId => MailChange.MarkUnread,
        ArchiveId => MailChange.Archive,
        JunkId => MailChange.Junk,
        DeleteId => MailChange.Delete,
        _ => null,
    };

    private static string DeleteLabel(MailMessage message)
        => message.Folder == MailFolder.Deleted ? EmailStr.WidgetsB_Email_DeletePermanently.Text() : Str.Common_Delete.Text();

    /// <summary>Who it is from, the subject, the full date, and which account it came to.</summary>
    private static string Tooltip(MailMessage message, MailAccount? account)
    {
        var from = message.FromAddress.Length > 0 && message.FromName.Length > 0
            ? $"{message.FromName} <{message.FromAddress}>"
            : message.Sender;

        var when = message.Received is { } at ? Environment.NewLine + WhenLong(at.ToLocalTime()) : string.Empty;
        var to = account is null ? string.Empty : Environment.NewLine + account.Label;
        return from + Environment.NewLine + message.SubjectLine + when + to;
    }

    /// <summary>"Sunday, October 4, 2026, 9:05 AM" in the current language.</summary>
    private static string WhenLong(DateTimeOffset local)
        => EmailStr.WidgetsB_Time_DateTime.Format(
            ("date", LocaleFormat.Date(Strings.Current, local.DateTime, DateStyle.Full)),
            ("time", LocaleFormat.Time(Strings.Current, local.DateTime)));
}
