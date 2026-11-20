using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Notifications.Tests;

/// <summary>[S-11404] The notification list states its rows: its plan and its size from its config.</summary>
public sealed class NotificationsTests
{
    [Fact]
    public void S11404_a_new_list_is_four_notifications_of_two_lines_flat_with_icons_and_times()
    {
        var config = new NotificationsConfig();
        var plan = NotificationsLayout.Plan(config);

        Assert.Equal(new NotificationPlan(2, true, true, false, NotificationRules.FoldAt, 4), plan);
        Assert.Equal(new SizeDip(300, 4 * 40), new NotificationsModule().Descriptor.Sizing(config).Natural);
    }

    [Fact]
    public void S11404_one_line_and_app_headings_are_the_config_s()
    {
        var plan = NotificationsLayout.Plan(new NotificationsConfig { Lines = 1, AppHeadings = true });
        Assert.Equal(1, plan.Lines);
        Assert.True(plan.Grouped);
        Assert.Equal(FamilyShape.Row1, plan.RowHeight);
    }

    [Fact]
    public void S11404_the_menu_offers_rows_lines_and_the_headings_switch()
    {
        var config = new NotificationsConfig();
        var menu = new WidgetMenuLines(() => { });
        new NotificationsModule().MenuLines(menu, config);

        Assert.Equal(
            [Str.Kit_Rows.Text(), Str.Kit_Lines.Text(), NotificationsStr.Notifications_AppHeadings.Text()],
            menu.Lines.Select(l => l.Text));
        menu.Lines[2].Action!();
        Assert.True(config.AppHeadings);
    }

    [Fact]
    public void S11404_its_rows_are_its_natural_height_and_out_of_range_is_repaired()
    {
        var sizing = new NotificationsModule().Descriptor.Sizing(new NotificationsConfig { Rows = 6, Lines = 2 });
        Assert.Equal((40d, 6 * 40d), (sizing.Min.Height, sizing.Natural.Height));

        var wild = new NotificationsConfig { Rows = 0, Lines = 5 };
        Assert.NotEmpty(wild.Repair());
        Assert.Equal((1, 2), (wild.Rows, wild.Lines));
    }
}
