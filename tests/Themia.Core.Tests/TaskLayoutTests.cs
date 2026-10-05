using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-780]..[S-789] The Tasks widget's size ladder, fade, drag targeting, keys and summary.</summary>
public sealed class TaskLayoutTests
{
    private const double Row = 40;
    private const double CompactRow = 32;

    // The size tiers as content areas (card less the ~44 DIP header).
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static TaskLayoutPlan Plan(WidgetArea area, int count = 7, bool today = false, TaskViewMode mode = TaskViewMode.List, int lines = 2)
        => TaskLayout.Plan(area, new TaskLayoutInput(count, Writable: true, today, ShowDueDate: true, mode, lines), Row, CompactRow);

    [Fact]
    public void S780_XS_shows_one_line_titles_and_no_due_line_with_no_add_row()
    {
        var plan = Plan(XS);
        Assert.True(plan.CompactRows);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(DueDetail.None, plan.Due);
        Assert.Equal(DueDetail.Urgent, Plan(new WidgetArea(120, 356)).Due);
        Assert.False(plan.AddRow);
        Assert.False(plan.Summary);
        Assert.False(plan.PriorityGlyph);
    }

    [Fact]
    public void S780_S_puts_the_due_under_one_line_titles_and_drops_the_add_row()
    {
        var plan = Plan(S);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(DueDetail.Short, plan.Due);
        Assert.False(plan.DueBeside);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S780_a_wide_strip_puts_the_due_beside_even_in_list_layout()
    {
        var plan = Plan(Wide);
        Assert.True(plan.DueBeside);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(1, plan.Columns);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S780_M_has_compact_rows_and_the_add_row_and_follows_the_layout_setting()
    {
        Assert.True(Plan(M).AddRow);
        Assert.True(Plan(M).CompactRows);
        Assert.False(Plan(M).DueBeside);
        Assert.True(Plan(M, mode: TaskViewMode.Compact).DueBeside);
    }

    [Fact]
    public void S780_a_tall_column_stacks_standard_rows_with_the_due_under_the_title()
    {
        var plan = Plan(Tall, lines: 4);
        Assert.False(plan.CompactRows);
        Assert.Equal(4, plan.TitleLines);
        Assert.False(plan.DueBeside);
        Assert.True(plan.AddRow);
        Assert.False(plan.Summary);
        Assert.True(plan.CompletedHeading);
    }

    [Fact]
    public void S783_S789_a_panel_adds_the_summary_a_long_due_column_and_list_names_under_Today()
    {
        var plan = Plan(L, today: true);
        Assert.True(plan.Summary);
        Assert.Equal(DueDetail.Long, plan.Due);
        Assert.True(plan.DueBeside);
        Assert.True(plan.ListNames);
        Assert.Equal(1, plan.Columns);
        Assert.False(Plan(L).ListNames);
    }

    [Fact]
    public void S782_XL_goes_to_two_columns_only_when_one_would_not_hold_the_list()
    {
        Assert.Equal(1, Plan(XL, count: 7).Columns);
        Assert.Equal(2, Plan(XL, count: 14).Columns);
        Assert.Equal(1, Plan(L, count: 30).Columns);
        Assert.True(Plan(XL, count: 14).DueBeside);
    }

    [Fact]
    public void S780_an_empty_list_has_no_summary_and_an_unwritable_one_no_add_row()
    {
        Assert.False(Plan(L, count: 0).Summary);
        var readOnly = TaskLayout.Plan(L, new TaskLayoutInput(5, Writable: false, false, true, TaskViewMode.List, 2), Row, CompactRow);
        Assert.False(readOnly.AddRow);
    }

    [Fact]
    public void S780_an_unmeasured_area_starts_minimal()
    {
        var plan = Plan(default);
        Assert.True(plan.CompactRows);
        Assert.False(plan.AddRow);
        Assert.Equal(1, plan.Columns);
    }

    [Fact]
    public void S782_rows_fill_columns_top_to_bottom()
    {
        Assert.Equal(0, TaskLayout.LinesFor(0, 2));
        Assert.Equal(7, TaskLayout.LinesFor(7, 1));
        Assert.Equal(4, TaskLayout.LinesFor(7, 2));
        Assert.Equal(396, TaskLayout.ColumnWidth(800, 2));
    }

    [Fact]
    public void S781_the_viewport_ends_on_the_last_whole_line()
    {
        double[] bottoms = [44, 88, 120, 164];
        Assert.Equal(88, TaskLayout.WholeLineCap(bottoms, 106));
        Assert.Equal(120, TaskLayout.WholeLineCap(bottoms, 120.4));
        Assert.Equal(30, TaskLayout.WholeLineCap(bottoms, 30));
    }

    [Fact]
    public void S781_the_viewport_never_ends_on_a_section_heading()
    {
        double[] bottoms = [44, 88, 112, 156];
        bool[] headings = [false, false, true, false];
        Assert.Equal(88, TaskLayout.WholeLineCap(bottoms, 130, headings));
        Assert.Equal(156, TaskLayout.WholeLineCap(bottoms, 160, headings));
    }

    [Fact]
    public void S781_the_fade_only_dims_the_bottom_lines_and_only_with_more_below()
    {
        Assert.Equal(1, TaskLayout.FadeOpacity(80, 120, 120, moreBelow: false));
        Assert.Equal(1, TaskLayout.FadeOpacity(0, 40, 120, moreBelow: true));
        var last = TaskLayout.FadeOpacity(80, 120, 120, moreBelow: true);
        Assert.InRange(last, TaskLayout.FadeFloor, 0.7);
        Assert.Equal(TaskLayout.FadeFloor, TaskLayout.FadeOpacity(120, 140, 120, moreBelow: true));
    }

    [Fact]
    public void S786_a_drag_moves_into_a_slot_only_over_its_middle_half()
    {
        SlotBox[] slots = [new(0, 0, 200, 40), new(0, 40, 200, 80), new(0, 120, 200, 40)];

        Assert.Equal(0, TaskLayout.Retarget(slots, 0, 50, 45));
        Assert.Equal(1, TaskLayout.Retarget(slots, 0, 50, 80));
        Assert.Equal(2, TaskLayout.Retarget(slots, 0, 50, 140));
        Assert.Equal(0, TaskLayout.Retarget(slots, 0, 250, 80));
        Assert.Equal(0, TaskLayout.Retarget(slots, 0, 50, 80, slot => slot != 1));
    }

    [Fact]
    public void S786_a_swap_of_unequal_rows_does_not_bounce_back()
    {
        // A short row dragged up over a tall one: once they swap, the pointer is in its own slot.
        SlotBox[] before = [new(0, 0, 200, 80), new(0, 80, 200, 40)];
        Assert.Equal(0, TaskLayout.Retarget(before, 1, 50, 40));

        SlotBox[] after = [new(0, 0, 200, 40), new(0, 40, 200, 80)];
        Assert.Equal(0, TaskLayout.Retarget(after, 0, 50, 40));
    }

    [Fact]
    public void S786_a_press_is_a_drag_only_past_the_threshold()
    {
        Assert.False(TaskLayout.IsDrag(3, 3));
        Assert.True(TaskLayout.IsDrag(0, 5));
    }

    [Fact]
    public void S787_arrows_and_home_end_stop_at_the_ends()
    {
        Assert.Equal(new TaskKeyResult(TaskKeyCommand.Focus, 0), TaskLayout.Decide(WidgetKey.Up, 0, 5));
        Assert.Equal(new TaskKeyResult(TaskKeyCommand.Focus, 4), TaskLayout.Decide(WidgetKey.Down, 4, 5));
        Assert.Equal(new TaskKeyResult(TaskKeyCommand.Focus, 3), TaskLayout.Decide(WidgetKey.Down, 2, 5));
        Assert.Equal(new TaskKeyResult(TaskKeyCommand.Focus, 0), TaskLayout.Decide(WidgetKey.Home, 3, 5));
        Assert.Equal(new TaskKeyResult(TaskKeyCommand.Focus, 4), TaskLayout.Decide(WidgetKey.End, 1, 5));
    }

    [Fact]
    public void S787_enter_toggles_delete_deletes_f2_renames_escape_releases()
    {
        Assert.Equal(TaskKeyCommand.Toggle, TaskLayout.Decide(WidgetKey.Enter, 1, 3).Command);
        Assert.Equal(TaskKeyCommand.Delete, TaskLayout.Decide(WidgetKey.Delete, 1, 3).Command);
        Assert.Equal(TaskKeyCommand.Rename, TaskLayout.Decide(WidgetKey.Rename, 1, 3).Command);
        Assert.Equal(TaskKeyCommand.Release, TaskLayout.Decide(WidgetKey.Escape, 1, 3).Command);
        Assert.Equal(TaskKeyCommand.None, TaskLayout.Decide(WidgetKey.Enter, 0, 0).Command);
        Assert.Equal(TaskKeyCommand.Release, TaskLayout.Decide(WidgetKey.Escape, 0, 0).Command);
    }

    [Fact]
    public void S788_tab_opens_the_next_editor_and_stops_at_the_last_shift_tab_closes()
    {
        string[] ids = ["a", "b", "c"];
        Assert.Equal("b", TaskLayout.NextEditor(ids, "a", backwards: false));
        Assert.Null(TaskLayout.NextEditor(ids, "c", backwards: false));
        Assert.Null(TaskLayout.NextEditor(ids, "b", backwards: true));
        Assert.Null(TaskLayout.NextEditor(ids, "zz", backwards: false));
    }

    private static readonly DateOnly Today = new(2026, 10, 4);

    private static string Due(int days) => $"{TaskDue.Wire(Today.AddDays(days))}T00:00:00.0000000";

    [Fact]
    public void S783_the_summary_counts_open_overdue_due_today_and_done()
    {
        TaskItem[] rows =
        [
            new("1", "l", "a", false, false, Due(-2)),
            new("2", "l", "b", false, false, Due(0)),
            new("3", "l", "c", false),
            new("4", "l", "d", true, false, Due(-1)),
        ];

        Assert.Equal("3 open · 1 overdue · 1 due today · 1 done", TaskRules.Summary(Locale.En, rows, Today, TimeZoneInfo.Utc));
        Assert.Equal("Nothing open", TaskRules.Summary(Locale.En, [], Today, TimeZoneInfo.Utc));
        Assert.Equal("3 offen · 1 überfällig · 1 heute fällig · 1 erledigt", TaskRules.Summary(Locale.De, rows, Today, TimeZoneInfo.Utc));
    }

    [Fact]
    public void S784_completed_tasks_follow_the_open_ones_each_in_its_own_order()
    {
        TaskItem[] rows = [new("a", "l", "a", true), new("b", "l", "b", false), new("c", "l", "c", true), new("d", "l", "d", false)];
        Assert.Equal(["b", "d", "a", "c"], TaskRules.Sectioned(rows).Select(t => t.Id));

        TaskItem[] sorted = [new("a", "l", "a", false), new("b", "l", "b", true)];
        Assert.Same(sorted, TaskRules.Sectioned(sorted));
    }

    [Fact]
    public void S789_the_long_due_text_carries_the_date_where_it_says_something()
    {
        Assert.Equal("Overdue · Oct 2", TaskDue.WideText(Locale.En, new Due(Today.AddDays(-2), DueLabel.Overdue, -2)));
        Assert.Equal("Today", TaskDue.WideText(Locale.En, new Due(Today, DueLabel.Today, 0)));
        Assert.Equal("Thu, Oct 8", TaskDue.WideText(Locale.En, new Due(Today.AddDays(4), DueLabel.Weekday, 4)));
        Assert.Equal("Überfällig · 2. Okt.", TaskDue.WideText(Locale.De, new Due(Today.AddDays(-2), DueLabel.Overdue, -2)));
        Assert.Equal("Do., 8. Okt.", TaskDue.WideText(Locale.De, new Due(Today.AddDays(4), DueLabel.Weekday, 4)));
        Assert.True(TaskDue.IsUrgent(new Due(Today, DueLabel.Today, 0)));
        Assert.False(TaskDue.IsUrgent(new Due(Today.AddDays(1), DueLabel.Tomorrow, 1)));
    }
}
