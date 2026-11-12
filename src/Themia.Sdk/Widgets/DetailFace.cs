using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-11007] [S-11008] What a detail atom shows: its fact rows, or its list's rows, as its config states them.</summary>
/// <param name="Facts">How many fact rows are shown (the atom's pick).</param>
/// <param name="ListRows">How many list rows are shown at a time (the rows its config states; the list scrolls for the rest).</param>
/// <param name="ListLines">How many lines each list row shows (its pitch follows: 28, 40, 56).</param>
public readonly record struct DetailFacePlan(int Facts, int ListRows, int ListLines)
{
    public bool List => ListRows > 0;

    /// <summary>The list's height: its whole rows.</summary>
    public double ListHeight => ListRows * FamilyShape.RowHeight(ListLines);
}

/// <summary>
/// [S-11007] [S-11008] The rules every system detail atom shares, on the family's blocks ([S-6711]):
/// when a new read lays the tile out again, and how many lines a list's rows have. What an atom shows
/// is its config at every size ([S-11800]).
/// </summary>
public static class DetailFace
{
    /// <summary>
    /// [S-6333] Whether a new read lays the tile out again rather than filling the rows it has: when
    /// what the face is made of changes (the facts' count, the caption, the figure coming or going, the
    /// sheet turning absent), or a list's new length changes the plan. A list that scrolls keeps its
    /// layout as processes start and exit.
    /// </summary>
    public static bool Relays(DetailSheet shown, DetailSheet next, DetailFacePlan plan, Func<DetailSheet, DetailFacePlan> planFor)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(planFor);

        if (shown.Facts.Count != next.Facts.Count
            || (shown.Caption is null) != (next.Caption is null)
            || shown.Absent != next.Absent
            || (shown.Hero is null) != (next.Hero is null))
        {
            return true;
        }

        if (shown.Rows.Count == next.Rows.Count)
        {
            return false;
        }

        return (shown.Rows.Count == 0) != (next.Rows.Count == 0) || planFor(next) != plan;
    }

    /// <summary>[S-2404] How many lines a list's rows would show: the name, a second figure, a detail line.</summary>
    public static int ListLines(IReadOnlyList<DetailRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var lines = 1;
        foreach (var row in rows)
        {
            lines = Math.Max(lines, 1 + (row.Second is null ? 0 : 1) + (row.Detail is null ? 0 : 1));
        }

        return lines;
    }
}
