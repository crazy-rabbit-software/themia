using System.Text.Json;
using Themia.Core.Widgets;
using Themia.Core.Localization;

namespace Themia.Core.Habits;

/// <summary>
/// [S-1420] One habit, and the period keys it was ticked in.
/// <para>
/// The log lives <b>inside</b> the habit rather than in an id-keyed map beside it. The original kept
/// the two apart and needed a rule — and a test — saying that removing a habit must prune its log,
/// or a later habit reusing the id inherited a dead streak. Holding them together makes that rule
/// unnecessary: there is no way to remove the habit and keep its log.
/// </para>
/// </summary>
public sealed record Habit(string Id, string Name, IReadOnlyList<string> Log);

/// <summary>[S-1420] A named list of habits on this PC. The pool of these is device-wide; a widget points at one.</summary>
public sealed record HabitList(string Id, string Name, IReadOnlyList<Habit> Habits);

/// <summary>
/// [S-1420]/[S-1425]/[S-1429] The rules for habit lists: the trust boundary at the stored file, and
/// every mutation the widget and its settings make. Immutable throughout — each returns new arrays,
/// so the pool can hand a snapshot to its writer while the UI keeps going. Every lookup and write
/// is by id; a write naming an id that has gone is dropped, never a resurrection.
/// </summary>
public static class HabitLists
{
    /// <summary>The name a new list starts with, numbered when taken.</summary>
    public static string NewListName => Str.WidgetsB_Habits_NewListName.Text();

    /// <summary>
    /// [S-1420] Narrow an untrusted stored value. A non-array is no lists; an entry without a non-empty
    /// string <c>id</c> or a string <c>name</c> is dropped, siblings kept; a repeated id is dropped
    /// keeping the first, since a duplicate would make "which list is this" ambiguous. Never throws.
    /// </summary>
    /// <param name="dropped">Incremented once per entry (list, habit or log key) that was dropped.</param>
    public static IReadOnlyList<HabitList> Parse(JsonElement raw, ref int dropped)
    {
        if (raw.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<HabitList>();
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

            var habits = entry.TryGetProperty("habits", out var rawHabits) ? ParseHabits(rawHabits, ref dropped) : [];
            result.Add(new HabitList(id, name, habits));
        }

        return result;
    }

    /// <summary>
    /// [S-1420] Narrow an untrusted habit array: entries without a non-empty string <c>id</c> and a
    /// string <c>name</c> are dropped, as is a repeated id (a tick must land on one habit). A
    /// corrupt habit array empties one list rather than losing it.
    /// </summary>
    public static IReadOnlyList<Habit> ParseHabits(JsonElement raw, ref int dropped)
    {
        if (raw.ValueKind != JsonValueKind.Array)
        {
            if (raw.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                dropped++;
            }

            return [];
        }

        var result = new List<Habit>();
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

            var log = entry.TryGetProperty("log", out var rawLog) ? ParseLog(rawLog, ref dropped) : [];
            result.Add(new Habit(id, name, log));
        }

        return result;
    }

    /// <summary>
    /// [S-1420] Narrow an untrusted log: strings only, empty and repeated keys dropped. The keys
    /// themselves are not judged here — a key of another cadence is valid data the current one
    /// ignores (see <see cref="HabitRules"/>).
    /// </summary>
    public static IReadOnlyList<string> ParseLog(JsonElement raw, ref int dropped)
    {
        if (raw.ValueKind != JsonValueKind.Array)
        {
            if (raw.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
            {
                dropped++;
            }

            return [];
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in raw.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.String || entry.GetString() is not { Length: > 0 } key || !seen.Add(key))
            {
                dropped++;
                continue;
            }

            result.Add(key);
        }

        return result;
    }

    // ---- lists -----------------------------------------------------------------------------------------

    /// <summary>[S-1429] The list with this id, or null. Never another list in its place.</summary>
    public static HabitList? Find(IReadOnlyList<HabitList> lists, string? id)
        => string.IsNullOrEmpty(id) ? null : lists.FirstOrDefault(l => l.Id == id);

    /// <summary>[S-1429] Append an empty list; a blank name falls back to <paramref name="fallback"/>.</summary>
    public static IReadOnlyList<HabitList> Add(IReadOnlyList<HabitList> lists, string id, string name, string fallback)
    {
        var clean = name.Trim();
        return [.. lists, new HabitList(id, clean.Length > 0 ? clean : fallback, [])];
    }

    /// <summary>
    /// [S-1429] Rename a list. A blank name is kept: the name box writes through as it is typed in, and
    /// refusing the empty string would make it impossible to clear before retyping.
    /// </summary>
    public static IReadOnlyList<HabitList> Rename(IReadOnlyList<HabitList> lists, string id, string name)
        => [.. lists.Select(l => l.Id == id ? l with { Name = name } : l)];

    /// <summary>[S-1429] Remove a list, its habits and their logs with it.</summary>
    public static IReadOnlyList<HabitList> Remove(IReadOnlyList<HabitList> lists, string id)
        => [.. lists.Where(l => l.Id != id)];

    /// <summary>[S-1420] Replace one list's habits — the one write path for every habit change.</summary>
    public static IReadOnlyList<HabitList> SetHabits(IReadOnlyList<HabitList> lists, string id, IReadOnlyList<Habit> habits)
        => [.. lists.Select(l => l.Id == id ? l with { Habits = habits } : l)];

    /// <summary>[S-1429] <paramref name="baseName"/>, or <c>baseName 2</c>, <c>3</c>… when taken.</summary>
    public static string UniqueName(IReadOnlyList<HabitList> lists, string baseName)
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
    /// [S-1420] The list a widget with none chosen should show: the first on this PC, or null — and
    /// only null means the caller creates one, so a fresh widget never litters the pool.
    /// </summary>
    public static string? First(IReadOnlyList<HabitList> lists) => lists.FirstOrDefault()?.Id;

    // ---- habits within a list ----------------------------------------------------------------------------

    /// <summary>
    /// [S-1425] Append a habit at the bottom, where the add row that made it is, trimming the name. A
    /// blank name is a no-op returning the input itself, so a nameless habit cannot be created.
    /// </summary>
    public static IReadOnlyList<Habit> AddHabit(IReadOnlyList<Habit> habits, string name, string id)
    {
        var clean = name.Trim();
        return clean.Length == 0 ? habits : [.. habits, new Habit(id, clean, [])];
    }

    /// <summary>
    /// [S-1425] Rename one habit, trimmed. A blank or unchanged name returns the input itself: an
    /// emptied editor keeps the habit, because deleting is the menu's job. The id — and so the log —
    /// never changes.
    /// </summary>
    public static IReadOnlyList<Habit> RenameHabit(IReadOnlyList<Habit> habits, string id, string name)
    {
        var clean = name.Trim();

        if (clean.Length == 0 || habits.FirstOrDefault(h => h.Id == id) is not { } habit || habit.Name == clean)
        {
            return habits;
        }

        return [.. habits.Select(h => h.Id == id ? h with { Name = clean } : h)];
    }

    /// <summary>[S-1425] Remove one habit, and its log with it. Unknown id: the input itself.</summary>
    public static IReadOnlyList<Habit> RemoveHabit(IReadOnlyList<Habit> habits, string id)
        => habits.Any(h => h.Id == id) ? [.. habits.Where(h => h.Id != id)] : habits;

    /// <summary>
    /// [S-1425] Move one habit up (<paramref name="delta"/> −1) or down (+1), keeping every other habit's
    /// order. Past either end, or an unknown id, is a no-op returning the input itself, so the caller
    /// writes nothing.
    /// </summary>
    public static IReadOnlyList<Habit> MoveHabit(IReadOnlyList<Habit> habits, string id, int delta)
    {
        var from = IndexOf(habits, id);
        var to = from + delta;

        if (from < 0 || delta == 0 || to < 0 || to >= habits.Count)
        {
            return habits;
        }

        var next = habits.ToList();
        var moved = next[from];
        next.RemoveAt(from);
        next.Insert(to, moved);
        return next;
    }

    /// <summary>[S-1421] Tick or un-tick one habit in the period <paramref name="key"/> names.</summary>
    public static IReadOnlyList<Habit> ToggleHabit(IReadOnlyList<Habit> habits, string id, string key)
        => IndexOf(habits, id) < 0 ? habits : [.. habits.Select(h => h.Id == id ? h with { Log = HabitRules.Toggle(h.Log, key) } : h)];

    /// <summary>A fresh id for a list or a habit. Opaque; nothing reads anything out of it.</summary>
    public static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid():n}";

    private static int IndexOf(IReadOnlyList<Habit> habits, string id)
    {
        for (var i = 0; i < habits.Count; i++)
        {
            if (habits[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private static string? String(JsonElement entry, string name)
        => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
