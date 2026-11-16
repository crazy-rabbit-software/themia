namespace Themia.Sdk;

/// <summary>[S-2438] A size in DIPs of the card's box (its header included) at widget scale 1.</summary>
public readonly record struct SizeDip(double Width, double Height);

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
/// handle. Content decides the size; the size never decides the content ([S-2445]): inside its range a
/// kind shows the same parts, and what it shows is its config ([S-11800]).
/// </summary>
public sealed record WidgetSizing
{
    public required SizeDip Min { get; init; }

    /// <summary>[S-10] Where a newly added widget lands.</summary>
    public required SizeDip Natural { get; init; }

    public required SizeDip Max { get; init; }

    /// <summary>[S-2439] The width's steps, or null for a free width in the range.</summary>
    public SizeSteps? WidthSteps { get; init; }

    /// <summary>[S-2439] The height's steps (a list's rows), or null for a free height in the range.</summary>
    public SizeSteps? HeightSteps { get; init; }

    /// <summary>
    /// [S-12411] How far past its content's range the kind may be made larger (<see cref="SizeStretch"/>):
    /// what its parts are, set where the sizing is made (a family rule: a kit list, a chart, a tile).
    /// </summary>
    public StretchRoom Room { get; init; } = StretchRoom.Standard;

    public bool FixedWidth => Min.Width >= Max.Width;

    public bool FixedHeight => Min.Height >= Max.Height;

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
    /// [S-2438] <paramref name="wanted"/> fitted into the sizing: each axis held to Min..Max, a stepped
    /// axis on its nearest step, an axis that follows the config at the config's step.
    /// </summary>
    public SizeDip Fit(SizeDip wanted) => new(FitWidth(wanted.Width), FitHeight(wanted.Height));

    /// <inheritdoc cref="Fit"/>
    public double FitWidth(double width) => FitAxis(width, Min.Width, Natural.Width, Max.Width, WidthSteps);

    /// <inheritdoc cref="Fit"/>
    public double FitHeight(double height) => FitAxis(height, Min.Height, Natural.Height, Max.Height, HeightSteps);

    private static double FitAxis(double size, double min, double natural, double max, SizeSteps? steps)
    {
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
    /// finite, Min ≤ Natural ≤ Max on both axes, and a stepped axis's
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

            if (!(double.IsFinite(max) && max > 0))
            {
                problems.Add($"{name}: Max must be positive and finite");
            }

            if (!(min <= natural && natural <= max))
            {
                problems.Add($"{name}: Min {min} <= Natural {natural} <= Max {max} does not hold");
            }

            if (steps is null)
            {
                return;
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

/// <summary>[S-12411] What a kind's parts are, for how far it may grow past its content's range.</summary>
public enum StretchRoom
{
    /// <summary>Text, readouts, rows of facts: twice the natural on each free axis.</summary>
    Standard,

    /// <summary>A list: its width two and a half times its natural (its height is its rows).</summary>
    List,

    /// <summary>A chart, a picture, a tile, a grid of items: three times the natural.</summary>
    Picture,

    /// <summary>A size that is not the content's to give (a desktop icon: the desktop's cell): no stretch.</summary>
    None,
}

/// <summary>
/// [S-12411] The relaxed range (owner, 2026-11-15: "relax the size limitations on all the widgets"): a
/// kind states the range its content needs (<see cref="WidgetDescriptor.Sizing"/>'s maker), and the
/// descriptor hands out that range with its Max raised to its Natural times the room's factor
/// (<see cref="Factor"/>, up to 4-DIP steps) where that is larger, on every axis the config does not step,
/// a fixed one included, never past <see cref="Ceiling"/> unless the kind stated more. Min and Natural
/// stay the content's, a stepped axis (a list's rows) stays its steps. In the larger box the parts
/// stretch to their caps and the rest is spacing ([S-12410], <c>ItemStretch</c>); what the widget
/// shows is still its config ([S-11800]).
/// </summary>
public static class SizeStretch
{
    /// <summary>The largest a relaxed axis grows to, in DIPs at widget scale 1.</summary>
    public const double Ceiling = 1200;

    /// <summary>How many times its natural an axis of <paramref name="room"/> may grow to (1: not at all).</summary>
    public static double Factor(StretchRoom room, bool width) => room switch
    {
        StretchRoom.Standard => 2,
        StretchRoom.List => width ? 2.5 : 2,
        StretchRoom.Picture => 3,
        _ => 1,
    };

    /// <summary>[S-12411] <paramref name="stated"/> with its Max relaxed by its room.</summary>
    public static WidgetSizing Relax(WidgetSizing stated)
    {
        ArgumentNullException.ThrowIfNull(stated);
        if (stated.Room == StretchRoom.None)
        {
            return stated;
        }

        var width = Axis(stated.Natural.Width, stated.Max.Width, stated.WidthSteps, Factor(stated.Room, width: true));
        var height = Axis(stated.Natural.Height, stated.Max.Height, stated.HeightSteps, Factor(stated.Room, width: false));
        return stated with { Max = new SizeDip(width, height) };
    }

    /// <summary>The relaxed Max of one axis: a stepped axis keeps its own.</summary>
    public static double Axis(double natural, double max, SizeSteps? steps, double factor)
    {
        if (steps is not null || !double.IsFinite(natural) || !double.IsFinite(max))
        {
            return max;
        }

        var grown = Math.Min(Ceiling, Math.Ceiling(natural * factor / 4) * 4);
        return Math.Max(max, grown);
    }
}
