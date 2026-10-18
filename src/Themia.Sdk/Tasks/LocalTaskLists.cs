using System.Text.Json;
using Themia.Core.Localization;

namespace Themia.Core.Tasks;

/// <summary>
/// [S-603] A task kept on this PC. Deliberately lean: an account task carries importance and a due
/// date a local one has no source for, so only what the widget can edit is stored.
/// </summary>
public sealed record LocalTask(string Id, string Title, bool Done);

/// <summary>[S-603] A named list on this PC. The pool of these is device-wide; a widget points at one.</summary>
public sealed record LocalTaskList(string Id, string Name, IReadOnlyList<LocalTask> Tasks);

/// <summary>
/// [S-603]/[S-604] The rules for lists kept on this PC: the trust boundary at the stored file, and
/// every mutation the widget and its settings make. Immutable throughout — each returns a new
/// array, so the service can hand a snapshot to the writer while the UI keeps going.
/// <para>
/// Lists are a pool rather than part of a widget's config because a pool you pick from and create
/// into has to outlive the widget that made it: deleting a widget does not delete its tasks, two
/// widgets may show one list, and renaming a list retitles every widget showing it.
/// </para>
/// </summary>
public static class LocalTaskLists
{
    /// <summary>The name a new list starts with, numbered when taken.</summary>
    public static string NewListName => Str.Todo_LocalLists_NewName.Text();

    /// <summary>
    /// [S-603] Narrow an untrusted stored value. A non-array is no lists; an entry without a
    /// non-empty string <c>id</c> or a string <c>name</c> is dropped, siblings kept; a repeated id is
    /// dropped keeping the first, because every lookup and write is by id and a duplicate would make
    /// "which list is this" ambiguous. Never throws.
    /// </summary>
    /// <param name="dropped">Incremented once per entry (list or task) that was dropped.</param>
    public static IReadOnlyList<LocalTaskList> Parse(JsonElement raw, ref int dropped)
    {
        if (raw.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<LocalTaskList>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in raw.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || String(entry, "id") is not { Length: > 0 } id
                || String(entry, "name") is not { } name
                || !seen.Add(id))
            {
                dropped++;
                continue;
            }

            var tasks = entry.TryGetProperty("tasks", out var rawTasks) ? ParseTasks(rawTasks, ref dropped) : [];
            result.Add(new LocalTaskList(id, name, tasks));
        }

        return result;
    }

    /// <summary>
    /// [S-603] Narrow an untrusted task array: entries without a string <c>id</c> and <c>title</c>
    /// are dropped, a non-boolean <c>done</c> reads as false, and a non-array is no tasks — so a
    /// corrupt task array empties one list rather than losing the list.
    /// </summary>
    public static IReadOnlyList<LocalTask> ParseTasks(JsonElement raw, ref int dropped)
    {
        if (raw.ValueKind != JsonValueKind.Array)
        {
            if (raw.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                dropped++;
            }

            return [];
        }

        var result = new List<LocalTask>();

        foreach (var entry in raw.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object
                || String(entry, "id") is not { Length: > 0 } id
                || String(entry, "title") is not { } title)
            {
                dropped++;
                continue;
            }

            var done = entry.TryGetProperty("done", out var flag) && flag.ValueKind == JsonValueKind.True;
            result.Add(new LocalTask(id, title, done));
        }

        return result;
    }

    /// <summary>[S-604] The list with this id, or null. Never another list in its place.</summary>
    public static LocalTaskList? Find(IReadOnlyList<LocalTaskList> lists, string? id)
        => string.IsNullOrEmpty(id) ? null : lists.FirstOrDefault(l => l.Id == id);

    /// <summary>
    /// [S-604] Append an empty list. The name is trimmed, and a blank one falls back to
    /// <paramref name="fallback"/>, so a list can never end up unnamed and unclickable.
    /// </summary>
    public static IReadOnlyList<LocalTaskList> Add(IReadOnlyList<LocalTaskList> lists, string id, string name, string fallback)
    {
        var clean = name.Trim();
        return [.. lists, new LocalTaskList(id, clean.Length > 0 ? clean : fallback, [])];
    }

    /// <summary>
    /// [S-604] Rename a list. A blank name is kept: the name box writes through as it is typed in,
    /// and refusing the empty string would make the field impossible to clear before retyping.
    /// </summary>
    public static IReadOnlyList<LocalTaskList> Rename(IReadOnlyList<LocalTaskList> lists, string id, string name)
        => [.. lists.Select(l => l.Id == id ? l with { Name = name } : l)];

    /// <summary>[S-604] Remove a list and its tasks with it. Unknown id: the same lists.</summary>
    public static IReadOnlyList<LocalTaskList> Remove(IReadOnlyList<LocalTaskList> lists, string id)
        => [.. lists.Where(l => l.Id != id)];

    /// <summary>
    /// [S-603] Replace one list's tasks — the one write path for every task change. A write to a list
    /// that has gone is dropped, never a resurrection of the list.
    /// </summary>
    public static IReadOnlyList<LocalTaskList> SetTasks(IReadOnlyList<LocalTaskList> lists, string id, IReadOnlyList<LocalTask> tasks)
        => [.. lists.Select(l => l.Id == id ? l with { Tasks = tasks } : l)];

    /// <summary>
    /// [S-604] <paramref name="baseName"/>, or <c>baseName 2</c>, <c>3</c>… when taken, filling the
    /// first hole. Two lists may share a name — they are addressed by id — but "New list" three times
    /// over is a picker nobody can read.
    /// </summary>
    public static string UniqueName(IReadOnlyList<LocalTaskList> lists, string baseName)
    {
        var taken = new HashSet<string>(lists.Select(l => l.Name), StringComparer.Ordinal);

        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// [S-604] The list a widget switching to "This PC" should show: the one it had if it still
    /// exists, else the first, else null — and only null means the caller creates one, so switching
    /// source back and forth cannot litter the pool with empty lists.
    /// </summary>
    public static string? Ensure(IReadOnlyList<LocalTaskList> lists, string? preferred)
        => Find(lists, preferred)?.Id ?? lists.FirstOrDefault()?.Id;

    // ---- tasks within a list -------------------------------------------------------------------

    /// <summary>
    /// [S-599] Add an open task at the bottom, where the add footer is, trimming the title — or
    /// [S-610] at the top when the widget puts new tasks there. A blank title is a no-op returning the
    /// input itself.
    /// </summary>
    public static IReadOnlyList<LocalTask> AddTask(IReadOnlyList<LocalTask> tasks, string title, string id, bool onTop = false)
    {
        var clean = title.Trim();
        if (clean.Length == 0)
        {
            return tasks;
        }

        var task = new LocalTask(id, clean, false);
        return onTop ? [task, .. tasks] : [.. tasks, task];
    }

    /// <summary>[S-603] Flip one task's done flag. Unknown id: an equal list.</summary>
    public static IReadOnlyList<LocalTask> Toggle(IReadOnlyList<LocalTask> tasks, string id)
        => [.. tasks.Select(t => t.Id == id ? t with { Done = !t.Done } : t)];

    /// <summary>[S-603] Set one task's done flag. Unknown id: an equal list.</summary>
    public static IReadOnlyList<LocalTask> SetDone(IReadOnlyList<LocalTask> tasks, string id, bool done)
        => [.. tasks.Select(t => t.Id == id ? t with { Done = done } : t)];

    /// <summary>
    /// [S-600] Rename one task, trimmed. A blank title is a no-op returning the input itself:
    /// committing an emptied editor keeps the task rather than blanking it, because deleting is the
    /// menu's job and a task with no title cannot be recovered from the UI.
    /// </summary>
    public static IReadOnlyList<LocalTask> RenameTask(IReadOnlyList<LocalTask> tasks, string id, string title)
    {
        var clean = title.Trim();
        return clean.Length == 0 ? tasks : [.. tasks.Select(t => t.Id == id ? t with { Title = clean } : t)];
    }

    /// <summary>[S-603] Remove one task. Unknown id: an equal list.</summary>
    public static IReadOnlyList<LocalTask> RemoveTask(IReadOnlyList<LocalTask> tasks, string id)
        => [.. tasks.Where(t => t.Id != id)];

    /// <summary>
    /// [S-598] Rewrite the stored order so the tasks named in <paramref name="ids"/> appear in that
    /// order. A local list's array <i>is</i> its order. Tasks the widget was hiding (completed, with
    /// completed hidden) are not in <paramref name="ids"/> and keep their absolute slots; the moved
    /// rows fill the slots around them, so a reorder cannot resurrect, drop or shuffle a task the
    /// user could not see. Unknown ids are ignored.
    /// </summary>
    public static IReadOnlyList<LocalTask> Reorder(IReadOnlyList<LocalTask> tasks, IReadOnlyList<string> ids)
    {
        var byId = new Dictionary<string, LocalTask>(StringComparer.Ordinal);
        foreach (var task in tasks)
        {
            byId.TryAdd(task.Id, task);
        }

        var moving = new List<LocalTask>();
        var movingIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (byId.TryGetValue(id, out var task) && movingIds.Add(id))
            {
                moving.Add(task);
            }
        }

        var next = 0;
        return [.. tasks.Select(t => movingIds.Contains(t.Id) ? moving[next++] : t)];
    }

    /// <summary>A local task as the widget draws every task.</summary>
    public static TaskItem ToItem(LocalTask task, string listId) => new(task.Id, listId, task.Title, task.Done);

    private static string? String(JsonElement entry, string name)
        => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
