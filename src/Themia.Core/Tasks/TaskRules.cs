using Themia.Core.Localization;
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
    /// [S-598] Arrange rows by the user's order: ids the order names (and that are present) first, in
    /// that order, then everything else in the provider's order. An empty order returns the input
    /// untouched, so an untouched widget keeps the provider's own sorting (and Today's oldest-first).
    /// A deleted id leaves no hole, a corrupt duplicate cannot double a row, and an id the order has
    /// never seen — a task just created — lands at the bottom ([S-599]).
    /// </summary>
    public static IReadOnlyList<T> ApplyOrder<T>(IReadOnlyList<T> rows, IReadOnlyList<string> order, Func<T, string> id)
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
        var result = new List<T>(rows.Count);

        foreach (var wanted in order)
        {
            if (byId.TryGetValue(wanted, out var row) && taken.Add(wanted))
            {
                result.Add(row);
            }
        }

        foreach (var row in rows)
        {
            if (taken.Add(id(row)))
            {
                result.Add(row);
            }
        }

        return result;
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

    /// <summary>[S-595] What the list shows: completed tasks are hidden unless asked for. Order kept.</summary>
    public static IReadOnlyList<TaskItem> Visible(IReadOnlyList<TaskItem> tasks, bool showCompleted)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        return showCompleted ? tasks : tasks.Where(t => !t.Completed).ToList();
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
