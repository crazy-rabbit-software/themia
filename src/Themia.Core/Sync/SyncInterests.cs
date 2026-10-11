using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Tasks;

namespace Themia.Core.Sync;

/// <param name="AccountIds">The widget's accounts; empty means every mail account ([S-541]).</param>
public sealed record MailInterest(IReadOnlyList<string> AccountIds, MailFolder Folder, int Count);

/// <param name="AccountIds">Empty means every calendar account ([S-562]).</param>
/// <param name="Calendars">The widget's calendar choice per account ([S-567]).</param>
public sealed record CalendarInterest(IReadOnlyList<string> AccountIds, IReadOnlyDictionary<string, List<string>> Calendars, bool Birthdays);

/// <param name="AccountIds">Empty means every contacts account ([S-580]).</param>
public sealed record ContactsInterest(IReadOnlyList<string> AccountIds);

/// <param name="AccountId">Empty means the first task account ([S-590]).</param>
/// <param name="ListId">A list, Today, or empty for the account's default list.</param>
public sealed record TaskInterest(string AccountId, string ListId, bool ShowCompleted, TimeSpan Interval);

public sealed record FeedInterest(string Url, int MaxItems, TimeSpan Interval);

/// <summary>
/// [S-2608] What the sync services keep on the shelf: the needs of every widget in every view, shown
/// or not. A view the user switches to is already synced, so switching draws and asks nothing. The
/// cost is bounded by what the views hold, and it stops with the desktop: the cadence rides the shared
/// ticker, which stops while the desktop is hidden.
/// </summary>
public sealed record SyncInterests(
    IReadOnlyList<MailInterest> Mail,
    IReadOnlyList<CalendarInterest> Calendar,
    IReadOnlyList<ContactsInterest> Contacts,
    IReadOnlyList<TaskInterest> Tasks,
    IReadOnlyList<FeedInterest> Feeds)
{
    public static SyncInterests None { get; } = new([], [], [], [], []);

    /// <summary>
    /// Everything that changes what is synced, as one string: the state changes sixty times a second
    /// during a drag, and the services only need to hear when this does.
    /// </summary>
    public string Key()
    {
        var text = new System.Text.StringBuilder();

        foreach (var m in Mail)
        {
            text.Append("m|").AppendJoin(',', m.AccountIds).Append('|').Append(m.Folder.Wire()).Append('|').Append(m.Count).Append('#');
        }

        foreach (var c in Calendar)
        {
            text.Append("c|").AppendJoin(',', c.AccountIds).Append('|').Append(c.Birthdays).Append('|');
            foreach (var (account, ids) in c.Calendars.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                text.Append(account).Append('=').AppendJoin(',', ids).Append(';');
            }

            text.Append('#');
        }

        foreach (var p in Contacts)
        {
            text.Append("p|").AppendJoin(',', p.AccountIds).Append('#');
        }

        foreach (var t in Tasks)
        {
            text.Append("t|").Append(t.AccountId).Append('|').Append(t.ListId).Append('|').Append(t.ShowCompleted).Append('|').Append(t.Interval.Ticks).Append('#');
        }

        foreach (var f in Feeds)
        {
            text.Append("f|").Append(f.Url).Append('|').Append(f.MaxItems).Append('|').Append(f.Interval.Ticks).Append('#');
        }

        return text.ToString();
    }

    public static SyncInterests From(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var mail = new List<MailInterest>();
        var calendar = new List<CalendarInterest>();
        var contacts = new List<ContactsInterest>();
        var tasks = new List<TaskInterest>();
        var feeds = new List<FeedInterest>();

        foreach (var widget in state.Views.SelectMany(v => v.Widgets))
        {
            switch (widget.Config)
            {
                case EmailConfig email:
                    mail.Add(new MailInterest([.. email.EffectiveAccountIds], email.Folder, email.FetchCount(0)));
                    break;

                case CalendarConfig cal:
                    calendar.Add(new CalendarInterest([.. cal.AccountIds], cal.Calendars, cal.ShowBirthdays));
                    break;

                case ContactsConfig people:
                    contacts.Add(new ContactsInterest([.. people.AccountIds]));
                    break;

                case TasksConfig list when list.Source == TaskSource.Account:
                    tasks.Add(new TaskInterest(list.AccountId ?? string.Empty, list.ListId ?? string.Empty, list.ShowCompleted, list.RefreshInterval));
                    break;

                case RssConfig rss:
                    foreach (var url in rss.FeedUrls)
                    {
                        feeds.Add(new FeedInterest(url, rss.EffectiveMaxItems, rss.TickInterval));
                    }

                    break;
            }
        }

        return new SyncInterests(mail, calendar, contacts, tasks, feeds);
    }
}
