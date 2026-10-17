namespace Themia.Core.Widgets;

/// <summary>
/// One step of the Windows 11 type ramp: a size and the line height that goes with it.
/// </summary>
/// <param name="FontSize">Size in device-independent pixels.</param>
/// <param name="LineHeight">The line height Fluent pairs with that size.</param>
/// <param name="SemiBold">Whether the step is drawn semibold, as Fluent specifies.</param>
public readonly record struct TypeStep(double FontSize, double LineHeight, bool SemiBold);

/// <summary>
/// The Windows 11 type ramp, and how to pick a step that fits a box.
/// <para>
/// Widgets used to compute a font size as an arbitrary fraction of the tile — 0.26 of the width,
/// clamped between 16 and 88 — which is how the original web app did it and is why the result looked
/// like a web dashboard rather than like Windows. Fluent is a <b>ramp</b>: a fixed set of sizes, each
/// with its own line height and weight, and text that is not on one of them looks foreign next to
/// everything else on the desktop.
/// </para>
/// <para>
/// So widgets still scale to their tile — that is what a resizable widget has to do — but they scale
/// by <i>choosing a step</i> rather than by inventing a number. A tile that grows moves its readout
/// from Subtitle to Title to Display, and every size on screen is one Windows itself uses.
/// </para>
/// </summary>
public static class FluentType
{
    /// <summary>
    /// [S-821] The user's text size: every step below is the ramp's own size times this. The ramp is
    /// scaled as a whole, so <see cref="Fit"/> still chooses among real steps and a layout that
    /// fitted at 100% sheds a line rather than overflowing at 150%. Set only by the appearance
    /// engine, on the UI thread; see <see cref="SetTextScale"/>.
    /// </summary>
    public static double TextScale { get; private set; } = 1.0;

    /// <summary>The smallest and largest text size the setting allows (and the store clamps to).</summary>
    public const double MinTextScale = 0.5;

    public const double MaxTextScale = 2.0;

    /// <summary>The unscaled Windows 11 ramp, smallest first.</summary>
    private static readonly TypeStep[] BaseRamp =
    [
        new(12, 16, false),  // Caption
        new(14, 20, false),  // Body
        new(14, 20, true),   // BodyStrong
        new(18, 24, false),  // BodyLarge
        new(20, 28, true),   // Subtitle
        new(28, 36, true),   // Title
        new(40, 52, true),   // TitleLarge
        new(68, 92, true),   // Display
    ];

    private static TypeStep[] _steps = StepsAt(1.0);

    /// <summary>
    /// [S-821] The ramp at a text scale, in <see cref="BaseRamp"/> order. Pure, so the scaling is
    /// testable without touching the live <see cref="TextScale"/>. An unusable scale is 1; a usable
    /// one is clamped to the setting's range.
    /// </summary>
    public static TypeStep[] StepsAt(double scale)
    {
        var factor = ClampTextScale(scale);
        var steps = new TypeStep[BaseRamp.Length];

        for (var i = 0; i < BaseRamp.Length; i++)
        {
            var step = BaseRamp[i];
            steps[i] = new TypeStep(step.FontSize * factor, step.LineHeight * factor, step.SemiBold);
        }

        return steps;
    }

    /// <summary>The setting's value made safe: not finite or not positive is 1, the rest clamped.</summary>
    public static double ClampTextScale(double scale)
        => double.IsFinite(scale) && scale > 0 ? Math.Clamp(scale, MinTextScale, MaxTextScale) : 1.0;

    /// <summary>
    /// Change the live text scale. Returns whether it changed, so the caller knows whether text
    /// already on screen has to follow. App code only: tests use <see cref="StepsAt"/>.
    /// </summary>
    public static bool SetTextScale(double scale)
    {
        var next = ClampTextScale(scale);
        if (Math.Abs(next - TextScale) < 0.0001)
        {
            return false;
        }

        TextScale = next;
        var steps = StepsAt(next);
        _steps = steps;
        _ramp = [steps[0], steps[1], steps[3], steps[4], steps[5], steps[6], steps[7]];
        return true;
    }

    /// <summary>12/16. Labels, timestamps, secondary metadata.</summary>
    public static TypeStep Caption => _steps[0];

    /// <summary>14/20. The default for body text.</summary>
    public static TypeStep Body => _steps[1];

    /// <summary>14/20 semibold. A body line that needs emphasis, and the standard list-row title.</summary>
    public static TypeStep BodyStrong => _steps[2];

    /// <summary>18/24. A larger body line.</summary>
    public static TypeStep BodyLarge => _steps[3];

    /// <summary>20/28 semibold. Section headings.</summary>
    public static TypeStep Subtitle => _steps[4];

    /// <summary>28/36 semibold. Page titles.</summary>
    public static TypeStep Title => _steps[5];

    /// <summary>40/52 semibold.</summary>
    public static TypeStep TitleLarge => _steps[6];

    /// <summary>68/92 semibold. The largest step Fluent defines.</summary>
    public static TypeStep Display => _steps[7];

    /// <summary>
    /// The same step, semibold. <see cref="Fit"/> can only return ramp entries and the ramp holds
    /// one semibold step below Subtitle, so a fitted readout that needs weight has no other way
    /// to ask for it.
    /// </summary>
    public static TypeStep Strong(TypeStep step) => step with { SemiBold = true };

    /// <summary>
    /// Roughly how wide <paramref name="characters"/> characters are at a step.
    /// <para>
    /// An estimate, not a measurement: 0.58em is a workable average advance for Segoe UI's digits
    /// and lower case. It only has to be close, because callers use it to choose a ramp step and
    /// being slightly out changes nothing until it crosses a whole step.
    /// </para>
    /// </summary>
    private static double Advance(TypeStep step, double characters)
        => step.FontSize * AverageAdvance * Math.Max(1, characters);

    /// <summary>The average character advance, as a fraction of the font size.</summary>
    private const double AverageAdvance = 0.58;

    /// <summary>
    /// The same width estimate <see cref="Fit"/> chooses steps with, for a layout decision that has
    /// to agree with it — whether a caption still fits beside a fitted reading, say.
    /// </summary>
    public static double EstimateWidth(TypeStep step, double characters) => Advance(step, characters);

    private static TypeStep[] _ramp = [_steps[0], _steps[1], _steps[3], _steps[4], _steps[5], _steps[6], _steps[7]];

    /// <summary>The ramp in order, smallest first, at the live <see cref="TextScale"/>.</summary>
    public static IReadOnlyList<TypeStep> Ramp => _ramp;

    /// <summary>
    /// The largest step whose line fits the box, never smaller than <paramref name="floor"/> and
    /// never larger than <paramref name="ceiling"/>.
    /// <para>
    /// The fit is measured against the line height rather than the font size, because a line of type
    /// occupies its line height and sizing to the glyph box is how text ends up touching the thing
    /// above it.
    /// </para>
    /// </summary>
    /// <param name="width">Available width in DIPs.</param>
    /// <param name="height">Height this line may occupy in DIPs.</param>
    /// <param name="characters">
    /// Roughly how many characters the line has to hold. Width matters as much as height: a readout
    /// of "100%" and one of "1.9 GB of 64 GB" want very different steps in the same box.
    /// </param>
    public static TypeStep Fit(double width, double height, double characters, TypeStep floor, TypeStep ceiling)
        => FitWidth(width, height, AverageAdvance * Math.Max(1, characters), floor, ceiling);

    /// <summary>
    /// <see cref="Fit"/> for a line whose width is known in ems rather than guessed from its length
    /// (<see cref="HeroFit.Ems"/>): the largest step whose line fits the box and whose
    /// <paramref name="ems"/> × size fits the width, from <paramref name="floor"/> to <paramref name="ceiling"/>.
    /// </summary>
    public static TypeStep FitWidth(double width, double height, double ems, TypeStep floor, TypeStep ceiling)
    {
        // An unmeasured or degenerate box gets the floor rather than a NaN that would reach a
        // FontSize and throw on the render path.
        if (!IsUsable(width) || !IsUsable(height) || !IsUsable(ems))
        {
            return floor;
        }

        var chosen = floor;

        foreach (var step in Ramp)
        {
            if (step.FontSize < floor.FontSize || step.FontSize > ceiling.FontSize)
            {
                continue;
            }

            if (step.LineHeight <= height && step.FontSize * ems <= width)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    /// <summary>
    /// A step scaled past the ramp, for the one case the ramp does not cover: a readout that is the
    /// entire point of a very large tile.
    /// <para>
    /// The ramp stops at 68px because Windows has nothing bigger to say. A clock filling a
    /// quarter-screen tile legitimately does, so it is allowed to scale <see cref="Display"/> up —
    /// keeping Fluent's own line-height ratio, so it is still the ramp's shape rather than a new one.
    /// </para>
    /// </summary>
    public static TypeStep Scaled(TypeStep step, double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0)
        {
            return step;
        }

        var ratio = step.LineHeight / step.FontSize;
        var size = step.FontSize * factor;

        return new TypeStep(size, size * ratio, step.SemiBold);
    }

    /// <summary>
    /// The readout for a tile whose whole point is one number: a ramp step while the box is small,
    /// and once it can hold <see cref="TitleLarge"/>, that step scaled to fill the box.
    /// <para>
    /// [S-641] It used to stay on the ramp until <see cref="Display"/> fitted and only then scale.
    /// The gap between those two steps is 40px of line height, so every box between them — a wide
    /// strip, a tall column — drew a 40px clock in a tile with room for one half as big again: the
    /// readout jumped from 40 to 68 and never stood anywhere in between. Scaling from TitleLarge
    /// closes the gap, keeping that step's own line-height ratio so it is still the ramp's shape.
    /// </para>
    /// </summary>
    public static TypeStep Hero(double width, double height, double characters)
    {
        if (!IsUsable(width) || !IsUsable(height))
        {
            return Subtitle;
        }

        var fitted = Fit(width, height, characters, Caption, Display);

        if (fitted.FontSize < TitleLarge.FontSize)
        {
            return fitted;
        }

        var wanted = Math.Max(1, characters);
        var byWidth = width / Advance(TitleLarge, wanted);
        var byHeight = height / TitleLarge.LineHeight;
        var factor = Math.Min(byWidth, byHeight);

        return factor > 1 ? Scaled(TitleLarge, factor) : TitleLarge;
    }

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
