using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-14200] A text as it is drawn, for where its ink stands: its words, its font (a family name as XAML names it),
/// size, weight (100..900), its lines' height (<see cref="LineHeight"/>: a family text's block line height, its baseline
/// where XAML puts it, at the font's ascent's share of the line; 0: the font's own line, as a glyph draws in its em box), the
/// width it wraps at (infinite: one line) and its tracking (thousandths of an em, XAML's <c>CharacterSpacing</c>), which
/// moves where it wraps.
/// </summary>
public readonly record struct InkText(string Text, string Font, double FontSize, int Weight, double LineHeight, double Width = double.PositiveInfinity, int Tracking = 0);

/// <summary>
/// [S-14200] Where a text's ink really stands in its box (owner, 2026-11-19, of the tiles and the one-line faces:
/// "Centred is fine"; docs/WIDGET-RULES.md: the bands over and under the face alike): the room over its first ink and
/// under its last, measured on the font's outlines as the text is laid out (its letters above the capitals, its
/// descenders, a glyph's own room in its em box), not the family's model of capitals on the baseline
/// (<see cref="LineInk.Of(TypeStep)"/>), which is off by a descender, an accent or a glyph's padding. The host's meter
/// (DirectWrite), installed once at start; where none is (Core's tests, a design-time host) or it cannot say, the
/// model stands.
/// </summary>
public static class InkMeter
{
    private static Func<InkText, LineInk?>? _meter;

    /// <summary>The room over <paramref name="text"/>'s first ink and under its last in its box, or null: nothing inked (no text), no meter.</summary>
    public static LineInk? Measure(InkText text)
        => string.IsNullOrEmpty(text.Text) || !(text.FontSize > 0) || !double.IsFinite(text.FontSize) ? null : Volatile.Read(ref _meter)?.Invoke(text);

    /// <summary>
    /// The ink of a line of the family's on <paramref name="step"/> saying <paramref name="text"/>: measured, else (no
    /// meter, no text: a planned line saying nothing yet) the model's capitals on the baseline.
    /// </summary>
    public static LineInk Line(TypeStep step, string? text, string font, int weight, double width = double.PositiveInfinity, int tracking = 0)
        => Measure(new InkText(text ?? string.Empty, font, step.FontSize, weight, step.LineHeight, width, tracking)) ?? LineInk.Of(step);

    /// <summary>The host's meter. Once per process.</summary>
    internal static void Install(Func<InkText, LineInk?> meter)
    {
        ArgumentNullException.ThrowIfNull(meter);
        if (Interlocked.CompareExchange(ref _meter, meter, null) is not null)
        {
            throw new InvalidOperationException("the ink meter is installed once per process");
        }
    }
}
