using Themia.Core.Widgets;

namespace Themia.Sdk.Kit;

/// <summary>
/// [S-6700] The family's type: nine sizes, two weights, every widget. Caption 12/16, Body 14/20
/// (BodyStrong at 600), Title 20/28, the Readout steps Big 28/36, 40/52 and 68/92 (600), and the
/// Hero steps 96/128, 136/180 and 192/252 (600) that only a one-readout widget uses (a clock, a
/// timer, a quick toggle, a power button: <see cref="FamilyShape"/>'s Tile).
/// <para>
/// A widget used to compute its readout as a fraction of the box (<c>FluentType.Hero</c>,
/// <c>Fit</c>, <c>Scaled</c>), and the desktop ended up with 112 different text sizes on it. A
/// step is chosen now, never computed: <see cref="ReadoutRules"/> picks the largest step whose line
/// fits, and the value's unit and the line under it follow from the step (<see cref="Unit"/>,
/// <see cref="Under"/>), so two widgets at the same size read the same.
/// </para>
/// <para>
/// [S-821] Every step is the base size times the user's text size (<see cref="FluentType.TextScale"/>),
/// the same factor the rest of the ramp follows, so a chooser still picks among real steps at 150 %.
/// </para>
/// </summary>
public static class FamilyType
{
    /// <summary>The two weights the family draws: regular and semibold, nothing lighter or bolder.</summary>
    public const int RegularWeight = 400;

    public const int StrongWeight = 600;

    /// <summary>The unscaled sizes and their lines, in order: what <see cref="Sizes"/> lists.</summary>
    public const double CaptionSize = 12, CaptionLine = 16;

    public const double BodySize = 14, BodyLine = 20;

    public const double TitleSize = 20, TitleLine = 28;

    /// <summary>The Readout steps, smallest first (size, line), all semibold.</summary>
    private static readonly (double Size, double Line)[] BigBase = [(28, 36), (40, 52), (68, 92)];

    /// <summary>The Hero steps, smallest first (size, line), all semibold: one-readout widgets only.</summary>
    private static readonly (double Size, double Line)[] HeroBase = [(96, 128), (136, 180), (192, 252)];

    /// <summary>The nine sizes the family draws text in, unscaled, smallest first.</summary>
    public static IReadOnlyList<double> Sizes { get; } = [CaptionSize, BodySize, TitleSize, 28, 40, 68, 96, 136, 192];

    /// <summary>The largest Readout step's size: a data widget's number caps here ([S-6702]).</summary>
    public const double BigCap = 68;

    /// <summary>12/16. Units beside a 28, timestamps, a row's second and third line, chips, a cell's name.</summary>
    public static TypeStep Caption => At(CaptionSize, CaptionLine, false);

    /// <summary>14/20. Every row's name and value, sentences, buttons.</summary>
    public static TypeStep Body => At(BodySize, BodyLine, false);

    /// <summary>14/20 at 600. The header's name and the one emphasised row (today, unread).</summary>
    public static TypeStep BodyStrong => At(BodySize, BodyLine, true);

    /// <summary>20/28 at 600. The one prominent line of a widget that has one (a note's title, a track).</summary>
    public static TypeStep Title => At(TitleSize, TitleLine, true);

    /// <summary>The Readout steps at the live text size: 28/36, 40/52, 68/92, semibold.</summary>
    public static IReadOnlyList<TypeStep> Big => [.. BigBase.Select(s => At(s.Size, s.Line, true))];

    /// <summary>The Hero steps at the live text size: 96/128, 136/180, 192/252, semibold.</summary>
    public static IReadOnlyList<TypeStep> Hero => [.. HeroBase.Select(s => At(s.Size, s.Line, true))];

    /// <summary>The smallest Readout step, 28/36: a readout's floor at every size.</summary>
    public static TypeStep Big28 => At(28, 36, true);

    public static TypeStep Big40 => At(40, 52, true);

    public static TypeStep Big68 => At(68, 92, true);

    public static TypeStep Hero96 => At(96, 128, true);

    public static TypeStep Hero136 => At(136, 180, true);

    public static TypeStep Hero192 => At(192, 252, true);

    /// <summary>
    /// [S-6701] The unit set right after a value on <paramref name="value"/>'s step, regular weight in
    /// the secondary colour: 28 → 14, 40 → 20, 68 → 28, 96 and 136 → 40, 192 → 68 (each with its own
    /// line). A step that is not a Readout or Hero step gets Caption.
    /// </summary>
    public static TypeStep Unit(TypeStep value) => BaseOf(value) switch
    {
        28 => Regular(Body),
        40 => Regular(Title),
        68 => Regular(Big28),
        96 or 136 => Regular(Big40),
        192 => Regular(Big68),
        _ => Caption,
    };

    /// <summary>
    /// [S-6701] The one line under a value on <paramref name="value"/>'s step, regular weight: 28 →
    /// Caption, 40 and 68 → Body, a Hero step → Title (at 400). Anything else gets Caption.
    /// </summary>
    public static TypeStep Under(TypeStep value) => BaseOf(value) switch
    {
        40 or 68 => Body,
        96 or 136 or 192 => Regular(Title),
        _ => Caption,
    };

    /// <summary>The same size and line at 400.</summary>
    public static TypeStep Regular(TypeStep step) => step with { SemiBold = false };

    /// <summary>Whether <paramref name="step"/> is one of the Hero steps (at the live text size).</summary>
    public static bool IsHero(TypeStep step) => BaseOf(step) >= 96;

    /// <summary>
    /// The base size a step stands for, undoing the live text size: 28 for a 42 at 150 %. A step off
    /// the family's sizes answers its own size.
    /// </summary>
    public static double BaseOf(TypeStep step)
    {
        var size = step.FontSize / FluentType.TextScale;
        foreach (var known in Sizes)
        {
            if (Math.Abs(known - size) < 0.01)
            {
                return known;
            }
        }

        return size;
    }

    /// <summary>
    /// [S-6700] Whether text drawn at <paramref name="fontSize"/> (unscaled, as a render at 100 %
    /// draws it) is one of the family's sizes; Hero sizes count only for a one-readout widget.
    /// </summary>
    public static bool IsSize(double fontSize, bool oneReadout)
    {
        foreach (var known in Sizes)
        {
            if (Math.Abs(known - fontSize) < 0.01)
            {
                return oneReadout || known <= BigCap;
            }
        }

        return false;
    }

    /// <summary>The line the family pairs with <paramref name="fontSize"/> (unscaled), or 0 for a size it does not draw.</summary>
    public static double LineOf(double fontSize)
    {
        (double Size, double Line)[] all = [(CaptionSize, CaptionLine), (BodySize, BodyLine), (TitleSize, TitleLine), .. BigBase, .. HeroBase];
        foreach (var (size, line) in all)
        {
            if (Math.Abs(size - fontSize) < 0.01)
            {
                return line;
            }
        }

        return 0;
    }

    private static TypeStep At(double size, double line, bool strong)
    {
        var scale = FluentType.TextScale;
        return new TypeStep(size * scale, line * scale, strong);
    }
}
