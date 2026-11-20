using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-14200] A text as it is drawn, for where its ink stands: its words, its font (a family name as XAML names it),
/// size, weight (100..900), its lines' height (<see cref="LineHeight"/>: a family text's block line height, its baseline
/// where XAML puts it, at the font's ascent's share of the line; 0: the font's own line, as a glyph draws in its em box), the
/// width it wraps at (infinite: one line), its tracking (thousandths of an em, XAML's <c>CharacterSpacing</c>), which
/// moves where it wraps, and the most lines it shows (0: all; past them XAML cuts the last shown line with its ellipsis).
/// </summary>
public readonly record struct InkText(string Text, string Font, double FontSize, int Weight, double LineHeight, double Width = double.PositiveInfinity, int Tracking = 0, int MaxLines = 0);

/// <summary>
/// [S-14200] [S-14240] The room in a text's box around its ink: over its first ink and under its last
/// (<see cref="Line"/>), and before its first ink and after its last across, left and right as it is laid out left to
/// right (a figure's side bearings, a glyph's room in its em box: the memory chip's none on the left, the battery's an
/// eighth).
/// </summary>
public readonly record struct InkRoom(double Over, double Under, double Left, double Right)
{
    /// <summary>The room over its first ink and under its last.</summary>
    public LineInk Line => new(Over, Under);

    /// <summary>The room on its start side: its left, or right to left its right.</summary>
    public double Start(bool rightToLeft) => rightToLeft ? Right : Left;

    /// <summary>
    /// [S-14247] How far a line's box reaches back over the room its ink leaves at its start and its end: its
    /// <paramref name="first"/> run's (the value's) and its <paramref name="last"/> run's (its delta's, else its unit's, else
    /// the value's), as measured. Left to right the first run starts the line and the last ends it; right to left the runs'
    /// order across follows their own direction (figures stay left to right), so each side takes the least either run
    /// leaves there, never reaching past the ink. A line <paramref name="led"/> by a glyph keeps its start (the gap to the
    /// glyph is from its box). Not measured (no meter): its box, 0 either side.
    /// </summary>
    public static (double Start, double End) Ends(InkRoom? first, InkRoom? last, bool rightToLeft, bool led)
    {
        if (first is not { } value || (last ?? first) is not { } end)
        {
            return (0, 0);
        }

        var start = rightToLeft ? Math.Min(value.Right, end.Right) : value.Left;
        var finish = rightToLeft ? Math.Min(value.Left, end.Left) : end.Right;
        return (led ? 0 : Room(start), Room(finish));

        static double Room(double v) => double.IsFinite(v) ? Math.Max(0, v) : 0;
    }
}

/// <summary>
/// [S-14200] Where a text's ink really stands in its box (owner, 2026-11-19, of the tiles and the one-line faces:
/// "Centred is fine"; docs/WIDGET-RULES.md: the bands over and under the face alike): the room over its first ink and
/// under its last, measured on the font's outlines as the text is laid out (its letters above the capitals, its
/// descenders, a glyph's own room in its em box), not the family's model of capitals on the baseline
/// (<see cref="LineInk.Of(TypeStep)"/>), which is off by a descender, an accent or a glyph's padding; [S-14240] and the
/// room before its first ink and after its last across (<see cref="Room"/>). The host's meter (DirectWrite), installed
/// once at start; where none is (Core's tests, a design-time host) or it cannot say, the model stands: capitals on the
/// baseline, and across the text's box (no room either side).
/// </summary>
public static class InkMeter
{
    private static Func<InkText, InkRoom?>? _meter;

    /// <summary>The room over <paramref name="text"/>'s first ink and under its last in its box, or null: nothing inked (no text), no meter.</summary>
    public static LineInk? Measure(InkText text) => Room(text)?.Line;

    /// <summary>[S-14240] The room around <paramref name="text"/>'s ink in its box, or null: nothing inked (no text), no meter.</summary>
    public static InkRoom? Room(InkText text)
        => string.IsNullOrEmpty(text.Text) || !(text.FontSize > 0) || !double.IsFinite(text.FontSize) ? null : Volatile.Read(ref _meter)?.Invoke(text);

    /// <summary>
    /// The ink of a line of the family's on <paramref name="step"/> saying <paramref name="text"/>: measured, else (no
    /// meter, no text: a planned line saying nothing yet) the model's capitals on the baseline.
    /// </summary>
    public static LineInk Line(TypeStep step, string? text, string font, int weight, double width = double.PositiveInfinity, int tracking = 0, int maxLines = 0)
        => Measure(new InkText(text ?? string.Empty, font, step.FontSize, weight, step.LineHeight, width, tracking, maxLines)) ?? LineInk.Of(step);

    /// <summary>The host's meter. Once per process.</summary>
    internal static void Install(Func<InkText, InkRoom?> meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        if (Interlocked.CompareExchange(ref _meter, meter, null) is not null)
        {
            throw new InvalidOperationException("the ink meter is installed once per process");
        }
    }
}
