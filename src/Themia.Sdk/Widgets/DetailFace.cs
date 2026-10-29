using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-2402] What a detail widget has to place, in its own terms.</summary>
/// <param name="Hero">The main figure's widest case (its value and unit, whether a line stands under it), or null for a widget without one (a list).</param>
/// <param name="Facts">How many facts it has, most wanted first.</param>
/// <param name="ListRows">How many list rows it has (processes, adapters); 0 for none.</param>
/// <param name="ListLines">How many lines a list row would show at most (a name, a second figure, a detail line): 1 to 3.</param>
public readonly record struct DetailShape(ReadoutText? Hero, int Facts, int ListRows = 0, int ListLines = 1);

/// <summary>[S-2402] One detail widget's face for its view's area.</summary>
/// <param name="Side">The figure in a column on the left and the rows beside it (an expanded width), else the rows under it.</param>
/// <param name="Hero">The figure's Readout step.</param>
/// <param name="Under">Whether the line under the figure (its caption) is shown.</param>
/// <param name="HeroColumn">On the side: the figure's column, from the card's edge to where the rows begin (the inset included).</param>
/// <param name="RowsTop">Where the rows start in the view: 8 under the figure, or at its top.</param>
/// <param name="Facts">How many fact rows are shown (the first ones).</param>
/// <param name="ListRows">How many list rows are shown at a time (whole rows; the list scrolls for the rest).</param>
/// <param name="ListLines">How many lines each list row shows (its pitch follows: 32, 48, 64).</param>
public readonly record struct DetailFacePlan(bool Side, TypeStep Hero, bool Under, double HeroColumn, double RowsTop, int Facts, int ListRows, int ListLines)
{
    public bool List => ListRows > 0;

    /// <summary>The list's height: its whole rows.</summary>
    public double ListHeight => ListRows * FamilyShape.RowHeight(ListLines);
}

/// <summary>
/// [S-2402] The ladder every system detail widget grows by, on the family's blocks ([S-6702],
/// [S-6711], [S-6712]). A detail widget is a main figure (the speed, the memory in use, the charge)
/// with a line under it saying what it is of, facts in priority order and, for some, a list
/// (processes, adapters). The figure is a Readout at the view's top, start-aligned, on the step its
/// widest case takes: 28, 40, 68 by the area and no larger ([S-666]). The facts are fact rows (the
/// value primary, the name secondary), one line, 32 each; the list's rows show one, two or three
/// lines by the tier. Only whole rows are shown, facts first, then the list.
/// <list type="bullet">
/// <item>An expanded width (Wide, L, XL): the figure in a column of two fifths of the width, the rows
/// beside it from the top, so a large tile shows more rows, not a larger figure.</item>
/// <item>Otherwise the rows stand 8 under the figure, as many as fit; a tiny tile is the figure alone.</item>
/// <item>No figure (Top processes): the rows from the top.</item>
/// </list>
/// </summary>
public static class DetailFace
{
    /// <summary>On the side, the figure's column takes this share of the card's width (its inset included).</summary>
    public const double HeroShare = 0.4;

    /// <summary>The plan for a view of <paramref name="area"/> and a sheet of <paramref name="shape"/>.</summary>
    public static DetailFacePlan Plan(WidgetArea area, DetailShape shape)
    {
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var lines = RowRules.Lines(area, Math.Clamp(shape.ListLines, 1, 3));
        var rows = shape.Facts + shape.ListRows;

        if (shape.Hero is not { } hero)
        {
            return Fill(new DetailFacePlan(false, FamilyType.Big28, false, 0, 0, 0, 0, lines), shape, height);
        }

        var side = rows > 0 && area.WidthClass >= SizeClass.Expanded;
        var column = side ? Math.Floor(area.Width * HeroShare) : area.Width;
        var room = side ? column - FamilySpace.Inset : width;
        var step = ReadoutRules.Step(area, room, height, hero);
        var under = hero.LineUnder && ReadoutRules.Height(step, hero) <= height;
        var figure = step.LineHeight + (under ? FamilyType.Under(step).LineHeight : 0);

        var plan = new DetailFacePlan(side, step, under, side ? column : 0, side ? 0 : figure + FamilySpace.S2, 0, 0, lines);
        return Fill(plan, shape, height - plan.RowsTop);
    }

    /// <summary>The facts, then the list, in the whole rows <paramref name="room"/> takes.</summary>
    private static DetailFacePlan Fill(DetailFacePlan plan, DetailShape shape, double room)
    {
        var facts = Math.Min(shape.Facts, RowRules.Fit(room, 1));
        var left = room - (facts * FamilyShape.RowHeight(1));
        var list = Math.Min(shape.ListRows, RowRules.Fit(left, plan.ListLines));
        return plan with { Facts = facts, ListRows = list };
    }

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
