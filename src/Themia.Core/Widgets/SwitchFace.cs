namespace Themia.Core.Widgets;

/// <summary>[S-3918] How the Switch style arranges its parts.</summary>
public enum SwitchLayout
{
    /// <summary>XS: the toggle's icon over the switch; the name does not fit, the icon says which it is.</summary>
    IconOver = 0,

    /// <summary>A tile or a column: the name (an icon above it where the height allows), the switch and its state under it.</summary>
    Stacked = 1,

    /// <summary>A wide strip, as a Settings row: icon and name at the left, the state and the switch at the right.</summary>
    Row = 2,
}

/// <summary>
/// [S-3918] How a toggle's Switch style draws itself in its box.
/// </summary>
/// <param name="Layout">Which arrangement.</param>
/// <param name="TrackWidth">The switch's track: Windows' 40×20 times a half-step scale, 1 to 3.</param>
/// <param name="TrackHeight">Half the track's width.</param>
/// <param name="IconSize">The toggle's icon (even), or 0 for none.</param>
/// <param name="Name">The name's ramp step, or null (XS).</param>
/// <param name="State">The state line's step ("On", "Off", "Unavailable"), or null where it does not fit.</param>
public readonly record struct SwitchFacePlan(SwitchLayout Layout, double TrackWidth, double TrackHeight, double IconSize, TypeStep? Name, TypeStep? State)
{
    /// <summary>The knob's diameter at rest: Windows' 12 in a 20-high track.</summary>
    public double Knob => Math.Round(TrackHeight * 0.6);

    /// <summary>The knob's diameter under the pointer: Windows' 14.</summary>
    public double KnobHover => Math.Round(TrackHeight * 0.7);
}

/// <summary>
/// [S-3918] The Switch style's ladder: the icon over the switch at XS; then the name with the switch
/// and its state line under it, an icon above the name where the height has room for one; in a wide
/// strip a Settings row, icon and name left, state and switch right. The switch grows with the name
/// (its track as high as the name's line, in half steps of Windows' 40×20), and the text is planned
/// for the language's longest state line, so switching moves nothing.
/// </summary>
public static class SwitchFace
{
    /// <summary>Windows' ToggleSwitch track.</summary>
    public const double BaseTrackWidth = 40;

    public const double BaseTrackHeight = 20;

    public const double MaxScale = 3;

    /// <summary>Between the parts: icon and name, name and switch, switch and state.</summary>
    public const double Gap = 8;

    /// <summary>The smallest icon worth drawing above the name.</summary>
    public const double MinIcon = 24;

    public const double MaxIcon = 96;

    /// <param name="area">The widget's content area (classes decide the rung).</param>
    /// <param name="width">Usable width inside the inset.</param>
    /// <param name="height">Usable height inside the inset.</param>
    /// <param name="nameChars">Length of the name ("Airplane mode").</param>
    /// <param name="stateChars">Length of the longest state line ("Unavailable").</param>
    public static SwitchFacePlan Plan(WidgetArea area, double width, double height, int nameChars, int stateChars)
    {
        width = Usable(width);
        height = Usable(height);

        if (!area.IsMeasured || area.WidthClass == SizeClass.Compact)
        {
            return IconOver(width, height);
        }

        if (area.Shape == AreaShape.Wide)
        {
            return RowPlan(width, height, nameChars, stateChars);
        }

        var floor = FluentType.EstimateWidth(FluentType.Body, nameChars) <= width ? FluentType.Body : FluentType.Caption;
        var name = FluentType.Fit(width, height / 6, nameChars, floor, FluentType.TitleLarge);
        var scale = ScaleFor(name.LineHeight, width / 2);
        var (trackWidth, trackHeight) = (BaseTrackWidth * scale, BaseTrackHeight * scale);

        var words = name.LineHeight + Gap + trackHeight;
        if (words > height)
        {
            return IconOver(width, height);
        }

        var state = Smaller(name);
        TypeStep? shown = trackWidth + Gap + FluentType.EstimateWidth(state, stateChars) <= width ? state : null;

        // An icon above the name only where it is worth drawing: the widget's own part, never filler.
        var iconRoom = Math.Min((height - words - Gap) * 0.6, width * 0.3);
        var icon = iconRoom >= MinIcon ? Even(Math.Min(iconRoom, MaxIcon)) : 0;
        return new SwitchFacePlan(SwitchLayout.Stacked, trackWidth, trackHeight, icon, name, shown);
    }

    /// <summary>XS: the icon over a switch at Windows' own size or a half step up.</summary>
    private static SwitchFacePlan IconOver(double width, double height)
    {
        var scale = ScaleFor(height * 0.25, width * 0.7);
        var (trackWidth, trackHeight) = (BaseTrackWidth * scale, BaseTrackHeight * scale);
        var icon = Even(Math.Clamp(Math.Min((height - trackHeight - Gap) * 0.6, width * 0.45), 16, MaxIcon));
        return new SwitchFacePlan(SwitchLayout.IconOver, trackWidth, trackHeight, icon, null, null);
    }

    /// <summary>A strip: icon and name at the left, state and switch at the right; the state goes first, then the icon.</summary>
    private static SwitchFacePlan RowPlan(double width, double height, int nameChars, int stateChars)
    {
        var name = FluentType.Fit(width / 2, height / 3, nameChars, FluentType.Caption, FluentType.TitleLarge);
        var scale = ScaleFor(name.LineHeight, width / 4);
        var (trackWidth, trackHeight) = (BaseTrackWidth * scale, BaseTrackHeight * scale);
        var state = Smaller(name);

        var nameWidth = FluentType.EstimateWidth(name, nameChars);
        var icon = Even(Math.Clamp(name.LineHeight, 16, MaxIcon));
        var stateWidth = FluentType.EstimateWidth(state, stateChars);

        var full = icon + Gap + nameWidth + (2 * Gap) + stateWidth + Gap + trackWidth;
        TypeStep? shown = full <= width ? state : null;
        var used = shown is null ? full - stateWidth - Gap : full;
        var drawnIcon = used <= width ? icon : 0;
        return new SwitchFacePlan(SwitchLayout.Row, trackWidth, trackHeight, drawnIcon, name, shown);
    }

    /// <summary>The track as high as <paramref name="line"/>, in half steps from 1 to <see cref="MaxScale"/>, never wider than <paramref name="maxWidth"/>.</summary>
    private static double ScaleFor(double line, double maxWidth)
    {
        var byLine = Math.Floor(line / BaseTrackHeight * 2) / 2;
        var byWidth = Math.Floor(maxWidth / BaseTrackWidth * 2) / 2;
        return Math.Clamp(Math.Min(byLine, byWidth), 1, MaxScale);
    }

    /// <summary>The state reads one step below the name, never below Caption, never semibold.</summary>
    private static TypeStep Smaller(TypeStep name)
    {
        var below = FluentType.Caption;
        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize < name.FontSize && step.FontSize > below.FontSize)
            {
                below = step;
            }
        }

        return below with { SemiBold = false };
    }

    private static double Even(double size) => Math.Floor(size / 2) * 2;

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
