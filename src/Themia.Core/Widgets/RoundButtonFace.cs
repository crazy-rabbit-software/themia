namespace Themia.Core.Widgets;

/// <summary>
/// [S-3917] [S-1919] How a round button (a toggle's or a power widget's Round style) draws itself.
/// </summary>
/// <param name="Diameter">The circle's diameter: even, fixed for a box, so arming or switching moves nothing.</param>
/// <param name="IconSize">The icon's font size, centred in the circle: even.</param>
/// <param name="Label">The name's ramp step under the circle, or null for the circle alone.</param>
/// <param name="Sentence">The step of a sentence in the name's place (a power widget's "Click again to shut down").</param>
/// <param name="SentenceLines">How many lines that sentence may wrap to (1 or 2).</param>
/// <param name="LabelSlot">The height kept under the circle: the taller of the name and the sentence; 0 with no label.</param>
public readonly record struct RoundButtonPlan(double Diameter, double IconSize, TypeStep? Label, TypeStep Sentence, int SentenceLines, double LabelSlot);

/// <summary>
/// [S-3917] [S-1919] The round button's ladder, one for the toggles and the power widgets: the circle
/// alone while the box is Compact in width (XS), then the circle with the name under it on a step
/// fitted to the width (Caption to Subtitle, about a sixth of the height). The circle takes the rest
/// of the box (the core content, so L and XL get a bigger button, never more parts), the icon two
/// fifths of it, as a Windows round button holds its glyph.
/// </summary>
public static class RoundButtonFace
{
    /// <summary>Space between the circle and the name.</summary>
    public const double LabelGap = 8;

    /// <summary>The smallest and largest circle.</summary>
    public const double MinDiameter = 32;

    public const double MaxDiameter = 320;

    /// <summary>The icon over the circle's diameter.</summary>
    public const double IconShare = 0.4;

    /// <summary>The circle over the room it has: the rest is air, so the button reads as a button.</summary>
    public const double Fill = 0.85;

    /// <param name="area">The widget's content area (classes decide the rung).</param>
    /// <param name="width">Usable width inside the inset.</param>
    /// <param name="height">Usable height inside the inset.</param>
    /// <param name="labelChars">Length of the name ("Airplane mode", "Shut down").</param>
    /// <param name="sentenceChars">Length of a sentence that can take the name's place; 0 for none.</param>
    public static RoundButtonPlan Plan(WidgetArea area, double width, double height, int labelChars, int sentenceChars = 0)
    {
        width = Usable(width);
        height = Usable(height);

        TypeStep? label = null;
        var sentence = FluentType.Caption;
        var sentenceLines = 1;
        var slot = 0.0;

        if (area.IsMeasured && area.WidthClass >= SizeClass.Regular)
        {
            var floor = FluentType.EstimateWidth(FluentType.Body, labelChars) <= width ? FluentType.Body : FluentType.Caption;
            var step = FluentType.Fit(width, height / 6, labelChars, floor, FluentType.Subtitle);

            if (step.LineHeight + LabelGap + MinDiameter <= height && FluentType.EstimateWidth(FluentType.Caption, labelChars) <= width)
            {
                label = step;
                slot = step.LineHeight;

                if (sentenceChars > 0)
                {
                    // The sentence takes the name's step if it fits on a line, else a smaller one, else
                    // wraps to two Caption lines; one trimmed line where two do not fit.
                    sentence = FluentType.Fit(width, step.LineHeight, sentenceChars, FluentType.Caption, step);
                    if (FluentType.EstimateWidth(sentence, sentenceChars) > width)
                    {
                        sentence = FluentType.Caption;
                        sentenceLines = 2;
                    }

                    var tall = sentence.LineHeight * sentenceLines;
                    if (tall + LabelGap + MinDiameter <= height)
                    {
                        slot = Math.Max(slot, tall);
                    }
                    else
                    {
                        sentenceLines = 1;
                    }
                }
            }
        }

        var room = label is null ? height : height - slot - LabelGap;
        var diameter = Even(Math.Clamp(Math.Min(room, width) * Fill, MinDiameter, MaxDiameter));
        var icon = Math.Max(16, Math.Floor(diameter * IconShare / 2) * 2);
        return new RoundButtonPlan(diameter, icon, label, sentence, sentenceLines, slot);
    }

    private static double Even(double size) => Math.Floor(size / 2) * 2;

    private static double Usable(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
