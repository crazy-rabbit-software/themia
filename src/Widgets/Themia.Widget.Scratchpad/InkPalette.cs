using Themia.Core.Localization;
using Themia.Core.Settings;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1497] The scratchpad's presets, shared by its properties menu and the toolbar on the pad: the
/// original's palette-and-three-sizes toolbar ([S-02] of the original). Six inks that read on the
/// pad in either theme, and the original's three nibs.
/// <para>
/// The inks are swatches of the host's one palette ([S-1549], <see cref="ColorPresets"/>), named by
/// it: a pen's colour is data the user picks, so its presets are the picker's own rather than
/// colours this module types ([S-6705]).
/// </para>
/// </summary>
public static class InkPalette
{
    public static IReadOnlyList<ColorPreset> Inks { get; } =
    [
        Swatch(Str.Shell_Color_DefaultBlue),
        Swatch(Str.Shell_Color_PaleRed),
        Swatch(Str.Shell_Color_Gold),
        Swatch(Str.Shell_Color_MintLight),
        Swatch(Str.Shell_Color_IrisSpring),
        Swatch(Str.Shell_Color_LightGray),
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
    public static bool Matches(ColorPreset ink, string? hex)
        => HexColor.Normalise(hex ?? string.Empty).Equals(HexColor.Normalise(ink.Hex), StringComparison.OrdinalIgnoreCase);

    /// <summary>The host palette's swatch of that name; a palette without it is a build error caught by the tests.</summary>
    private static ColorPreset Swatch(Str name)
    {
        var label = name.Local();
        return ColorPresets.Surfaces.First(preset => preset.Label.Equals(label));
    }
}
