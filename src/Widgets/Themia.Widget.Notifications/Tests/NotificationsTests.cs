using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Notifications.Tests;

/// <summary>[S-16970] The notification list on the item base: a notification as an item, its order, its size and its page.</summary>
public sealed class NotificationsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 42, 0, TimeSpan.Zero);

    private static NotificationItem At(uint id, string app, string appName, string title, int minutesAgo, string body = "")
        => new(id, app, appName, title, body, Now.AddMinutes(-minutesAgo));

    private static readonly NotificationItem[] Sample =
    [
        At(1, "chat", "Chat", "Ana", 3, "See you at 11.\nBring the sketches."),
        At(2, "mail", "Outlook", "Dana", 21),
        At(3, "chat", "Chat", "Team", 30),
        At(4, "mail", "", "", 40),
    ];

    [Fact]
    public void S16970_a_notification_is_an_item_its_apps_icon_title_body_and_age()
    {
        var item = NotificationsSpec.Item(Sample[0], Now, Locale.En);

        Assert.Equal(("1", "Ana", "chat", "Chat"), (item.Id, item.Title, item.Lead!.Key, item.Note));
        Assert.Equal("See you at 11. Bring the sketches.", item.Subtitle);
        Assert.Equal(ItemEndKind.Text, item.End!.Kind);
        Assert.Equal(KitText.When(Locale.En, KitWhen.Instant(Sample[0].Arrived), KitTime.Compact, Now), item.End.Value);

        // A notification without a title is named after its app.
        Assert.Equal(NotificationRules.FallbackName("mail"), NotificationsSpec.Item(Sample[3], Now, Locale.En).Title);
    }

    [Fact]
    public void S16970_newest_first_or_together_by_app()
    {
        Assert.Equal([1u, 2u, 3u, 4u], NotificationsSpec.Ordered(Sample, byApp: false).Select(n => n.Id));
        Assert.Equal([1u, 3u, 2u, 4u], NotificationsSpec.Ordered(Sample, byApp: true).Select(n => n.Id));
    }

    [Fact]
    public void S16970_a_new_list_is_four_items_and_leaves_the_apps_name_out()
    {
        var config = new NotificationsConfig();
        Assert.Equal([ItemPart.Note], config.HiddenParts);

        // The 16..36 lead and the reading-less end: the lead's line, the insets: 60 a notification.
        Assert.Equal(new SizeDip(300, 4 * 60), new NotificationsModule().Descriptor.Sizing(config).Natural);
        Assert.Equal(4 * 30, new NotificationsModule().Descriptor.Sizing(new NotificationsConfig { Style = ItemStyle.Table, ColumnTitles = false }).Natural.Height);

        var wild = new NotificationsConfig { Rows = 0, Gap = 9 };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal((1, 4), (wild.Rows, wild.Gap));
    }

    [Fact]
    public void S16970_the_menu_is_grouped_by_app_then_the_item_lines()
    {
        var config = new NotificationsConfig();
        var menu = new WidgetMenuLines(() => { });
        new NotificationsModule().MenuLines(menu, config);

        Assert.Equal(
            [NotificationsStr.Notifications_ByApp.Text(), Str.Items_Parts.Text(), Str.Items_Gap.Text(), Str.Kit_Rows.Text()],
            menu.Lines.Where(l => !l.IsSeparator).Select(l => l.Text));
        menu.Lines[0].Action!();
        Assert.True(config.ByApp);
    }
}
