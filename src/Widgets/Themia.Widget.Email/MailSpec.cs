using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Widget.Email;

/// <summary>
/// [S-6420] Mail on the kit: its spec (the stacked look going one line on a strip, or the table), what
/// the user chose (<see cref="Choices"/>), a message as a <see cref="KitItem"/> and its actions. Pure,
/// so the menu's lines and the rows' fields are tested here and not in a view.
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

    /// <summary>[S-6403] The spec of a style: List and Compact are the stacked look, Table the table.</summary>
    public static KitSpec Spec(EmailStyle style) => style == EmailStyle.Table ? Table : Stacked;

    private static readonly KitSpec Stacked = new(
        KitRowLook.TwoLine,
        KitShow.All,
        () => string.Empty,
        () => string.Empty,
        KitTime.Compact);

    private static readonly KitSpec Table = new(
        KitRowLook.Table,
        KitShow.All,
        () => string.Empty,
        () => string.Empty,
        KitTime.Compact)
    {
        ColumnHeading = column => Column(column).Label(),
        ColumnTime = KitTime.Stamp,
    };

    /// <summary>[S-543] [S-6403] The fields the user has on: subject and account as chosen, the preview the List style's own.</summary>
    public static KitChoices Choices(EmailConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var shows = KitShow.Lead | KitShow.When
            | (config.Shows(MailListField.Subject) ? KitShow.Subtitle : KitShow.None)
            | (config.Shows(MailListField.Account) ? KitShow.Badges : KitShow.None)
            | (config.ShowsPreview ? KitShow.Preview : KitShow.None);

        return new KitChoices(shows, config.EffectivePreviewLines, [.. config.EffectiveColumns.Select(Kit)]);
    }

    /// <summary>[S-543] A table column of the user's as the kit's.</summary>
    public static KitColumn Kit(MailColumn column) => column switch
    {
        MailColumn.From => KitColumn.Title,
        MailColumn.Subject => KitColumn.Subtitle,
        MailColumn.Date => KitColumn.When,
        MailColumn.Preview => KitColumn.Preview,
        _ => KitColumn.Badge,
    };

    private static MailColumn Column(KitColumn column) => column switch
    {
        KitColumn.Title => MailColumn.From,
        KitColumn.Subtitle => MailColumn.Subject,
        KitColumn.When => MailColumn.Date,
        KitColumn.Preview => MailColumn.Preview,
        _ => MailColumn.Account,
    };

    /// <summary>
    /// [S-1282] [S-1284] A message as a row: the sender, the subject, the preview, its time; unread is
    /// the strong title, the accent bar and time, the closed envelope; read the open envelope. The
    /// account's name is its badge, at every size, never its provider.
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
            new KitLead(KitLeadKind.Icon, (unread ? KitIcon.Mail : KitIcon.MailRead).ToString(), unread ? KitTone.Accent : KitTone.Neutral),
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
