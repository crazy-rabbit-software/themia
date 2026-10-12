namespace Themia.Core.Widgets;

/// <summary>
/// [S-3906] How a toggle widget draws itself in its box.
/// </summary>
/// <param name="Row">Icon left of the words (a wide strip) rather than above them.</param>
/// <param name="IconSize">The glyph's font size: even, so it sits on whole pixels at 100%.</param>
/// <param name="Name">The name's ramp step, or null for the icon alone.</param>
/// <param name="State">The state line's step ("On", "Off", "Unavailable"), or null where it does not fit.</param>
public readonly record struct ToggleFacePlan(bool Row, double IconSize, TypeStep? Name, TypeStep? State);

/// <summary>
/// [S-3906] The toggle widget's ladder: the icon alone while the box is Compact in width (XS); the icon
/// with its name under it; the state line under the name where the height allows; and in a wide strip
/// the icon beside the name and state, as a Windows 11 quick-settings tile reads. Text is planned for
/// the longest state line of the language, so switching never moves anything.
/// </summary>
public static class ToggleFace
{
    /// <summary>Space between the icon and the words, and between the name and the state.</summary>
    public const double Gap = 8;

    public const double MinIcon = 16;

    public const double MaxIcon = 128;

    /// <param name="area">The widget's content area (classes decide the rung).</param>
    /// <param name="width">Usable width inside the inset.</param>
    /// <param name="height">Usable height inside the inset.</param>
    /// <param name="nameChars">Length of the name ("Airplane mode").</param>
    /// <param name="stateChars">Length of the longest state line ("Unavailable").</param>
    public static ToggleFacePlan Plan(WidgetArea area, double width, double height, int nameChars, int stateChars)
    {
        width = Usable(width);
        height = Usable(height);

        if (!area.IsMeasured || area.WidthClass == SizeClass.Compact)
        {
            return new ToggleFacePlan(false, Even(Math.Min(height * 0.6, width * 0.5)), null, null);
        }

        if (area.Shape == AreaShape.Wide)
        {
            return RowPlan(width, height, nameChars, stateChars);
        }

        // Stacked: about a sixth of the height for the name, as large as the width lets it be.
        var floor = FluentType.EstimateWidth(FluentType.Body, nameChars) <= width ? FluentType.Body : FluentType.Caption;
        var name = FluentType.Fit(width, height / 6, nameChars, floor, FluentType.Title);
        var state = Smaller(name);
        var words = name.LineHeight + Gap;
        if (words + MinIcon > height)
        {
            return new ToggleFacePlan(false, Even(Math.Min(height * 0.6, width * 0.5)), null, null);
        }

        TypeStep? shown = null;
        if (words + state.LineHeight + Gap + MinIcon <= height && FluentType.EstimateWidth(state, stateChars) <= width)
        {
            shown = state;
            words += state.LineHeight + Gap;
        }

        var icon = Even(Math.Min((height - words) * 0.75, width * 0.5));
        return new ToggleFacePlan(false, icon, name, shown);
    }

    /// <summary>A strip: the icon at the left, the name with the state under it at its right.</summary>
    private static ToggleFacePlan RowPlan(double width, double height, int nameChars, int stateChars)
    {
        var icon = Even(Math.Min(height * 0.55, width * 0.25));
        var room = Math.Max(0, width - icon - (2 * Gap));
        var name = FluentType.Fit(room, height / 3, nameChars, FluentType.Caption, FluentType.Title);
        var state = Smaller(name);
        TypeStep? shown = name.LineHeight + state.LineHeight <= height && FluentType.EstimateWidth(state, stateChars) <= room
            ? state
            : null;
        return new ToggleFacePlan(true, icon, name, shown);
    }

    /// <summary>The state reads one step below the name, never below Caption.</summary>
    private static TypeStep Smaller(TypeStep name)
    {
        var ramp = FluentType.Ramp;
        var below = FluentType.Caption;
        foreach (var step in ramp)
        {
            if (step.FontSize < name.FontSize && step.FontSize > below.FontSize)
            {
                below = step;
            }
        }

        return below with { SemiBold = false };
    }

    private static double Even(double size) => Math.Clamp(Math.Floor(size / 2) * 2, MinIcon, MaxIcon);

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
