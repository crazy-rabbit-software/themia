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

    private static readonly KitShow NoPreview = KitShow.All & ~KitShow.Preview;

    private static KitPlan Plan(KitSpec spec, double width, double height, KitShow? shows = null, int lines = 2, IReadOnlyList<KitColumn>? columns = null)
        => KitLadder.Plan(new WidgetArea(width, height), spec, new KitChoices(shows ?? KitShow.All, lines, columns));

    [Fact]
    public void S7032_a_wide_strip_and_a_very_wide_tile_go_one_line_per_item()
    {
        Assert.Equal(KitRowLook.Line, Plan(Stacked, 480, 116).Look);    // Wide strip
        Assert.Equal(KitRowLook.TwoLine, Plan(Stacked, 480, 300).Look); // wide but tall: stacked rows still pay
        Assert.Equal(KitRowLook.Line, Plan(Stacked, 800, 516).Look);    // XL
        Assert.Equal(KitRowLook.TwoLine, Plan(Stacked, 320, 196).Look); // M
        Assert.Equal(KitRowLook.TwoLine, Plan(Stacked, 200, 356).Look); // Tall

        // The table is one line already; it keeps its columns.
        Assert.Equal(KitRowLook.Table, Plan(Table, 800, 516).Look);
    }

    [Fact]
    public void S7032_one_line_rows_add_the_preview_then_the_badge_as_they_widen()
    {
        Assert.Equal(0, Plan(Stacked, 430, 116).PreviewLines);

        var strip = Plan(Stacked, 480, 116);
        Assert.Equal(1, strip.PreviewLines);
        Assert.False(strip.Badges);

        var wider = Plan(Stacked, 560, 116);
        Assert.Equal(1, wider.PreviewLines);
        Assert.True(wider.Badges);

        Assert.Equal(1, Plan(Stacked, 800, 516).PreviewLines);

        // A preview the user does not show never comes, on one line or two.
        Assert.Equal(0, Plan(Stacked, 800, 516, NoPreview).PreviewLines);
        Assert.Equal(0, Plan(Stacked, 320, 300, NoPreview).PreviewLines);
    }

    [Fact]
    public void S7032_the_lead_shows_from_a_medium_tile()
    {
        Assert.False(Plan(Stacked, 200, 106).Lead);
        Assert.True(Plan(Stacked, 320, 196).Lead);
        Assert.True(Plan(Stacked, 480, 116).Lead);
    }

    [Fact]
    public void S7032_stacked_rows_shed_the_badge_then_the_preview_then_its_lines()
    {
        var m = Plan(Stacked, 320, 196);
        Assert.True(m.Badges);
        Assert.Equal(1, m.PreviewLines);   // under twice the preview's height: one line
        Assert.True(m.When);

        Assert.Equal(3, Plan(Stacked, 320, 300, lines: 3).PreviewLines);
        Assert.Equal(5, Plan(Stacked, 320, 300, lines: 9).PreviewLines);
        Assert.False(Plan(Stacked, 200, 356).Badges);
        Assert.Equal(0, Plan(Stacked, 200, 106).PreviewLines);
        Assert.False(Plan(Stacked, 100, 300).When);
        Assert.True(Plan(Stacked, 200, 150).Compact);
        Assert.False(Plan(Stacked, 200, 356).Compact);
    }

    [Fact]
    public void S7033_inline_actions_only_on_a_huge_wide_line()
    {
        Assert.True(Plan(Stacked, 800, 516).InlineActions);
        Assert.False(Plan(Stacked, 480, 116).InlineActions);
        Assert.False(Plan(Stacked, 320, 196).InlineActions);
    }

    [Theory]
    [InlineData(720, 0, 3)]
    [InlineData(720, 96, 3)]
    [InlineData(1100, 120, 2)]
    [InlineData(1600, 0, 3)]
    public void S7033_revealing_the_inline_actions_moves_nothing_but_the_slot_content(double inner, double badge, int buttons)
    {
        var actions = KitLadder.InlineActionsWidth(buttons, button: 22, gap: 4);
        var boxes = KitLadder.LineBoxes(inner, 12, badge, actions);

        Assert.True(boxes.Slot >= KitLadder.LineTimeWidth);
        Assert.True(boxes.Slot >= actions, $"the actions ({actions}) overflow the slot ({boxes.Slot}) and push the line");

        var gaps = (badge > 0 ? 3 : 2) * 12;
        Assert.Equal(inner, boxes.Title + boxes.Line + boxes.Badge + boxes.Slot + gaps, 6);
        Assert.Equal(badge, boxes.Badge);
    }

    [Fact]
    public void S7033_a_row_without_actions_keeps_the_narrow_time_slot()
    {
        Assert.Equal(0, KitLadder.InlineActionsWidth(0, 22, 4));
        Assert.Equal(KitLadder.LineTimeWidth, KitLadder.LineBoxes(800, 12, 0, 0).Slot);
        Assert.Equal(74, KitLadder.InlineActionsWidth(3, 22, 4));
    }

    [Fact]
    public void S7034_a_wide_table_adds_the_preview_after_a_lone_subtitle()
    {
        KitColumn[] chosen = [KitColumn.Title, KitColumn.Subtitle, KitColumn.When];

        Assert.Equal(chosen, KitLadder.TableColumns(chosen, 600, 8).Select(c => c.Column));
        Assert.Equal(
            [KitColumn.Title, KitColumn.Subtitle, KitColumn.Preview, KitColumn.When],
            KitLadder.TableColumns(chosen, 760, 8).Select(c => c.Column));

        KitColumn[] own = [KitColumn.Preview, KitColumn.Title, KitColumn.Subtitle];
        Assert.Equal(own, KitLadder.TableColumns(own, 760, 8).Select(c => c.Column));
        KitColumn[] noSubtitle = [KitColumn.Title, KitColumn.When];
        Assert.Equal(noSubtitle, KitLadder.TableColumns(noSubtitle, 760, 8).Select(c => c.Column));
    }

    [Fact]
    public void S7034_a_narrow_table_keeps_the_most_essential_columns_in_the_users_order()
    {
        KitColumn[] chosen = [KitColumn.Subtitle, KitColumn.Badge, KitColumn.Title, KitColumn.When];

        // Title (80) + When (56) + Subtitle (96) and their gaps fit 260; the badge is shed.
        Assert.Equal([KitColumn.Subtitle, KitColumn.Title, KitColumn.When], KitLadder.TableColumns(chosen, 260, 8).Select(c => c.Column));
        Assert.Equal([KitColumn.Title], KitLadder.TableColumns(chosen, 40, 8).Select(c => c.Column));

        // With no flexible column the first one takes the slack.
        var fixedOnly = KitLadder.TableColumns([KitColumn.When, KitColumn.Title], 400, 8);
        Assert.Equal(1, fixedOnly[0].Star);
        Assert.Equal(0, fixedOnly[1].Star);
    }

    [Fact]
    public void S7034_the_column_titles_show_where_they_cost_no_row()
    {
        Assert.False(Plan(Table, 320, 120, columns: [KitColumn.Title]).ColumnHeader);
        Assert.True(Plan(Table, 320, 196, columns: [KitColumn.Title]).ColumnHeader);
        Assert.NotNull(Plan(Table, 320, 196).Table);
        Assert.Null(Plan(Stacked, 320, 196).Table);
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
