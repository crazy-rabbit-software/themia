using Themia.Core.Geometry;

namespace Themia.Core.Widgets;

/// <summary>[S-3918] How the Switch style arranges its parts.</summary>
public enum SwitchLayout
{
    /// <summary>A square or tall tile: icon, name, state and switch stacked and centred as one group.</summary>
    Column = 0,

    /// <summary>A wide strip, as a Settings row: icon and name (state under it) at the left, the switch at the right.</summary>
    Row = 1,
}

/// <summary>
/// [S-3918] How a toggle's Switch style draws itself in its box. Every part is placed, in DIPs from
/// the top left of the box inside <see cref="ButtonInset"/>, so the centring is a rule (and tested),
/// not something the view's panels happen to do.
/// </summary>
/// <param name="Layout">Column or row.</param>
/// <param name="Level">The step on <see cref="FluentType.Ramp"/> the whole face is drawn at (0 is Caption).</param>
/// <param name="Gap">Between the parts at this level, on the 4 px grid.</param>
/// <param name="Name">The name's step, or null where not even Caption fits.</param>
/// <param name="State">The state line's step ("On", "Off", "Unavailable"), or null where it does not fit.</param>
/// <param name="Icon">The icon's square; empty where there is no room for it.</param>
/// <param name="NameBox">The name's line (as wide as the room it may use, text centred in a column).</param>
/// <param name="StateBox">The state's line; empty without a state.</param>
/// <param name="Track">The switch's track: Windows' 2:1 pill.</param>
public readonly record struct SwitchFacePlan(
    SwitchLayout Layout,
    int Level,
    double Gap,
    TypeStep? Name,
    TypeStep? State,
    PixelRect Icon,
    PixelRect NameBox,
    PixelRect StateBox,
    PixelRect Track)
{
    public double IconSize => Icon.Width;

    public double TrackWidth => Track.Width;

    public double TrackHeight => Track.Height;

    /// <summary>The knob's diameter at rest: Windows' 12 in a 20-high track.</summary>
    public double Knob => Math.Round(TrackHeight * 0.6);

    /// <summary>The knob's diameter under the pointer: Windows' 14.</summary>
    public double KnobHover => Math.Round(TrackHeight * 0.7);
}

/// <summary>
/// [S-3918] The Switch style's ladder. One level per step of the type ramp: the name on that step,
/// the track as high as its line (Windows' 40×20 at Body, never below it, at most 80 high), the
/// icon half again the line, the gaps growing on the 4 px grid. The face takes the
/// largest level that fits the box with icon, name and switch; only where none does it drops the
/// icon, then the name; the state line is added where it fits at that level. A column and a row
/// are both planned and the higher level wins, a tie going to the box's aspect, so the content
/// grows with the tile in steps and never falls to the smallest one while the next still fits.
/// The text is planned for the language's longest state line, so switching moves nothing.
/// </summary>
public static class SwitchFace
{
    /// <summary>Windows' ToggleSwitch track.</summary>
    public const double BaseTrackWidth = 40;

    public const double BaseTrackHeight = 20;

    /// <summary>The tallest track: four times Windows'.</summary>
    public const double MaxTrackHeight = 80;

    /// <summary>The icon over the name's line: a glyph draws inside its em box, so half again reads as the lead.</summary>
    public const double IconShare = 1.5;

    /// <summary>A box this many times as wide as high prefers the row on a tie.</summary>
    public const double RowAspect = 2;

    /// <param name="width">Usable width inside <see cref="ButtonInset.For"/>.</param>
    /// <param name="height">Usable height inside the inset.</param>
    /// <param name="nameChars">Length of the name ("Airplane mode").</param>
    /// <param name="stateChars">Length of the longest state line ("Unavailable").</param>
    public static SwitchFacePlan Plan(double width, double height, int nameChars, int stateChars)
    {
        width = Usable(width);
        height = Usable(height);

        var column = Best(SwitchLayout.Column, width, height, nameChars, stateChars);
        var row = Best(SwitchLayout.Row, width, height, nameChars, stateChars);

        var useRow = row.Rank > column.Rank || (row.Rank == column.Rank && width >= height * RowAspect);
        return useRow ? row.Plan : column.Plan;
    }

    /// <summary>The level's sizes: the name's step, the state a step below, the track, the icon and the gap.</summary>
    public static (TypeStep Name, TypeStep State, double TrackHeight, double Icon, double Gap) Level(int level)
    {
        var ramp = FluentType.Ramp;
        var name = ramp[Math.Clamp(level, 0, ramp.Count - 1)];
        var state = ramp[Math.Clamp(level - 1, 0, ramp.Count - 1)] with { SemiBold = false };
        var track = Math.Clamp(Even(name.LineHeight), BaseTrackHeight, MaxTrackHeight);
        var icon = Math.Max(16, Even(name.LineHeight * IconShare));
        var gap = Math.Clamp(Math.Round(name.LineHeight * 0.4 / 4) * 4, 8, 24);
        return (name, state, track, icon, gap);
    }

    /// <summary>
    /// The best plan of one arrangement, with its rank: which parts it shows (icon and name 3, name
    /// 2, icon 1, the switch alone 0) times the levels, plus the level.
    /// </summary>
    private static (int Rank, SwitchFacePlan Plan) Best(SwitchLayout layout, double width, double height, int nameChars, int stateChars)
    {
        var top = FluentType.Ramp.Count - 1;
        (bool Icon, bool Name, int Tier)[] parts = [(true, true, 3), (false, true, 2), (true, false, 1)];

        foreach (var (icon, name, tier) in parts)
        {
            for (var level = top; level >= 0; level--)
            {
                if (Fits(layout, level, width, height, nameChars, stateChars, icon, name, state: false))
                {
                    var state = name && Fits(layout, level, width, height, nameChars, stateChars, icon, name, state: true);
                    return ((tier * (top + 1)) + level, Place(layout, level, width, height, icon, name, state));
                }
            }
        }

        // Nothing fits but the switch, at Windows' own size (narrowed to the box if it must be).
        return (0, Place(SwitchLayout.Column, 0, width, height, icon: false, name: false, state: false));
    }

    private static bool Fits(SwitchLayout layout, int level, double width, double height, int nameChars, int stateChars, bool icon, bool name, bool state)
    {
        var (nameStep, stateStep, trackHeight, iconSize, gap) = Level(level);
        var trackWidth = trackHeight * 2;
        var nameWidth = name ? FluentType.EstimateWidth(nameStep, nameChars) : 0;
        var stateWidth = state ? FluentType.EstimateWidth(stateStep, stateChars) : 0;
        var iconSide = icon ? iconSize : 0;

        if (layout == SwitchLayout.Row)
        {
            if (!name)
            {
                return false;
            }

            var words = nameStep.LineHeight + (state ? stateStep.LineHeight : 0);
            var tall = Math.Max(Math.Max(iconSide, words), trackHeight);
            var wide = (icon ? iconSide + gap : 0) + Math.Max(nameWidth, stateWidth) + (2 * gap) + trackWidth;
            return tall <= height && wide <= width;
        }

        var stack = (icon ? iconSide + gap : 0)
            + (name ? nameStep.LineHeight + gap : 0)
            + (state ? stateStep.LineHeight + gap : 0)
            + trackHeight;
        var span = Math.Max(Math.Max(iconSide, trackWidth), Math.Max(nameWidth, stateWidth));
        return stack <= height && span <= width;
    }

    /// <summary>Every part's box: a column centred as one group in both axes; a row spanning the width, each part centred on its height.</summary>
    private static SwitchFacePlan Place(SwitchLayout layout, int level, double width, double height, bool icon, bool name, bool state)
    {
        var (nameStep, stateStep, trackHeight, iconSize, gap) = Level(level);
        var trackWidth = Math.Min(trackHeight * 2, Math.Max(BaseTrackWidth, width));
        trackHeight = trackWidth / 2;
        var iconSide = icon ? iconSize : 0;
        TypeStep? shownName = name ? nameStep : null;
        TypeStep? shownState = state ? stateStep : null;

        if (layout == SwitchLayout.Row)
        {
            var words = nameStep.LineHeight + (state ? stateStep.LineHeight : 0);
            var textX = icon ? iconSide + gap : 0;
            var trackX = width - trackWidth;
            var textWidth = Math.Max(0, trackX - (2 * gap) - textX);
            var textY = Snap((height - words) / 2);

            return new SwitchFacePlan(
                layout,
                level,
                gap,
                shownName,
                shownState,
                icon ? new PixelRect(0, Snap((height - iconSide) / 2), iconSide, iconSide) : default,
                new PixelRect(textX, textY, textWidth, nameStep.LineHeight),
                state ? new PixelRect(textX, textY + nameStep.LineHeight, textWidth, stateStep.LineHeight) : default,
                new PixelRect(trackX, Snap((height - trackHeight) / 2), trackWidth, trackHeight));
        }

        var stack = (icon ? iconSide + gap : 0)
            + (name ? nameStep.LineHeight + gap : 0)
            + (state ? stateStep.LineHeight + gap : 0)
            + trackHeight;
        var y = Snap((height - stack) / 2);

        var iconBox = default(PixelRect);
        if (icon)
        {
            iconBox = new PixelRect(Snap((width - iconSide) / 2), y, iconSide, iconSide);
            y += iconSide + gap;
        }

        var nameBox = default(PixelRect);
        if (name)
        {
            nameBox = new PixelRect(0, y, width, nameStep.LineHeight);
            y += nameStep.LineHeight + gap;
        }

        var stateBox = default(PixelRect);
        if (state)
        {
            stateBox = new PixelRect(0, y, width, stateStep.LineHeight);
            y += stateStep.LineHeight + gap;
        }

        var track = new PixelRect(Snap((width - trackWidth) / 2), y, trackWidth, trackHeight);
        return new SwitchFacePlan(layout, level, gap, shownName, shownState, iconBox, nameBox, stateBox, track);
    }

    private static double Even(double size) => Math.Floor(size / 2) * 2;

    private static double Snap(double value) => Math.Max(0, Math.Round(value));

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}

/// <summary>
/// [S-3916] The inset of the toggles' and the power buttons' faces, every style: the card's standard
/// content inset (<c>Fluent.Space4</c>, as every other widget), <c>Fluent.Space2</c> while the tile's
/// short side is under the Compact breakpoint (128), where 16 on each side would take a quarter of it.
/// Tied to the short side alone, so a narrow but tall tile keeps the standard inset.
/// </summary>
public static class ButtonInset
{
    public const double Standard = 16;

    public const double Compact = 8;

    /// <summary>The short side below which the inset tightens: the Compact height breakpoint.</summary>
    public const double CompactSide = 128;

    public static double For(WidgetArea area)
        => area.IsMeasured && Math.Min(area.Width, area.Height) < CompactSide ? Compact : Standard;
}
