using Themia.Core.Localization;
using Themia.Core.Tasks;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Due dates, Today, row order and title lines for the Tasks widget, ported from the classic app's
/// <c>dueDate.test.ts</c>, <c>todayList.test.ts</c>, <c>taskOrder.test.ts</c> and
/// <c>titleLines.test.ts</c>. The viewer's zone is passed in, so "a viewer in Bangkok" is a fixed
/// offset rather than an environment variable.
/// </summary>
public sealed class TaskRulesTests
{
    private static readonly TimeZoneInfo Bangkok = TimeZoneInfo.CreateCustomTimeZone("Test/Bangkok", TimeSpan.FromHours(7), "UTC+7", "UTC+7");
    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.CreateCustomTimeZone("Test/NewYork", TimeSpan.FromHours(-4), "UTC-4", "UTC-4");

    private static readonly DateOnly Friday = new(2026, 8, 7);
    private static readonly DateOnly Monday = new(2026, 8, 10);

    private static TaskItem Task(string id, string? due, bool done = false) => new(id, "L1", id, done, DueRaw: due);

    // ---- [S-593] which day a due value means ------------------------------------------------------

    [Fact]
    public void S593_a_converted_local_midnight_resolves_back_to_the_intended_day()
    {
        // The To Do mobile app writes a UTC+7 user's "due the 10th" as 17:00 UTC on the 9th.
        Assert.Equal(new DateOnly(2026, 8, 10), TaskDue.Day("2026-08-09T17:00:00.0000000", Bangkok));
        Assert.Equal(DayOfWeek.Monday, TaskDue.Day("2026-08-09T17:00:00.0000000", Bangkok)!.Value.DayOfWeek);
    }

    [Theory]
    [InlineData("2026-08-11T15:00:00.0000000")] // Tokyo's midnight of the 12th, converted to UTC by To Do
    [InlineData("2026-08-12T00:00:00.0000000")] // the zoneless midnight Graph also writes
    [InlineData("2026-08-12T00:00:00.000Z")] // Google's UTC midnight
    public void S593_issue_109_due_the_12th_seen_on_the_10th_in_Tokyo_is_not_tomorrow(string raw)
    {
        var tokyo = TimeZoneInfo.CreateCustomTimeZone("Test/Tokyo", TimeSpan.FromHours(9), "UTC+9", "UTC+9");
        var due = TaskDue.Resolve(raw, new DateOnly(2026, 8, 10), tokyo)!.Value;

        Assert.Equal(new DateOnly(2026, 8, 12), due.Day);
        Assert.Equal(DueLabel.Weekday, due.Label);
    }

    [Fact]
    public void S593_a_task_due_today_is_today_not_overdue()
    {
        var due = "2026-08-06T17:00:00.0000000"; // Friday's midnight in UTC+7, as Graph returns it
        Assert.Equal(DueLabel.Today, TaskDue.Resolve(due, Friday, Bangkok)!.Value.Label);
        Assert.False(TaskDue.IsPast(due, Friday, Bangkok));
    }

    [Theory]
    [InlineData("2026-08-10T00:00:00.0000000")] // Graph zoneless midnight
    [InlineData("2026-08-10T00:00:00.000Z")] // Google UTC midnight
    [InlineData("2026-08-10")] // a bare date
    [InlineData("2026-08-10T00:00:00+07:00")] // midnight at an explicit offset is that offset's day
    public void S593_midnight_of_the_intended_day_keeps_its_day_either_side_of_UTC(string raw)
    {
        Assert.Equal(new DateOnly(2026, 8, 10), TaskDue.Day(raw, NewYork));
        Assert.Equal(new DateOnly(2026, 8, 10), TaskDue.Day(raw, Bangkok));
    }

    [Fact]
    public void S593_a_real_timestamp_resolves_to_the_viewers_local_day()
    {
        Assert.Equal(new DateOnly(2026, 8, 10), TaskDue.Day("2026-08-09T20:00:00.000Z", Bangkok));
        Assert.Equal(new DateOnly(2026, 8, 9), TaskDue.Day("2026-08-09T20:00:00.000Z", NewYork));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01T00:00:00Z")]
    public void S593_absent_or_unparseable_values_have_no_day(string? raw)
    {
        Assert.Null(TaskDue.Day(raw, NewYork));
        Assert.Null(TaskDue.Resolve(raw, Friday, NewYork));
        Assert.False(TaskDue.IsPast(raw, Friday, NewYork));
    }

    // ---- [S-594] labels --------------------------------------------------------------------------

    [Theory]
    [InlineData("2026-08-06", DueLabel.Overdue)]
    [InlineData("2026-08-07", DueLabel.Today)]
    [InlineData("2026-08-08", DueLabel.Tomorrow)]
    [InlineData("2026-08-09", DueLabel.Weekday)]
    [InlineData("2026-08-13", DueLabel.Weekday)] // +6, the last day inside the week
    [InlineData("2026-08-14", DueLabel.Date)] // +7 switches to the date
    public void S594_labels_each_day_offset(string raw, DueLabel expected)
        => Assert.Equal(expected, TaskDue.Resolve(raw, Friday, NewYork)!.Value.Label);

    [Fact]
    public void S594_only_days_before_today_are_past()
    {
        Assert.True(TaskDue.IsPast("2026-08-06", Friday, NewYork));
        Assert.False(TaskDue.IsPast("2026-08-07", Friday, NewYork));
        Assert.False(TaskDue.IsPast("2026-08-08", Friday, NewYork));
    }

    [Fact]
    public void S594_day_counts_survive_a_DST_boundary()
    {
        var zurich = TimeZoneInfo.CreateCustomTimeZone("Test/Zurich", TimeSpan.FromHours(1), "CET", "CET");
        var saturday = new DateOnly(2026, 10, 24); // clocks go back on the 25th
        Assert.Equal(1, TaskDue.Resolve("2026-10-25", saturday, zurich)!.Value.Days);
        Assert.Equal(2, TaskDue.Resolve("2026-10-26", saturday, zurich)!.Value.Days);
    }

    [Fact]
    public void S594_says_overdue_in_words_and_the_far_future_as_a_date()
    {
        Assert.Equal("Overdue", TaskDue.Text(Locale.En, TaskDue.Resolve("2026-08-01", Friday, NewYork)!.Value));
        Assert.Equal("Today", TaskDue.Text(Locale.En, TaskDue.Resolve("2026-08-07", Friday, NewYork)!.Value));
        Assert.Equal("Tomorrow", TaskDue.Text(Locale.En, TaskDue.Resolve("2026-08-08", Friday, NewYork)!.Value));
        Assert.Equal("Mon", TaskDue.Text(Locale.En, TaskDue.Resolve("2026-08-10", Friday, NewYork)!.Value));
        Assert.Equal("Aug 21", TaskDue.Text(Locale.En, TaskDue.Resolve("2026-08-21", Friday, NewYork)!.Value));
        Assert.Equal("Überfällig", TaskDue.Text(Locale.De, TaskDue.Resolve("2026-08-01", Friday, NewYork)!.Value));
        Assert.Equal("Mo.", TaskDue.Text(Locale.De, TaskDue.Resolve("2026-08-10", Friday, NewYork)!.Value));
        Assert.Equal("21. Aug.", TaskDue.Text(Locale.De, TaskDue.Resolve("2026-08-21", Friday, NewYork)!.Value));
    }

    // ---- [S-596] Today ---------------------------------------------------------------------------

    [Fact]
    public void S596_recognises_the_virtual_id_and_nothing_else()
    {
        Assert.True(TaskRules.IsToday(TaskRules.TodayListId));
        Assert.False(TaskRules.IsToday("AQMkAGZjNDg9PQAAAA=="));
        Assert.False(TaskRules.IsToday(string.Empty));
        Assert.False(TaskRules.IsToday(null));
    }

    [Fact]
    public void S596_keeps_tasks_due_today_and_drops_later_ones()
    {
        var got = TaskRules.Today(
            [Task("today", "2026-08-10T00:00:00.0000000"), Task("tomorrow", "2026-08-11T00:00:00.0000000"), Task("next-week", "2026-08-20")],
            Monday,
            NewYork);

        Assert.Equal(["today"], got.Select(t => t.Id));
    }

    [Fact]
    public void S596_keeps_overdue_tasks_oldest_first_ahead_of_today()
    {
        var got = TaskRules.Today(
            [Task("today", "2026-08-10"), Task("yesterday", "2026-08-09"), Task("last-week", "2026-08-03")],
            Monday,
            NewYork);

        Assert.Equal(["last-week", "yesterday", "today"], got.Select(t => t.Id));
    }

    [Fact]
    public void S596_drops_undated_tasks()
    {
        var got = TaskRules.Today([Task("undated", null), Task("blank", string.Empty), Task("today", "2026-08-10")], Monday, NewYork);
        Assert.Equal(["today"], got.Select(t => t.Id));
    }

    [Fact]
    public void S596_keeps_the_provider_order_within_one_day()
    {
        var got = TaskRules.Today([Task("a", "2026-08-10"), Task("b", "2026-08-10"), Task("c", "2026-08-10")], Monday, NewYork);
        Assert.Equal(["a", "b", "c"], got.Select(t => t.Id));
    }

    [Fact]
    public void S596_resolves_the_mobile_encoding_to_the_viewers_day()
    {
        var got = TaskRules.Today([Task("mobile", "2026-08-09T17:00:00.0000000")], Monday, Bangkok);
        Assert.Equal(["mobile"], got.Select(t => t.Id));
    }

    [Fact]
    public void S596_a_task_created_from_Today_round_trips_into_Today()
    {
        var created = TaskWire.CreateBody(Themia.Core.Accounts.HostProviders.Microsoft, "new", Monday);
        Assert.Contains("\"dateTime\":\"2026-08-10T00:00:00.0000000\"", created, StringComparison.Ordinal);
        Assert.Equal(["new"], TaskRules.Today([Task("new", $"{TaskDue.Wire(Monday)}T00:00:00.0000000")], Monday, NewYork).Select(t => t.Id));
    }

    [Fact]
    public void S596_a_task_from_Today_lands_in_the_default_list_else_the_first()
    {
        Assert.Equal("B", TaskRules.CreateTarget([new("A", "One", false), new("B", "Tasks", true)])!.Id);
        Assert.Equal("A", TaskRules.CreateTarget([new("A", "One", false), new("B", "Two", false)])!.Id);
        Assert.Null(TaskRules.CreateTarget([]));
    }

    // ---- [S-598] order ---------------------------------------------------------------------------

    private static IReadOnlyList<string> Ordered(IReadOnlyList<string> rows, IReadOnlyList<string> order)
        => TaskRules.ApplyOrder(rows, order, r => r);

    [Fact]
    public void S598_an_untouched_order_leaves_the_provider_order_alone()
    {
        IReadOnlyList<string> rows = ["a", "b"];
        Assert.Same(rows, Ordered(rows, []));
    }

    [Fact]
    public void S598_arranges_known_ids_first_in_the_stored_order()
        => Assert.Equal(["c", "a", "b"], Ordered(["a", "b", "c"], ["c", "a", "b"]));

    [Fact]
    public void S599_an_id_the_order_has_never_seen_lands_at_the_bottom()
        => Assert.Equal(["a", "b", "fresh"], Ordered(["fresh", "a", "b"], ["a", "b"]));

    [Fact]
    public void S598_skips_ids_that_no_longer_exist_and_cannot_duplicate_a_row()
    {
        Assert.Equal(["b", "a"], Ordered(["a", "b"], ["gone", "b", "a"]));
        Assert.Equal(["a", "b"], Ordered(["a", "b"], ["a", "a", "b"]));
    }

    [Fact]
    public void S598_moves_an_id_into_the_target_slot_either_way()
    {
        Assert.Equal(["b", "c", "a", "d"], TaskRules.MoveId(["a", "b", "c", "d"], "a", "c"));
        Assert.Equal(["c", "a", "b"], TaskRules.MoveId(["a", "b", "c"], "c", "a"));
    }

    [Fact]
    public void S598_a_move_that_comes_home_returns_the_same_list()
    {
        IReadOnlyList<string> ids = ["a", "b"];
        Assert.Same(ids, TaskRules.MoveId(ids, "a", "a"));
        Assert.Same(ids, TaskRules.MoveId(ids, "ghost", "b"));
        Assert.Same(ids, TaskRules.MoveId(ids, "a", "ghost"));
    }

    [Fact]
    public void S598_nudging_moves_one_step_and_stops_at_the_ends()
    {
        IReadOnlyList<string> ids = ["a", "b", "c"];
        Assert.Equal(["b", "a", "c"], TaskRules.Nudge(ids, "b", -1));
        Assert.Equal(["a", "c", "b"], TaskRules.Nudge(ids, "b", +1));
        Assert.Same(ids, TaskRules.Nudge(ids, "a", -1));
        Assert.Same(ids, TaskRules.Nudge(ids, "c", +1));
        Assert.Same(ids, TaskRules.Nudge(ids, "ghost", +1));
    }

    [Fact]
    public void S598_same_order_is_true_only_for_the_same_ids_in_the_same_order()
    {
        Assert.True(TaskRules.SameOrder(["a", "b"], ["a", "b"]));
        Assert.False(TaskRules.SameOrder(["a", "b"], ["b", "a"]));
        Assert.False(TaskRules.SameOrder(["a"], ["a", "b"]));
    }

    [Fact]
    public void S598_a_stored_order_drops_blank_and_repeated_entries()
        => Assert.Equal(["a", "b"], TaskRules.CleanOrder(["a", null, string.Empty, "b", "a"]));

    // ---- [S-601] title lines, [S-595] visibility ---------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(6, 6)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 6)]
    public void S601_title_lines_are_clamped_rather_than_rejected(int raw, int expected)
        => Assert.Equal(expected, TaskRules.ClampTitleLines(raw));

    [Fact]
    public void S595_hides_completed_tasks_unless_asked_keeping_order()
    {
        IReadOnlyList<TaskItem> tasks = [Task("a", null), Task("b", null, done: true), Task("c", null)];
        Assert.Equal(["a", "c"], TaskRules.Visible(tasks, new TaskFilter(false), Monday, NewYork).Select(t => t.Id));
        Assert.Same(tasks, TaskRules.Visible(tasks, new TaskFilter(true), Monday, NewYork));
        Assert.Equal(2, TaskRules.OpenCount(tasks));
    }

    // ---- [S-610] new tasks at the top --------------------------------------------------------------

    [Fact]
    public void S610_tasks_the_order_has_not_seen_lead_when_new_tasks_go_to_the_top()
    {
        IReadOnlyList<string> rows = ["a", "phone", "b", "new"];
        Assert.Equal(["phone", "new", "b", "a"], TaskRules.ApplyOrder(rows, ["b", "a"], r => r, unknownFirst: true));
        Assert.Equal(["b", "a", "phone", "new"], TaskRules.ApplyOrder(rows, ["b", "a"], r => r));
    }

    [Fact]
    public void S610_an_added_task_is_pinned_first_or_last_and_never_twice()
    {
        Assert.Equal(["n", "a", "b"], TaskRules.PlaceAdded(["a", "n", "b"], "n", onTop: true));
        Assert.Equal(["a", "b", "n"], TaskRules.PlaceAdded(["a", "n", "b"], "n", onTop: false));
        Assert.Equal(["n"], TaskRules.PlaceAdded([], "n", onTop: true));
    }

    [Fact]
    public void S610_a_task_on_this_pc_is_added_at_the_top_when_asked()
    {
        IReadOnlyList<LocalTask> tasks = [new("a", "A", false)];
        Assert.Equal(["n", "a"], LocalTaskLists.AddTask(tasks, " New ", "n", onTop: true).Select(t => t.Id));
        Assert.Equal(["a", "n"], LocalTaskLists.AddTask(tasks, "New", "n").Select(t => t.Id));
        Assert.Same(tasks, LocalTaskLists.AddTask(tasks, "  ", "n", onTop: true));
    }

    // ---- [S-611] filters ---------------------------------------------------------------------------

    [Theory]
    [InlineData(TaskDueFilter.Any, "past,today,thu,next,undated,donepast")]
    [InlineData(TaskDueFilter.Overdue, "past")]
    [InlineData(TaskDueFilter.Today, "today")]
    [InlineData(TaskDueFilter.ThisWeek, "today,thu")]
    [InlineData(TaskDueFilter.NoDate, "undated")]
    public void S611_the_due_filter_keeps_the_tasks_due_then(TaskDueFilter due, string expected)
    {
        // Monday 10 Aug 2026, weeks starting on Monday: Thursday is this week, the next Monday is not.
        IReadOnlyList<TaskItem> tasks =
        [
            Task("past", "2026-08-07"),
            Task("today", "2026-08-10"),
            Task("thu", "2026-08-13"),
            Task("next", "2026-08-17"),
            Task("undated", null),
            Task("donepast", "2026-08-07", done: true),
        ];

        var got = TaskRules.Visible(tasks, new TaskFilter(true, due), Monday, NewYork, Model.WeekStartDay.Monday);
        Assert.Equal(expected.Split(','), got.Select(t => t.Id));
    }

    [Fact]
    public void S611_this_week_follows_the_regional_week_start()
    {
        // Holding Monday 10 Aug: a Sunday week runs 9..15 Aug, a Saturday week 8..14, a Monday week 10..16.
        IReadOnlyList<TaskItem> tasks = [Task("sun", "2026-08-09"), Task("sat", "2026-08-15")];
        Assert.Equal(["sun", "sat"], TaskRules.Visible(tasks, new TaskFilter(false, TaskDueFilter.ThisWeek), Monday, NewYork, Model.WeekStartDay.Sunday).Select(t => t.Id));
        Assert.Equal(["sun"], TaskRules.Visible(tasks, new TaskFilter(false, TaskDueFilter.ThisWeek), Monday, NewYork, Model.WeekStartDay.Saturday).Select(t => t.Id));
        Assert.Equal(["sat"], TaskRules.Visible(tasks, new TaskFilter(false, TaskDueFilter.ThisWeek), Monday, NewYork, Model.WeekStartDay.Monday).Select(t => t.Id));
    }

    [Fact]
    public void S611_important_only_and_words_in_any_case()
    {
        IReadOnlyList<TaskItem> tasks =
        [
            new("a", "L1", "Call the Dentist", false, Important: true),
            new("b", "L1", "dentist bill", false),
            new("c", "L1", "Water plants", false, Important: true),
        ];

        Assert.Equal(["a", "c"], TaskRules.Visible(tasks, new TaskFilter(false, ImportantOnly: true), Monday, NewYork).Select(t => t.Id));
        Assert.Equal(["a", "b"], TaskRules.Visible(tasks, new TaskFilter(false, Text: "  DENTIST "), Monday, NewYork).Select(t => t.Id));
        Assert.Equal(["a"], TaskRules.Visible(tasks, new TaskFilter(false, ImportantOnly: true, Text: "dentist"), Monday, NewYork).Select(t => t.Id));
        Assert.Same(tasks, TaskRules.Visible(tasks, new TaskFilter(false, Text: "   "), Monday, NewYork));
    }

    [Fact]
    public void S611_only_a_real_filter_narrows_the_list()
    {
        Assert.False(new TaskFilter(true, Text: " ").Narrows);
        Assert.True(new TaskFilter(false, TaskDueFilter.NoDate).Narrows);
        Assert.True(new TaskFilter(false, ImportantOnly: true).Narrows);
        Assert.True(new TaskFilter(false, Text: "x").Narrows);
    }

    [Fact]
    public void S611_moving_a_row_in_a_filtered_list_keeps_the_hidden_rows_in_their_slots()
    {
        // b and d are hidden by a filter; the user drags c above a.
        Assert.Equal(["c", "b", "a", "d", "e"], TaskRules.MergeOrder(["a", "b", "c", "d", "e"], ["c", "a", "e"]));
        Assert.Equal(["a", "b"], TaskRules.MergeOrder(["a", "b"], ["gone", "a"]));
    }

    // ---- [S-612] Today: finished today ---------------------------------------------------------------

    [Fact]
    public void S612_today_with_show_completed_keeps_only_tasks_finished_today()
    {
        IReadOnlyList<TaskItem> tasks =
        [
            Task("open", "2026-08-07"),
            Task("done-today", "2026-08-07", done: true) with { CompletedOn = TaskStamp.Parse("2026-08-10T13:00:00Z") },
            Task("done-friday", "2026-08-07", done: true) with { CompletedOn = TaskStamp.Parse("2026-08-07T13:00:00Z") },
            Task("done-undated", "2026-08-07", done: true),

            // 02:00 UTC on the 11th is still the 10th in New York.
            Task("done-late", "2026-08-10", done: true) with { CompletedOn = TaskStamp.Parse("2026-08-11T02:00:00Z") },

            // To Do's day-only completion is that day for every viewer.
            Task("done-day", "2026-08-10", done: true) with { CompletedOn = TaskStamp.Parse("2026-08-10T00:00:00.0000000") },
        ];

        var today = TaskRules.Visible(tasks, new TaskFilter(true, FinishedTodayOnly: true), Monday, NewYork);
        Assert.Equal(["open", "done-today", "done-late", "done-day"], today.Select(t => t.Id));

        // A list (not Today) shows every finished task.
        Assert.Same(tasks, TaskRules.Visible(tasks, new TaskFilter(true), Monday, NewYork));
    }

    [Theory]
    [InlineData("2026-08-10T00:00:00.0000000", "2026-08-10")]
    [InlineData("2026-08-10", "2026-08-10")]
    [InlineData("2026-08-10T18:30:00Z", "2026-08-10T18:30:00.0000000Z")]
    [InlineData("2026-08-10T18:30:00.5+02:00", "2026-08-10T16:30:00.5000000Z")]
    public void S612_a_stamp_is_a_day_or_an_instant_and_round_trips_through_its_wire_form(string raw, string wire)
    {
        var stamp = TaskStamp.Parse(raw)!.Value;
        Assert.Equal(wire, stamp.Wire());
        Assert.Equal(stamp, TaskStamp.Parse(stamp.Wire()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("2026-02-30")]
    [InlineData("2026-13-01T10:00:00Z")]
    public void S612_a_value_that_is_not_a_date_is_no_stamp(string? raw) => Assert.Null(TaskStamp.Parse(raw));

    // ---- [S-614] renaming a list ---------------------------------------------------------------------

    [Fact]
    public void S614_neither_Today_nor_a_built_in_list_is_renamed_and_a_blank_or_same_name_writes_nothing()
    {
        Assert.True(TaskRules.CanRename(new TaskListInfo("a", "Work", false)));
        Assert.False(TaskRules.CanRename(new TaskListInfo("t", "Tasks", true, BuiltIn: true)));
        Assert.False(TaskRules.CanRename(new TaskListInfo(TaskRules.TodayListId, "Today", false)));
        Assert.False(TaskRules.CanRename(null));

        Assert.Equal("Home", TaskRules.RenameTo("Work", "  Home "));
        Assert.Null(TaskRules.RenameTo("Work", "   "));
        Assert.Null(TaskRules.RenameTo("Work", " Work "));
    }
}
