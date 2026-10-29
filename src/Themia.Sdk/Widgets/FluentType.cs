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
        new(Themia.Sdk.Kit.FamilyType.CaptionSize, Themia.Sdk.Kit.FamilyType.CaptionLine, false),  // Caption ([S-6700]: the family's sizes)
        new(Themia.Sdk.Kit.FamilyType.BodySize, Themia.Sdk.Kit.FamilyType.BodyLine, false),        // Body
        new(Themia.Sdk.Kit.FamilyType.BodySize, Themia.Sdk.Kit.FamilyType.BodyLine, true),         // BodyStrong
        new(18, 24, false),  // BodyLarge (off the family's sizes: goes once no view uses it)
        new(Themia.Sdk.Kit.FamilyType.TitleSize, Themia.Sdk.Kit.FamilyType.TitleLine, true),       // Subtitle: the family's Title
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
    /// A width estimate of <paramref name="characters"/> characters at a step (0.58em a character), for a
    /// layout decision that only has to be close: whether a caption still fits beside a reading, say.
    /// </summary>
    public static double EstimateWidth(TypeStep step, double characters) => Advance(step, characters);

    private static TypeStep[] _ramp = [_steps[0], _steps[1], _steps[3], _steps[4], _steps[5], _steps[6], _steps[7]];

    /// <summary>The ramp in order, smallest first, at the live <see cref="TextScale"/>.</summary>
    public static IReadOnlyList<TypeStep> Ramp => _ramp;

}
