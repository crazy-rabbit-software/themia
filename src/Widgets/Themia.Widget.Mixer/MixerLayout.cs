using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-449] What the mixer's list shows at one size.
/// </summary>
/// <param name="Lines">[S-6712] The lines every row shows: the second says what the app is doing (<see cref="MixerFill.Detail(MixerRow)"/>).</param>
/// <param name="Rows">Whole rows of the playing apps the list shows before it scrolls (at least one).</param>
/// <param name="Quiet">[S-1228] Rows of the silent apps Only-active left out, shown under the list when every playing row fits.</param>
/// <param name="Slider">The slider's width; <see cref="double.NaN"/> takes the row's room (the name gives way); 0 is no slider.</param>
/// <param name="Names">Whether a row shows its app's name beside its lead.</param>
public readonly record struct MixerPlan(int Lines, int Rows, int Quiet, double Slider, bool Names);

/// <summary>
/// [S-449] The mixer's ladder on the family's rows ([S-6711], [S-6712]): one Row block per app, [lead 16:
/// the app's icon] 8 [name] 12 [slider] 8 [mute, a 28 icon button], 28 or 40 tall by the tier (the
/// second line what the app is doing), as many whole rows as the view's area less the card's bottom
/// inset holds. The slider takes three fifths of the room the lead, the gaps and the mute leave, so
/// the name keeps two; where that is under the slider's least (80) a row with its app's icon drops
/// the name for a slider over all of it (the icon names the app; the tooltip has the name); where
/// even that is too narrow a row is its icon, its name and its mute.
/// </summary>
public static class MixerLayout
{
    /// <summary>The slider's share of the room the lead, the gaps and the mute leave a named row.</summary>
    public const double SliderShare = 0.6;

    /// <summary>The room a row's lead, its gaps and its mute take from its inner width: lead 16 + 8, 12 before the meter, 8 + the 32 mute.</summary>
    public static double Fixed(bool icons) => (icons ? FamilyShape.Icon + FamilySpace.GlyphGap : 0) + FamilySpace.ColumnGap + FamilySpace.S2 + FamilyShape.IconButton;

    /// <summary>[S-449] The plan for a view of <paramref name="area"/> listing <paramref name="playing"/> rows and <paramref name="quiet"/> quiet ones.</summary>
    public static MixerPlan Plan(WidgetArea area, bool icons, int playing, int quiet)
    {
        if (!area.IsMeasured)
        {
            return new MixerPlan(1, Math.Max(0, playing), 0, 0, true);
        }

        var room = area.Width - (2 * FamilySpace.Inset) - Fixed(icons);
        var named = Math.Floor(room * SliderShare);
        var (slider, names) = named >= ControlsRules.MinSlider ? (named, true)
            : icons && room >= ControlsRules.MinSlider ? (double.NaN, false)
            : (0d, true);

        // A row without its name has no second line under it.
        var lines = names ? RowRules.Lines(area, 2) : 1;
        var fit = RowRules.Fit(area.Height - FamilySpace.Inset, lines);
        var rows = Math.Max(1, Math.Min(fit, playing));
        var quietRows = playing <= fit ? Math.Max(0, Math.Min(fit - playing, quiet)) : 0;
        return new MixerPlan(lines, Math.Min(rows, Math.Max(0, playing)), quietRows, slider, names);
    }
}
