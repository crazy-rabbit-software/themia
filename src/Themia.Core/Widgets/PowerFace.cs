namespace Themia.Core.Widgets;

/// <summary>
/// [S-1912] How a power widget draws itself in its box.
/// </summary>
/// <param name="IconBox">
/// The square that holds the icon and, while armed, the draining ring around it. Fixed for a box,
/// so arming never moves anything.
/// </param>
/// <param name="IconSize">The icon's font size, centred in <see cref="IconBox"/>.</param>
/// <param name="Label">The name's ramp step, or null for icon only (XS).</param>
/// <param name="Armed">The step of "Click again to shut down" in the label's place.</param>
/// <param name="ArmedLines">How many lines that sentence may wrap to (1 or 2).</param>
/// <param name="LabelSlot">
/// The height kept under the icon for the label: the taller of the name and the armed sentence, so
/// the face does not jump when it arms. 0 with no label.
/// </param>
public readonly record struct PowerFacePlan(double IconBox, double IconSize, TypeStep? Label, TypeStep Armed, int ArmedLines, double LabelSlot);

/// <summary>
/// [S-1912] The power widget's ladder, in two rungs: the icon alone while the box is Compact in width
/// (XS), then the icon with its name underneath. The icon grows with the box (the ring around it
/// is part of the same square), and the name takes a ramp step that fits the width; both are
/// measured on the box inside the inset.
/// </summary>
public static class PowerFace
{
    /// <summary>Space between the icon's square and the label.</summary>
    public const double LabelGap = 8;

    /// <summary>The smallest and largest icon square.</summary>
    public const double MinBox = 24;

    public const double MaxBox = 192;

    /// <summary>The icon over its square: the rest is the ring and the air round it.</summary>
    public const double IconShare = 1 / 1.5;

    /// <summary>
    /// Plan the face for a box.
    /// </summary>
    /// <param name="area">The widget's content area (classes decide the rung).</param>
    /// <param name="width">Usable width inside the inset.</param>
    /// <param name="height">Usable height inside the inset.</param>
    /// <param name="labelChars">Length of the name ("Shut down").</param>
    /// <param name="armedChars">Length of the armed sentence; 0 for an action that never arms.</param>
    public static PowerFacePlan Plan(WidgetArea area, double width, double height, int labelChars, int armedChars)
    {
        width = Usable(width);
        height = Usable(height);

        TypeStep? label = null;
        var armed = FluentType.Caption;
        var armedLines = 1;
        var slot = 0.0;

        if (area.IsMeasured && area.WidthClass >= SizeClass.Regular)
        {
            // About a sixth of the height for the name, as large as the width lets it be, Body to Title.
            var step = FluentType.Fit(width, height / 6, labelChars, FluentType.Body, FluentType.Title);
            var needed = step.LineHeight + LabelGap + MinBox;

            if (needed <= height && FluentType.EstimateWidth(FluentType.Body, labelChars) <= width)
            {
                label = step;
                slot = step.LineHeight;

                if (armedChars > 0)
                {
                    // The sentence takes the name's step if it fits on a line, else a smaller one, else
                    // wraps to two Caption lines.
                    armed = FluentType.Fit(width, step.LineHeight, armedChars, FluentType.Caption, step);
                    if (FluentType.EstimateWidth(armed, armedChars) > width)
                    {
                        armed = FluentType.Caption;
                        armedLines = 2;
                    }

                    var armedHeight = armed.LineHeight * armedLines;
                    if (armedHeight + LabelGap + MinBox <= height)
                    {
                        slot = Math.Max(slot, armedHeight);
                    }
                    else
                    {
                        // No room for two lines: one, trimmed; the tooltip carries the rest.
                        armedLines = 1;
                    }
                }
            }
        }

        var room = label is null ? height : height - slot - LabelGap;
        var box = Math.Clamp(Math.Floor(Math.Min(room * 0.8, width * 0.6)), MinBox, MaxBox);

        // Even sizes keep the glyph on whole pixels at 100% scaling.
        var icon = Math.Max(16, Math.Floor(box * IconShare / 2) * 2);
        return new PowerFacePlan(box, icon, label, armed, armedLines, slot);
    }

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
