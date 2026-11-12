namespace Themia.Sdk;

/// <summary>[S-2438] A size in DIPs of the card's box (its header included) at widget scale 1.</summary>
public readonly record struct SizeDip(double Width, double Height)
{
    /// <summary>No bound: a legacy kind's maximum ([S-2443]).</summary>
    public static SizeDip Unbounded { get; } = new(double.PositiveInfinity, double.PositiveInfinity);
}

/// <summary>
/// [S-2439] An axis that only takes steps: <c>Base + n × Step</c> for n in
/// <see cref="MinCount"/>..<see cref="MaxCount"/> (a list's n rows). With <see cref="Count"/> set the
/// axis follows the config: its size is <c>At(Count)</c>, and a resize that lands on another step asks
/// the kind for the config that step means (<see cref="WidgetDescriptor.SizeSets"/>, [S-2442]).
/// </summary>
public sealed record SizeSteps(double Base, double Step, int MinCount, int MaxCount, int? Count = null)
{
    /// <summary>The size of <paramref name="n"/> steps, n held to the range.</summary>
    public double At(int n) => Base + (Math.Clamp(n, MinCount, Math.Max(MinCount, MaxCount)) * Step);

    /// <summary>The count whose size is nearest <paramref name="size"/>, in the range.</summary>
    public int Nearest(double size)
        => Step > 0 && double.IsFinite(size)
            ? Math.Clamp((int)Math.Round((size - Base) / Step, MidpointRounding.AwayFromZero), MinCount, Math.Max(MinCount, MaxCount))
            : Count ?? MinCount;

    /// <summary><paramref name="size"/> on its nearest step.</summary>
    public double Snap(double size) => At(Nearest(size));

    /// <summary>The axis follows the config's count.</summary>
    public bool FollowsConfig => Count is not null;
}

/// <summary>
/// [S-2438] What sizes a widget kind may take, a pure function of its config
/// (<see cref="WidgetDescriptor.Sizing"/>): <see cref="Min"/> ≤ <see cref="Natural"/> ≤ <see cref="Max"/>
/// on both axes, in DIPs of the card's box at widget scale 1 with the kind's default header
/// (<c>WidgetInstance.DefaultHeader</c>). An axis whose Min equals its Max is fixed: it has no resize
/// handle. Content decides the size; the size never decides the content ([S-2445]).
/// <para>
/// [S-2443] A kind not yet converted states a <see cref="Legacy"/> sizing: Natural its old default size,
/// Min the 32 floor, no Max. The host then keeps today's behaviour for it: no fit, the size ladder
/// (<c>IWidgetSizeAware</c>, <c>Disclosure</c>, the seven tiers).
/// </para>
/// </summary>
public sealed record WidgetSizing
{
    /// <summary>[S-08] The floor every legacy kind keeps.</summary>
    public const double LegacyFloor = 32;

    public required SizeDip Min { get; init; }

    /// <summary>[S-10] Where a newly added widget lands.</summary>
    public required SizeDip Natural { get; init; }

    public required SizeDip Max { get; init; }

    /// <summary>[S-2439] The width's steps, or null for a free width in the range.</summary>
    public SizeSteps? WidthSteps { get; init; }

    /// <summary>[S-2439] The height's steps (a list's rows), or null for a free height in the range.</summary>
    public SizeSteps? HeightSteps { get; init; }

    /// <summary>[S-2443] Not converted yet: the old way (the ladder, the 32 floor, no maximum).</summary>
    public bool IsLegacy { get; init; }

    public bool FixedWidth => Min.Width >= Max.Width;

    public bool FixedHeight => Min.Height >= Max.Height;

    /// <summary>[S-2443] A kind not yet converted: <paramref name="width"/> × <paramref name="height"/> its natural (old default) size.</summary>
    public static WidgetSizing Legacy(double width, double height) => new()
    {
        Min = new(LegacyFloor, LegacyFloor),
        Natural = new(width, height),
        Max = SizeDip.Unbounded,
        IsLegacy = true,
    };

    /// <summary>A range on both axes.</summary>
    public static WidgetSizing Range(SizeDip min, SizeDip natural, SizeDip max) => new() { Min = min, Natural = natural, Max = max };

    /// <summary>One size on both axes: no handle at all.</summary>
    public static WidgetSizing Fixed(SizeDip size) => new() { Min = size, Natural = size, Max = size };

    /// <summary>
    /// [S-2439] The height taken in <paramref name="steps"/>: Min and Max become the first and the last
    /// step, Natural the config's count (or the step nearest the stated Natural).
    /// </summary>
    public WidgetSizing WithHeightSteps(SizeSteps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return this with
        {
            HeightSteps = steps,
            Min = Min with { Height = steps.At(steps.MinCount) },
            Max = Max with { Height = steps.At(steps.MaxCount) },
            Natural = Natural with { Height = steps.At(steps.Count ?? steps.Nearest(Natural.Height)) },
        };
    }

    /// <summary>[S-2439] The width taken in <paramref name="steps"/>, as <see cref="WithHeightSteps"/>.</summary>
    public WidgetSizing WithWidthSteps(SizeSteps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        return this with
        {
            WidthSteps = steps,
            Min = Min with { Width = steps.At(steps.MinCount) },
            Max = Max with { Width = steps.At(steps.MaxCount) },
            Natural = Natural with { Width = steps.At(steps.Count ?? steps.Nearest(Natural.Width)) },
        };
    }

    /// <summary>
    /// [S-2440] <paramref name="wanted"/> fitted into the sizing: each axis held to Min..Max, a stepped
    /// axis on its nearest step, an axis that follows the config at the config's step. A legacy sizing
    /// fits nothing: the old floor is the host's ([S-08]).
    /// </summary>
    public SizeDip Fit(SizeDip wanted) => new(FitWidth(wanted.Width), FitHeight(wanted.Height));

    /// <inheritdoc cref="Fit"/>
    public double FitWidth(double width) => FitAxis(width, Min.Width, Natural.Width, Max.Width, WidthSteps);

    /// <inheritdoc cref="Fit"/>
    public double FitHeight(double height) => FitAxis(height, Min.Height, Natural.Height, Max.Height, HeightSteps);

    private double FitAxis(double size, double min, double natural, double max, SizeSteps? steps)
    {
        if (IsLegacy)
        {
            return size;
        }

        if (steps is { Count: { } count })
        {
            return steps.At(count);
        }

        if (!double.IsFinite(size))
        {
            return natural;
        }

        var held = Math.Clamp(size, min, Math.Max(min, max));
        return steps is null ? held : steps.Snap(held);
    }

    /// <summary>
    /// [S-2441] What is wrong with the sizing, in words; empty when it holds: every size positive and
    /// finite (a legacy Max may be unbounded), Min ≤ Natural ≤ Max on both axes, and a stepped axis's
    /// Min, Natural and Max on its steps with a positive step and a count in its range.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();

        void Axis(string name, double min, double natural, double max, SizeSteps? steps)
        {
            if (!(double.IsFinite(min) && min > 0) || !(double.IsFinite(natural) && natural > 0))
            {
                problems.Add($"{name}: Min and Natural must be positive and finite");
            }

            if (!(max > 0) || (!IsLegacy && !double.IsFinite(max)))
            {
                problems.Add($"{name}: Max must be positive{(IsLegacy ? string.Empty : " and finite")}");
            }

            if (!(min <= natural && natural <= max))
            {
                problems.Add($"{name}: Min {min} <= Natural {natural} <= Max {max} does not hold");
            }

            if (steps is null)
            {
                return;
            }

            if (IsLegacy)
            {
                problems.Add($"{name}: a legacy sizing takes no steps");
            }

            if (!(steps.Step > 0) || !double.IsFinite(steps.Base) || steps.MinCount < 0 || steps.MaxCount < steps.MinCount)
            {
                problems.Add($"{name}: steps need a positive step and MinCount <= MaxCount");
            }

            if (steps.Count is { } count && (count < steps.MinCount || count > steps.MaxCount))
            {
                problems.Add($"{name}: Count {count} is outside {steps.MinCount}..{steps.MaxCount}");
            }

            if (!Same(min, steps.At(steps.MinCount)) || !Same(max, steps.At(steps.MaxCount)) || !Same(natural, steps.Snap(natural)))
            {
                problems.Add($"{name}: Min, Natural and Max must stand on the steps");
            }

            if (steps.Count is { } c && !Same(natural, steps.At(c)))
            {
                problems.Add($"{name}: Natural must be the config's count");
            }
        }

        Axis("width", Min.Width, Natural.Width, Max.Width, WidthSteps);
        Axis("height", Min.Height, Natural.Height, Max.Height, HeightSteps);
        return problems;
    }

    private static bool Same(double a, double b) => Math.Abs(a - b) < 0.01;
}
