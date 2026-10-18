using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-780]..[S-789] The Tasks widget's size ladder, fade, drag targeting and keys: the kit's check look ([S-7037], [S-7038]).</summary>
public sealed class TaskLayoutTests
{
    private static readonly KitSpec Check = new(KitRowLook.Check, KitShow.All, () => string.Empty, () => string.Empty, KitTime.None);

    private enum Detail
    {
        None,
        Urgent,
        Short,
        Long,
    }

    /// <summary>How much of a due date a row says: the kit plan's time rungs.</summary>
    private static Detail Due(KitPlan plan) => !plan.When ? Detail.None : plan.UrgentOnly ? Detail.Urgent : plan.LongWhen ? Detail.Long : Detail.Short;

    private static KitPlan Kit(WidgetArea area, int count, bool writable, bool today, TasksStyle mode, int lines)
        => KitLadder.Plan(area, Check, new KitChoices((today ? KitShow.Subtitle : KitShow.None) | KitShow.When) { Count = count, CanAdd = writable, TitleLines = lines, WhenBeside = mode == TasksStyle.Compact });

    // The size tiers as content areas (card less the ~44 DIP header).
    private static readonly WidgetArea XS = new(120, 76);
    private static readonly WidgetArea S = new(200, 106);
    private static readonly WidgetArea M = new(320, 196);
    private static readonly WidgetArea Wide = new(480, 116);
    private static readonly WidgetArea Tall = new(200, 356);
    private static readonly WidgetArea L = new(560, 376);
    private static readonly WidgetArea XL = new(800, 516);

    private static KitPlan Plan(WidgetArea area, int count = 7, bool today = false, TasksStyle mode = TasksStyle.List, int lines = 2)
        => Kit(area, count, writable: true, today, mode, lines);

    [Fact]
    public void S780_XS_shows_one_line_titles_and_no_due_line_with_no_add_row()
    {
        var plan = Plan(XS);
        Assert.True(plan.Compact);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(Detail.None, Due(plan));
        Assert.Equal(Detail.Urgent, Due(Plan(new WidgetArea(120, 356))));
        Assert.False(plan.AddRow);
        Assert.False(plan.Mark);
    }

    [Fact]
    public void S780_S_puts_the_due_under_one_line_titles_and_drops_the_add_row()
    {
        var plan = Plan(S);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(Detail.Short, Due(plan));
        Assert.False(plan.WhenBeside);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S780_a_wide_strip_puts_the_due_beside_even_in_list_layout()
    {
        var plan = Plan(Wide);
        Assert.True(plan.WhenBeside);
        Assert.Equal(1, plan.TitleLines);
        Assert.Equal(1, plan.Columns);
        Assert.False(plan.AddRow);
    }

    [Fact]
    public void S780_M_has_compact_rows_and_the_add_row_and_follows_the_layout_setting()
    {
        Assert.True(Plan(M).AddRow);
        Assert.True(Plan(M).Compact);
        Assert.False(Plan(M).WhenBeside);
        Assert.True(Plan(M, mode: TasksStyle.Compact).WhenBeside);
    }

    [Fact]
    public void S780_a_tall_column_stacks_standard_rows_with_the_due_under_the_title()
    {
        var plan = Plan(Tall, lines: 4);
        Assert.False(plan.Compact);
        Assert.Equal(4, plan.TitleLines);
        Assert.False(plan.WhenBeside);
        Assert.True(plan.AddRow);
        Assert.True(plan.Headings);
    }

    [Fact]
    public void S789_a_panel_adds_a_long_due_column_and_list_names_under_Today()
    {
        var plan = Plan(L, today: true);
        Assert.Equal(Detail.Long, Due(plan));
        Assert.True(plan.WhenBeside);
        Assert.True(plan.Subtitle);
        Assert.Equal(1, plan.Columns);
        Assert.False(Plan(L).Subtitle);
    }

    [Fact]
    public void S782_XL_goes_to_two_columns_only_when_one_would_not_hold_the_list()
    {
        Assert.Equal(1, Plan(XL, count: 7).Columns);
        Assert.Equal(2, Plan(XL, count: 14).Columns);
        Assert.Equal(1, Plan(L, count: 30).Columns);
        Assert.True(Plan(XL, count: 14).WhenBeside);
    }

    [Fact]
    public void S780_an_unwritable_list_has_no_add_row()
    {
        var readOnly = Kit(L, 5, writable: false, false, TasksStyle.List, 2);
        Assert.False(readOnly.AddRow);
    }

    [Fact]
    public void S3850_an_empty_list_has_its_add_row_even_at_XS()
    {
        Assert.True(Plan(XS, count: 0).AddRow);
        Assert.True(Plan(Wide, count: 0).AddRow);
        var readOnly = Kit(XS, 0, writable: false, false, TasksStyle.List, 2);
        Assert.False(readOnly.AddRow);
    }

    [Fact]
    public void S780_an_unmeasured_area_starts_minimal()
    {
        var plan = Plan(default);
        Assert.True(plan.Compact);
        Assert.False(plan.AddRow);
        Assert.Equal(1, plan.Columns);
    }

    [Fact]
    public void S782_rows_fill_columns_top_to_bottom()
    {
        Assert.Equal(0, KitChecks.LinesFor(0, 2));
        Assert.Equal(7, KitChecks.LinesFor(7, 1));
        Assert.Equal(4, KitChecks.LinesFor(7, 2));
        Assert.Equal(396, KitChecks.ColumnWidth(800, 2));
    }

    [Fact]
    public void S781_the_viewport_ends_on_the_last_whole_line()
    {
        double[] bottoms = [44, 88, 120, 164];
        Assert.Equal(88, KitChecks.WholeLineCap(bottoms, 106));
        Assert.Equal(120, KitChecks.WholeLineCap(bottoms, 120.4));
        Assert.Equal(30, KitChecks.WholeLineCap(bottoms, 30));
    }

    [Fact]
    public void S781_the_viewport_never_ends_on_a_section_heading()
    {
        double[] bottoms = [44, 88, 112, 156];
        bool[] headings = [false, false, true, false];
        Assert.Equal(88, KitChecks.WholeLineCap(bottoms, 130, headings));
        Assert.Equal(156, KitChecks.WholeLineCap(bottoms, 160, headings));
    }

    [Fact]
    public void S781_the_fade_only_dims_the_bottom_lines_and_only_with_more_below()
    {
        Assert.Equal(1, ListFade.Opacity(80, 120, 120, moreBelow: false));
        Assert.Equal(1, ListFade.Opacity(0, 40, 120, moreBelow: true));
        var last = ListFade.Opacity(80, 120, 120, moreBelow: true);
        Assert.InRange(last, ListFade.Floor, 0.7);
        Assert.Equal(ListFade.Floor, ListFade.Opacity(120, 140, 120, moreBelow: true));
    }

    [Fact]
    public void S786_a_drag_moves_into_a_slot_only_over_its_middle_half()
    {
        KitSlot[] slots = [new(0, 0, 200, 40), new(0, 40, 200, 80), new(0, 120, 200, 40)];

        Assert.Equal(0, KitChecks.Retarget(slots, 0, 50, 45));
        Assert.Equal(1, KitChecks.Retarget(slots, 0, 50, 80));
        Assert.Equal(2, KitChecks.Retarget(slots, 0, 50, 140));
        Assert.Equal(0, KitChecks.Retarget(slots, 0, 250, 80));
        Assert.Equal(0, KitChecks.Retarget(slots, 0, 50, 80, slot => slot != 1));
    }

    [Fact]
    public void S786_a_swap_of_unequal_rows_does_not_bounce_back()
    {
        // A short row dragged up over a tall one: once they swap, the pointer is in its own slot.
        KitSlot[] before = [new(0, 0, 200, 80), new(0, 80, 200, 40)];
        Assert.Equal(0, KitChecks.Retarget(before, 1, 50, 40));

        KitSlot[] after = [new(0, 0, 200, 40), new(0, 40, 200, 80)];
        Assert.Equal(0, KitChecks.Retarget(after, 0, 50, 40));
    }

    [Fact]
    public void S786_a_press_is_a_drag_only_past_the_threshold()
    {
        Assert.False(KitChecks.IsDrag(3, 3));
        Assert.True(KitChecks.IsDrag(0, 5));
    }

    [Fact]
    public void S787_arrows_and_home_end_stop_at_the_ends()
    {
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.Up, 0, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 4), KitKeys.Decide(WidgetKey.Down, 4, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 3), KitKeys.Decide(WidgetKey.Down, 2, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 0), KitKeys.Decide(WidgetKey.Home, 3, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, 4), KitKeys.Decide(WidgetKey.End, 1, 5));
    }

    [Fact]
    public void S787_enter_opens_space_ticks_delete_deletes_f2_renames_escape_releases()
    {
        // [S-616] The kit's one key map: Enter runs the primary action (a task's: rename), Space ticks.
        Assert.Equal(KitKeyCommand.Open, KitKeys.Decide(WidgetKey.Enter, 1, 3).Command);
        Assert.Equal(KitKeyCommand.Toggle, KitKeys.Decide(WidgetKey.Space, 1, 3).Command);
        Assert.Equal(KitKeyCommand.Delete, KitKeys.Decide(WidgetKey.Delete, 1, 3).Command);
        Assert.Equal(KitKeyCommand.Rename, KitKeys.Decide(WidgetKey.Rename, 1, 3).Command);
        Assert.Equal(KitKeyCommand.Release, KitKeys.Decide(WidgetKey.Escape, 1, 3).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.Enter, 0, 0).Command);
        Assert.Equal(KitKeyCommand.Release, KitKeys.Decide(WidgetKey.Escape, 0, 0).Command);
    }

    [Fact]
    public void S788_tab_opens_the_next_editor_and_stops_at_the_last_shift_tab_closes()
    {
        string[] ids = ["a", "b", "c"];
        Assert.Equal("b", KitChecks.NextEditor(ids, "a", backwards: false));
        Assert.Null(KitChecks.NextEditor(ids, "c", backwards: false));
        Assert.Null(KitChecks.NextEditor(ids, "b", backwards: true));
        Assert.Null(KitChecks.NextEditor(ids, "zz", backwards: false));
    }

    private static readonly DateOnly Today = new(2026, 10, 4);

    private static string Due(int days) => $"{TaskDue.Wire(Today.AddDays(days))}T00:00:00.0000000";

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
