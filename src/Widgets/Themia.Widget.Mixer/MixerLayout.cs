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

    /// <summary>
    /// [S-11805] The mute button's side on a row of <paramref name="lines"/> lines: the 24 (the family's header
    /// button) inset 2 in a one-line row of 28, so stacked rows' buttons stand 4 apart; the 28 icon button in
    /// a two-line row of 40.
    /// </summary>
    public static double Mute(int lines) => lines >= 2 ? FamilyShape.IconButton : FamilyShape.HeaderButton;

    /// <summary>[S-11309] The fewest, the most and a new mixer's rows.</summary>
    public const int MinRows = 1, MaxRows = 12, DefaultRows = 4;

    /// <summary>[S-11309] The card's width: 240 to 520, a new one 300; at 240 the slider keeps its least travel beside the name.</summary>
    public const double Narrowest = 240, NaturalWidth = 300, Widest = 520;

    /// <summary>[S-11309] The rows the config gives, held to <see cref="MinRows"/>..<see cref="MaxRows"/>.</summary>
    public static int ClampRows(int rows) => Math.Clamp(rows, MinRows, MaxRows);

    /// <summary>[S-11309] The height's steps: the content top and the inset as the base, a row as the step, the config's rows as the count.</summary>
    public static Sdk.SizeSteps Steps(int rows, int lines)
        => new(FamilySpace.ContentTop + FamilySpace.Inset, FamilyShape.RowHeight(Math.Clamp(lines, 1, 2)), MinRows, MaxRows, ClampRows(rows));

    /// <summary>
    /// [S-11309] The mixer's sizing ([S-2438]): its rows (one or two lines each) under its header, stepped
    /// by a row and following the config's rows, 240..520 wide.
    /// </summary>
    public static Sdk.WidgetSizing Sizing(Model.MixerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Sdk.WidgetSizing.Range(new(Narrowest, 1), new(NaturalWidth, 1), new(Widest, 1)).WithHeightSteps(Steps(config.Rows, config.Lines));
    }

    /// <summary>[S-11309] The config a mixer dragged to <paramref name="size"/> means: the rows that height holds.</summary>
    public static Model.WidgetConfig SizeSets(Model.MixerConfig config, Sdk.SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = Steps(config.Rows, config.Lines).Nearest(size.Height);
        if (rows == ClampRows(config.Rows))
        {
            return config;
        }

        var copy = (Model.MixerConfig)config.Clone();
        copy.Rows = rows;
        return copy;
    }

    /// <summary>
    /// [S-449] [S-11309] The rows for a view <paramref name="width"/> wide: the config's rows and lines,
    /// the playing apps first and the quiet ones under them while rows are left (more scroll inside them);
    /// the slider 0.6 of the room beside the name and the mute button, never under its least travel.
    /// </summary>
    public static MixerPlan Plan(double width, bool icons, int lines, int rows, int playing, int quiet)
    {
        var room = (double.IsFinite(width) ? width : Narrowest) - (2 * FamilySpace.Inset) - Fixed(icons);
        var slider = Math.Max(ControlsRules.MinSlider, Math.Floor(room * SliderShare));
        var shown = ClampRows(rows);
        var quietRows = Math.Max(0, Math.Min(shown - Math.Max(0, playing), quiet));
        return new MixerPlan(Math.Clamp(lines, 1, 2), shown, quietRows, slider, true);
    }
}
