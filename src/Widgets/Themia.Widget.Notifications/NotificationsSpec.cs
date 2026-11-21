using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Notifications;

/// <summary>
/// [S-4400] [S-11404] Windows' notification list on the kit ([S-7074]): rows of the rich look, a notification's title the
/// name, when it came the value (the kit's compact age), its body on the second line where the config gives two, its
/// app's icon the lead; under app headings where the config says so (a group per app, its heading the app's icon and
/// name, [S-4402]), each app's newest three and a "See more (n)" row, which a click opens and closes.
/// </summary>
public static class NotificationsSpec
{
    /// <summary>The Open action's id: the view opens the sender.</summary>
    public const string OpenId = "open";

    /// <summary>Dismiss: the view takes the one notification away (the kit's Delete).</summary>
    public const string DismissId = "dismiss";

    /// <summary>Clear the app's: the view takes every notification of that app away.</summary>
    public const string ClearAppId = "clearApp";

    /// <summary>[S-4402] A group's "See more" / "See fewer" row's id: this, then the app's id.</summary>
    public const string FoldPrefix = "fold:";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => NotificationsStr.Panels_Notifications_None.Text(),
        KitTime.Compact);

    /// <summary>A notification's id: its number in Windows' list.</summary>
    public static string Id(NotificationItem item) => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The sender's name: the app's, or a name made from its id where Windows gives none.</summary>
    public static string App(NotificationItem item) => item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);

    /// <summary>
    /// [S-4401] [S-4402] The kit items for <paramref name="items"/>: newest first with each its app's icon as the lead; or
    /// under <paramref name="grouped"/> app headings, each app's newest <see cref="NotificationRules.FoldAt"/> (all of
    /// an app in <paramref name="open"/>) and its fold row.
    /// </summary>
    public static IReadOnlyList<KitItem> Items(IReadOnlyList<NotificationItem> items, bool grouped, IReadOnlySet<string> open, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(open);

        if (!grouped)
        {
            return [.. NotificationRules.Newest(items).Select(item => Item(item, null, lead: true, locale))];
        }

        var rows = new List<KitItem>();
        foreach (var group in NotificationRules.Groups(items))
        {
            var heading = new KitGroup(group.AppId, group.AppName);
            var opened = open.Contains(group.AppId);
            var (shown, more) = NotificationRules.Fold(group, opened, NotificationRules.FoldAt);
            rows.AddRange(shown.Select(item => Item(item, heading, lead: false, locale)));
            if (more > 0 || (opened && group.Items.Count > NotificationRules.FoldAt))
            {
                rows.Add(new KitItem(
                    FoldPrefix + group.AppId,
                    more > 0 ? NotificationsStr.Panels_Notifications_SeeMore.Format(locale, ("count", LocaleFormat.Integer(locale, more))) : NotificationsStr.Panels_Notifications_SeeFewer.Text(locale),
                    Group: heading));
            }
        }

        return rows;
    }

    /// <summary>The app a fold row opens and closes, or null for a notification's row.</summary>
    public static string? FoldOf(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Id.StartsWith(FoldPrefix, StringComparison.Ordinal) ? item.Id[FoldPrefix.Length..] : null;
    }

    private static KitItem Item(NotificationItem item, KitGroup? group, bool lead, Locale locale)
    {
        var title = item.Title.Length > 0 ? item.Title : NotificationRules.FallbackName(item.AppId);
        return new KitItem(
            Id(item),
            title,
            Subtitle: item.Body.Length > 0 ? ClipboardFormatting.Preview(item.Body, NotificationRules.MostBody) : null,
            Lead: lead ? new KitLead(KitLeadKind.Icon, item.AppId) : KitLead.None,
            When: KitWhen.Instant(item.Arrived),
            Group: group,
            Tooltip: NotificationsStr.Panels_Notifications_Open.Format(locale, ("app", App(item))));
    }

    /// <summary>
    /// [S-4401] A notification's menu: Open its app (Enter, a double-click), Clear the app's, then Dismiss (Delete); a fold
    /// row's: its words, which open or close the group.
    /// </summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item, NotificationItem? notification)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (FoldOf(item) is not null)
        {
            return [new KitAction(KitActionKind.Open, item.Title, KitIcon.Default, Id: FoldPrefix)];
        }

        if (notification is null)
        {
            return [];
        }

        var app = App(notification);
        return
        [
            new KitAction(KitActionKind.Open, NotificationsStr.Panels_Notifications_Open.Format(("app", app)), KitIcon.App, Id: OpenId),
            new KitAction(KitActionKind.Change, NotificationsStr.Panels_Notifications_ClearApp.Format(("app", app)), KitIcon.Clear, Id: ClearAppId),
            new KitAction(KitActionKind.Delete, NotificationsStr.Panels_Notifications_Dismiss.Text(), KitIcon.Dismiss, Id: DismissId),
        ];
    }
}
