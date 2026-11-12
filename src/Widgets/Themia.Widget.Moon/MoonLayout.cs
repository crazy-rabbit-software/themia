using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-3105] One rung of the Moon widget's list, in the order the ladder gives them.</summary>
public enum MoonRow
{
    /// <summary>The next moonrise (the next of rise and set when only one row fits).</summary>
    Rise = 0,

    /// <summary>The next moonset.</summary>
    Set = 1,

    /// <summary>The next full or new moon, whichever comes first ([S-1491]).</summary>
    NextMajor = 2,
}

/// <summary>What the Moon widget shows at one size. [S-3105]</summary>
/// <param name="Face">The readout and the rows, as the detail widgets lay them out ([S-2402]).</param>
/// <param name="Disc">Whether the moon leads the readout at its step: it gives way first, never the figure.</param>
public readonly record struct MoonPlan(DetailFacePlan Face, bool Disc);

/// <summary>
/// [S-3105] The Moon widget's ladder on the family's blocks ([S-6702], [S-6711]): a Readout of how
/// much of the moon is lit ("23%"), the moon drawn at its phase as the readout's lead at the step's
/// size, the phase's name the line under it; then whole fact rows, moonrise, moonset, the next full or
/// new moon. The readout's step is the area's (28, or 40 once expanded both ways) for the widest figure ("100%"), and room
/// left goes to the rows, never a bigger moon ([S-666]). Where the readout is too narrow for the moon
/// beside even the floor step, the moon gives way. On an expanded width the readout stands in a column
/// of two fifths of the card and the rows beside it from the top (<see cref="DetailFace"/>).
/// </summary>
public static class MoonLayout
{
    /// <summary>The rows, in priority order.</summary>
    public static IReadOnlyList<MoonRow> Ladder { get; } = [MoonRow.Rise, MoonRow.Set, MoonRow.NextMajor];

    /// <summary>A row's lines at most: the name and the next event's countdown.</summary>
    public const int RowLines = 2;

    /// <summary>Plan the widget for its view's area; <paramref name="full"/> is the language's "100%".</summary>
    public static MoonPlan Plan(WidgetArea area, string full = "100%")
    {
        var worst = ReadoutRules.Split(full) with { Lead = true, LineUnder = true };
        var face = DetailFace.Plan(area, Shape(worst));
        var room = face.Side ? face.HeroColumn - FamilySpace.Inset : area.Width - (2 * FamilySpace.Inset);

        if (ReadoutRules.Width(face.Hero, worst) <= room)
        {
            return new MoonPlan(face, true);
        }

        return new MoonPlan(DetailFace.Plan(area, Shape(worst with { Lead = false })), false);
    }

    private static DetailShape Shape(ReadoutText worst) => new(worst, 0, Ladder.Count, RowLines);
}
