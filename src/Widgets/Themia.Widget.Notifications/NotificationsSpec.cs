using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.App.Shell;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Widget.Notifications;

/// <summary>
/// [S-4400] [S-16970] Windows' notification list on the item base ([S-16940]; owner, 2026-11-23: "notifications and upcoming
/// meetings should get the same base widget as the bluetooth and drive devices"): a notification as an item, its app's icon
/// the lead, its title, its body the subtitle, when it came the end (the kit's compact age), its app's name the note; newest
/// first, or together by app (the app with the newest first); its actions.
/// </summary>
public static class NotificationsSpec
{
    /// <summary>The Open action's id: the view opens the sender.</summary>
    public const string OpenId = "open";

    /// <summary>Dismiss: the view takes the one notification away (the kit's Delete).</summary>
    public const string DismissId = "dismiss";

    /// <summary>Clear the app's: the view takes every notification of that app away.</summary>
    public const string ClearAppId = "clearApp";

    /// <summary>[S-16970] The parts a notification has, in its own order.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.Note, ItemPart.End];

    public static KitSpec Spec { get; } = new(
        KitRowLook.Line,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        static () => string.Empty,
        static () => NotificationsStr.Panels_Notifications_None.Text(),
        KitTime.Compact);

    /// <summary>A notification's id: its number in Windows' list.</summary>
    public static string Id(NotificationItem item) => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The sender's name: the app's, or a name made from its id where Windows gives none.</summary>
    public static string App(NotificationItem item) => item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);

    /// <summary>
    /// [S-4401] [S-16970] The notifications in the list's order: newest first, or <paramref name="byApp"/> together by app, the
    /// app with the newest first, each app's newest first.
    /// </summary>
    public static IReadOnlyList<NotificationItem> Ordered(IReadOnlyList<NotificationItem> items, bool byApp)
    {
        ArgumentNullException.ThrowIfNull(items);
        return byApp ? [.. NotificationRules.Groups(items).SelectMany(g => g.Items)] : NotificationRules.Newest(items);
    }

    /// <summary>[S-4401] The kit items for <paramref name="items"/>, in the list's order.</summary>
    public static IReadOnlyList<KitItem> Items(IReadOnlyList<NotificationItem> items, bool byApp)
        => [.. Ordered(items, byApp).Select(item => new KitItem(Id(item), item.Title, When: KitWhen.Instant(item.Arrived)))];

    /// <summary>
    /// [S-16970] A notification as an item at <paramref name="now"/>: its app's icon (the bell until it comes, [S-2271]), its title
    /// (else its app's name), its body on one line (its own breaks read as spaces), when it came as the kit's compact age,
    /// its app's name the note.
    /// </summary>
    public static Item Item(NotificationItem item, DateTimeOffset now, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(item);
        var app = App(item);
        return new Item(
            Id(item),
            item.Title.Length > 0 ? item.Title : NotificationRules.FallbackName(item.AppId),
            new ItemLead(Glyphs.Bell, Key: item.AppId),
            item.Body.Length > 0 ? ClipboardFormatting.Preview(item.Body, NotificationRules.MostBody) : string.Empty,
            ItemEnd.Text(KitText.When(locale, KitWhen.Instant(item.Arrived), KitTime.Compact, now)),
            Note: app,
            Tooltip: NotificationsStr.Panels_Notifications_Open.Format(locale, ("app", app)));
    }

    /// <summary>[S-16970] A notification item's words for its parts: "Title", "Message", "App", "Time".</summary>
    public static string PartName(ItemPart part, Locale locale) => part switch
    {
        ItemPart.Title => NotificationsStr.Notifications_Part_Title.Text(locale),
        ItemPart.Subtitle => NotificationsStr.Notifications_Part_Subtitle.Text(locale),
        ItemPart.Note => NotificationsStr.Notifications_Part_Note.Text(locale),
        ItemPart.End => NotificationsStr.Notifications_Part_End.Text(locale),
        _ => Strings.Get(locale, Str.Items_Part_Lead),
    };

    /// <summary>[S-4401] A notification's menu: Open its app (Enter, a click), Clear the app's, then Dismiss (Delete).</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item, NotificationItem? notification)
    {
        ArgumentNullException.ThrowIfNull(item);
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
