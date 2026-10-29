using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Core.Astronomy;

/// <summary>
/// [S-6735] What a row of the Sun or Moon widget has for its words, and which form of its name it
/// shows. A row is the family's fact row ([S-6711]): a 16 lead, the name, the value at the end. Long
/// words are a language's, not a size's, so the row decides from the words it has: the name takes its
/// short form ("Aufgang" for "Mondaufgang") where the long one and the value do not fit side by side,
/// and a date drops its weekday where even that does not fit. Widths are the ramp's estimate
/// (<see cref="FluentType.EstimateWidth"/>), Body for both texts.
/// </summary>
public static class SkyRows
{
    /// <summary>The room a row of <paramref name="width"/> has for its name and value: less the side insets, the lead and its gap, and the gap before the value.</summary>
    public static double Room(double width)
        => Math.Max(0, width - (2 * FamilySpace.Inset) - FamilyShape.Icon - FamilySpace.GlyphGap - FamilySpace.ColumnGap);

    /// <summary>Whether <paramref name="name"/> and <paramref name="value"/> stand side by side in <paramref name="room"/>.</summary>
    public static bool Fits(double room, string name, string value)
        => Width(name) + Width(value) <= room + 0.01;

    /// <summary>The name the row shows: the long one where it fits beside the value, else the short one where there is one.</summary>
    public static string Name(double room, string name, string? shortName, string value)
        => string.IsNullOrEmpty(shortName) || Fits(room, name, value) ? name : shortName;

    private static double Width(string text) => text.Length == 0 ? 0 : FluentType.EstimateWidth(FluentType.Body, text.Length);
}
