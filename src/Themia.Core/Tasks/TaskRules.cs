using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.Core.Tasks;

/// <summary>
/// The list rules of the Tasks widget: the virtual Today list, the user's own row order, and how
/// many lines a title may wrap to. Pure, so each is tested against a fixed clock and fixed input.
/// </summary>
public static class TaskRules
{
    /// <summary>
    /// [S-596] The list id standing for the cross-list Today view. Not a possible Graph or Google id
    /// (both are opaque base64-ish strings) and kept identical to the classic app's, so a config
    /// carried over still means Today.
    /// </summary>
    public const string TodayListId = "__today__";

    /// <summary>[S-596] Bound on the Today fan-out: one request per list, so an absurd number of lists cannot turn one refresh into hundreds.</summary>
    public const int TodayMaxLists = 50;

    /// <summary>[S-601] Title lines: two by default, one to six.</summary>
    public const int TitleLinesMin = 1;

    public const int TitleLinesMax = 6;

    public const int TitleLinesDefault = 2;

    public static bool IsToday(string? listId) => listId == TodayListId;

    /// <summary>
    /// [S-596] Today: every task due today or already overdue, oldest first; undated tasks are not in
    /// it (Today is a schedule, not a copy of every task). Days come from <see cref="TaskDue"/>, so
    /// both providers' encodings land on the viewer's day. Stable: tasks sharing a day keep the order
    /// the provider gave them.
    /// </summary>
    public static IReadOnlyList<TaskItem> Today(IEnumerable<TaskItem> tasks, DateOnly today, TimeZoneInfo viewer)
    {
        ArgumentNullException.ThrowIfNull(tasks);

        return tasks
            .Select(task => (Task: task, Due: TaskDue.Resolve(task.DueRaw, today, viewer)))
            .Where(row => row.Due is { Days: <= 0 })
            .OrderBy(row => row.Due!.Value.Days) // OrderBy is stable
            .Select(row => row.Task)
            .ToList();
    }

    /// <summary>
    /// [S-598] Arrange rows by the user's order: ids the order names (and that are present) in that
    /// order, and everything else in the provider's order after them — or, with
    /// <paramref name="unknownFirst"/> ([S-610], "new tasks go to the top"), before them. An empty
    /// order returns the input untouched, so an untouched widget keeps the provider's own sorting
    /// (and Today's oldest-first). A deleted id leaves no hole, a corrupt duplicate cannot double a
    /// row, and an id the order has never seen — a task just created, here or on another device —
    /// lands at the bottom ([S-599]) or the top ([S-610]).
    /// </summary>
    public static IReadOnlyList<T> ApplyOrder<T>(IReadOnlyList<T> rows, IReadOnlyList<string> order, Func<T, string> id, bool unknownFirst = false)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(id);

        if (order.Count == 0)
        {
            return rows;
        }

        var byId = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            byId.TryAdd(id(row), row);
        }

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var known = new List<T>(rows.Count);

        foreach (var wanted in order)
        {
            if (byId.TryGetValue(wanted, out var row) && taken.Add(wanted))
            {
                known.Add(row);
            }
        }

        var unknown = new List<T>();
        foreach (var row in rows)
        {
            if (taken.Add(id(row)))
            {
                unknown.Add(row);
            }
        }

        return unknownFirst ? [.. unknown, .. known] : [.. known, .. unknown];
    }

    /// <summary>
    /// [S-599] [S-610] The order to pin once a task was added: every id in <paramref name="ids"/> (the
    /// whole list as arranged, hidden rows included) with <paramref name="added"/> at the top or the
    /// bottom. Pinned so the next refresh cannot re-sort it into the provider's own order.
    /// </summary>
    public static IReadOnlyList<string> PlaceAdded(IReadOnlyList<string> ids, string added, bool onTop)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(added);

        var rest = ids.Where(id => !string.Equals(id, added, StringComparison.Ordinal));
        return onTop ? [added, .. rest] : [.. rest, added];
    }

    /// <summary>
    /// [S-598] [S-611] A new arrangement of the rows shown, written into the whole list's order: rows a
    /// filter or "Show completed" hides keep their slots and the shown ones fill the slots around them
    /// in their new order, so moving a row in a filtered list cannot shuffle, drop or promote a task
    /// the user could not see. Ids that are not in <paramref name="all"/> are ignored.
    /// </summary>
    public static IReadOnlyList<string> MergeOrder(IReadOnlyList<string> all, IReadOnlyList<string> shown)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(shown);

        var present = new HashSet<string>(all, StringComparer.Ordinal);
        var moving = new List<string>();
        var movingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in shown)
        {
            if (present.Contains(id) && movingIds.Add(id))
            {
                moving.Add(id);
            }
        }

        var next = 0;
        return [.. all.Select(id => movingIds.Contains(id) ? moving[next++] : id)];
    }

    /// <summary>
    /// [S-598] Move <paramref name="id"/> into <paramref name="target"/>'s slot, keeping every other
    /// id's relative order. Returns the input itself for a no-op or an unknown id, so a move that
    /// comes home writes nothing.
    /// </summary>
    public static IReadOnlyList<string> MoveId(IReadOnlyList<string> ids, string id, string target)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var from = IndexOf(ids, id);
        var to = IndexOf(ids, target);

        if (from < 0 || to < 0 || from == to)
        {
            return ids;
        }

        var next = ids.ToList();
        next.RemoveAt(from);
        next.Insert(to, id);
        return next;
    }

    /// <summary>
    /// [S-598] Move a row one step up (<paramref name="step"/> -1) or down (+1) among the rows shown.
    /// At either end it is a no-op, returning the input itself.
    /// </summary>
    public static IReadOnlyList<string> Nudge(IReadOnlyList<string> ids, string id, int step)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var from = IndexOf(ids, id);
        var to = from + Math.Sign(step);

        return from < 0 || step == 0 || to < 0 || to >= ids.Count ? ids : MoveId(ids, id, ids[to]);
    }

    /// <summary>Do two id lists hold the same ids in the same order?</summary>
    public static bool SameOrder(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return a.Count == b.Count && a.SequenceEqual(b, StringComparer.Ordinal);
    }

    /// <summary>
    /// [S-598] Narrow a stored order: blank entries and repeats are dropped, so a hand-edited or
    /// half-written config cannot double a row or name nothing.
    /// </summary>
    public static List<string> CleanOrder(IEnumerable<string?>? order)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var id in order ?? [])
        {
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    /// <summary>
    /// [S-601] How many lines a title wraps to before it is cut with an ellipsis. Out-of-range values
    /// are clamped rather than rejected, so no config can produce a zero-height or unbounded row.
    /// </summary>
    public static int ClampTitleLines(int lines) => Math.Clamp(lines, TitleLinesMin, TitleLinesMax);

    /// <summary>
    /// [S-595] [S-611] [S-612] What the list shows, in the order it was given: finished tasks only
    /// when asked for (and under <see cref="TaskFilter.FinishedTodayOnly"/> only those finished on
    /// <paramref name="today"/>), then the user's filters — due date, importance, words in the title
    /// in any case. Returns the input itself when nothing is left out.
    /// </summary>
    public static IReadOnlyList<TaskItem> Visible(
        IReadOnlyList<TaskItem> tasks,
        TaskFilter filter,
        DateOnly today,
        TimeZoneInfo viewer,
        WeekStartDay weekStart = WeekStartDay.Monday)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(viewer);

        var words = filter.Words;
        var weekFirst = CalendarDays.StartOfWeek(today, weekStart);
        var kept = tasks.Where(Keep).ToList();
        return kept.Count == tasks.Count ? tasks : kept;

        bool Keep(TaskItem task)
        {
            if (task.Completed && (!filter.ShowCompleted || (filter.FinishedTodayOnly && task.CompletedOn?.DayIn(viewer) != today)))
            {
                return false;
            }

            if ((filter.ImportantOnly && !task.Important)
                || (words is not null && !task.Title.Contains(words, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var due = TaskDue.Day(task.DueRaw, viewer);
            return filter.Due switch
            {
                TaskDueFilter.Overdue => !task.Completed && due < today,
                TaskDueFilter.Today => due == today,
                TaskDueFilter.ThisWeek => due >= weekFirst && due < weekFirst.AddDays(7),
                TaskDueFilter.NoDate => due is null,
                _ => true,
            };
        }
    }

    /// <summary>The header count: open tasks among those shown.</summary>
    public static int OpenCount(IEnumerable<TaskItem> tasks) => tasks.Count(t => !t.Completed);

    /// <summary>
    /// [S-784] Completed tasks after the open ones, each part in its own order — the way To Do and
    /// Google Tasks both show a list with completed tasks in it. Returns the input itself when there
    /// is nothing to move.
    /// </summary>
    public static IReadOnlyList<TaskItem> Sectioned(IReadOnlyList<TaskItem> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var firstDone = -1;
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Completed)
            {
                firstDone = i;
                break;
            }
        }

        if (firstDone < 0 || rows.Skip(firstDone).All(t => t.Completed))
        {
            return rows;
        }

        return [.. rows.Where(t => !t.Completed), .. rows.Where(t => t.Completed)];
    }

    /// <summary>
    /// [S-596] The list a task typed into Today is created in: Today has no list of its own, so the
    /// account's default one, else its first. Null when the account has no lists at all.
    /// </summary>
    public static TaskListInfo? CreateTarget(IReadOnlyList<TaskListInfo> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);
        return lists.FirstOrDefault(l => l.IsDefault) ?? lists.FirstOrDefault();
    }

    /// <summary>
    /// [S-614] Whether a list may be renamed from the widget: never Today (it is not a list) and never
    /// one of To Do's own lists (To Do refuses).
    /// </summary>
    public static bool CanRename(TaskListInfo? list) => list is { BuiltIn: false } && !IsToday(list.Id);

    /// <summary>[S-614] The name a rename writes, trimmed; null when it would write nothing (blank or unchanged).</summary>
    public static string? RenameTo(string current, string typed)
    {
        ArgumentNullException.ThrowIfNull(typed);
        var clean = typed.Trim();
        return clean.Length == 0 || string.Equals(clean, current, StringComparison.Ordinal) ? null : clean;
    }

    /// <summary>The list a widget with no list chosen shows: the default, else the first.</summary>
    public static TaskListInfo? DefaultList(IReadOnlyList<TaskListInfo> lists) => CreateTarget(lists);

    private static int IndexOf(IReadOnlyList<string> ids, string id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
