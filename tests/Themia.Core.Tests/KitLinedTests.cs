using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-7013] [S-7032]..[S-7034] The kit's lined looks (the stacked rows going one line on a strip, the
/// table), their times, and the actions an account widget keeps: Mail's ladder ([S-556],
/// [S-1280]..[S-1287]) as the kit's.
/// </summary>
public sealed class KitLinedTests
{
    private static readonly KitSpec Stacked = new(KitRowLook.TwoLine, KitShow.All, () => "(untitled)", () => "Empty", KitTime.Compact);

    private static readonly KitSpec Table = new(KitRowLook.Table, KitShow.All, () => "(untitled)", () => "Empty", KitTime.Compact);

    private static KitPlan Plan(KitSpec spec, double width, double height, KitShow? shows = null, IReadOnlyList<KitColumn>? columns = null)
        => KitPlanner.Plan(new WidgetArea(width, height), spec, new KitChoices(shows ?? KitShow.All, columns));

    [Fact]
    public void S7032_the_one_line_look_and_a_compact_style_keep_one_line_everywhere()
    {
        var line = Stacked with { Look = KitRowLook.Line };
        Assert.Equal(1, Plan(line, 800, 524).Lines);
        Assert.Equal(1, KitPlanner.Plan(new WidgetArea(800, 524), Stacked, new KitChoices(KitShow.All) { OneLine = true }).Lines);
    }

    [Fact]
    public void S11401_a_table_shows_every_chosen_column_in_the_users_order_at_every_width()
    {
        KitColumn[] chosen = [KitColumn.Subtitle, KitColumn.Badge, KitColumn.Title, KitColumn.When];

        // No width adds a column or drops one ([S-11800]): the range starts where they all fit.
        foreach (var width in new double[] { 260, 600, 760 })
        {
            Assert.Equal(chosen, KitPlanner.TableColumns(chosen, width, 8).Select(c => c.Column));
        }

        KitColumn[] lone = [KitColumn.Title, KitColumn.Subtitle, KitColumn.When];
        Assert.Equal(lone, KitPlanner.TableColumns(lone, 760, 8).Select(c => c.Column));

        // The least width holds every column at its own least, the gaps, the insets and a lead.
        Assert.Equal((84 + 96 + 80 + 56) + (3 * KitPlanner.ColumnGap) + (2 * FamilySpace.Inset), KitPlanner.TableLeast(chosen, lead: false));
        Assert.Equal(KitPlanner.TableLeast(chosen, lead: false) + RowRules.NameStart(FamilyShape.Icon), KitPlanner.TableLeast(chosen, lead: true));
    }

    [Fact]
    public void S7034_the_flexible_columns_share_the_rest()
    {
        // With no flexible column the first one takes the slack.
        var fixedOnly = KitPlanner.TableColumns([KitColumn.When, KitColumn.Title], 400, 8);
        Assert.Equal(1, fixedOnly[0].Star);
        Assert.Equal(0, fixedOnly[1].Star);
    }

    [Fact]
    public void S548_compact_times_and_table_stamps()
    {
        var now = new DateTimeOffset(2026, 10, 14, 15, 0, 0, TimeSpan.FromHours(2));
        var en = Locale.En;

        Assert.Equal(KitText.Compact(en, now.AddMinutes(-4), now), KitText.When(en, KitWhen.Instant(now.AddMinutes(-4)), KitTime.Compact, now));
        Assert.Equal("4m", KitText.Compact(en, now.AddMinutes(-4), now));
        Assert.Equal(KitText.Compact(en, now, now), KitText.Compact(en, now.AddMinutes(3), now)); // a clock ahead reads "now"
        Assert.Equal(LocaleFormat.Time(en, now.AddHours(-2).DateTime), KitText.Stamp(en, now.AddHours(-2), now));
        Assert.Equal(LocaleFormat.WeekdayName(en, now.AddDays(-3).DayOfWeek, NameWidth.Short), KitText.Stamp(en, now.AddDays(-3), now));
    }

    [Fact]
    public void S7013_an_open_the_view_carries_out_runs_without_an_address()
    {
        Assert.True(new KitAction(KitActionKind.Open, "Open", Id: "open").Runnable);
        Assert.False(new KitAction(KitActionKind.Open, "Open").Runnable);
        Assert.True(new KitAction(KitActionKind.Change, "Archive", Id: "archive").Runnable);
        Assert.NotNull(KitMenu.Primary([new KitAction(KitActionKind.Open, "Open", Id: "open")]));
    }

    [Fact]
    public void S7013_changes_follow_the_web_pages_in_the_items_group_in_the_specs_order()
    {
        var lines = KitMenu.Lines(
        [
            new(KitActionKind.Open, "Open", Id: "open"),
            new(KitActionKind.OpenTo, "Reply", Id: "reply"),
            new(KitActionKind.Change, "Mark as read", Id: "read"),
            new(KitActionKind.Change, "Archive", Id: "archive"),
            new(KitActionKind.CopyText, "Copy subject", Text: "s"),
            new(KitActionKind.Delete, "Delete", Id: "delete"),
        ]);

        Assert.Equal(["Open", "Reply", "Mark as read", "Archive", "Copy subject", "-", "Delete"], lines.Select(l => l.Action?.Label ?? "-"));
    }
}
