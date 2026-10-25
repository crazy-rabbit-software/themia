using Themia.Widget.Scratchpad;

namespace Themia.Core.Widgets;

/// <summary>One preset ink: what is stored (<c>#RRGGBB</c>, content rather than chrome) and its name, in the current language.</summary>
public readonly record struct Ink(string Hex, ScratchpadStr Name);

/// <summary>
/// [S-1497] The scratchpad's presets, shared by its properties menu and the toolbar on the pad: the
/// original's palette-and-three-sizes toolbar ([S-02] of the original). Six inks that read on the
/// pad in either theme, and the original's three nibs.
/// </summary>
public static class InkPalette
{
    public static IReadOnlyList<Ink> Inks { get; } =
    [
        new("#0078D4", ScratchpadStr.WidgetsC_Scratchpad_Ink_Blue),
        new("#F43F5E", ScratchpadStr.WidgetsC_Scratchpad_Ink_Rose),
        new("#F59E0B", ScratchpadStr.WidgetsC_Scratchpad_Ink_Amber),
        new("#10B981", ScratchpadStr.WidgetsC_Scratchpad_Ink_Emerald),
        new("#7C3AED", ScratchpadStr.WidgetsC_Scratchpad_Ink_Violet),
        new("#E7E5E4", ScratchpadStr.WidgetsC_Scratchpad_Ink_Chalk),
    ];

    /// <summary>The original's <c>SIZES</c>: fine, the default-ish middle, and bold.</summary>
    public static IReadOnlyList<double> Widths { get; } = [2, 4, 8];

    /// <summary>The preset nearest <paramref name="width"/>, so a width set by the slider still marks one dot.</summary>
    public static double NearestWidth(double width)
    {
        var best = Widths[0];

        foreach (var candidate in Widths)
        {
            if (Math.Abs(candidate - width) < Math.Abs(best - width))
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>Whether <paramref name="hex"/> is this preset, ignoring case and a missing <c>#</c>.</summary>
    public static bool Matches(Ink ink, string? hex)
        => HexColor.Normalise(hex ?? string.Empty).Equals(ink.Hex, StringComparison.OrdinalIgnoreCase);
}
