using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>[S-2438] A size in DIPs of the card's box at widget scale 1.</summary>
public readonly record struct SizeDip(double Width, double Height);

/// <summary>
/// [S-2439] An axis that only takes steps: <c>Base + n × Step</c> for n in
/// <see cref="MinCount"/>..<see cref="MaxCount"/> (a list's n rows). With <see cref="Count"/> set the
/// axis follows the config: its size is <c>At(Count)</c>, and a resize that lands on another step asks
/// the kind for the config that step means (<see cref="WidgetDescriptor.SizeSets"/>, [S-2442]).
/// </summary>
public sealed record SizeSteps(double Base, double Step, int MinCount, int MaxCount, int? Count = null)
{
    /// <summary>
    /// [S-14100] The axis in whole grid cells, part by part (owner, 2026-11-20: "now the weather forecast widget always has
    /// one block space at the bottom. this should obviously not be"): its fixed parts and its repeated item as natural
    /// lengths, each laid in whole cells of the card's grid (<see cref="WholeCells.On"/>), so every step is exactly its
    /// fixed parts and n whole items, never a cell left over under the last. <see cref="Base"/> and <see cref="Step"/> are
    /// then those on the default grid (<see cref="ItemGrid.Default"/>), what the DIPs of the sizing say; the host lays the
    /// axis on its display's grid (<c>SizeFit.Limits</c>). Null: Base and Step are DIPs as stated.
    /// </summary>
    public WholeCells? Cells { get; init; }

    /// <summary>
    /// [S-14100] An axis of <paramref name="item"/>-long items over <paramref name="over"/> (the fixed parts, each its own
    /// whole cells: a tool line from the card's top edge, column titles, an add row), every part and item whole cells of
    /// the card's grid: steps of one item's whole cells (<see cref="Cells"/>), n items for n in
    /// <paramref name="minCount"/>..<paramref name="maxCount"/>, following <paramref name="count"/> where the config says.
    /// </summary>
    public static SizeSteps InCells(double item, int minCount, int maxCount, int? count = null, params double[] over)
    {
        var cells = new WholeCells(item, over ?? []);
        var (b, step) = cells.On(ItemGrid.Default());
        return new SizeSteps(b, step, minCount, maxCount, count) { Cells = cells };
    }

    /// <summary>
    /// [S-14100] An axis of <paramref name="items"/> like items that grow together (a month's six weeks, its seven days
    /// across), over <paramref name="over"/> (the fixed parts, each its own whole cells): its least is every item at the
    /// whole cells <paramref name="item"/> takes, and each step one more cell for every item, up to
    /// <paramref name="most"/> steps, so every size of it lays the items whole cells and alike, none left over.
    /// </summary>
    public static SizeSteps Growing(double item, int items, int most, params double[] over)
    {
        var cells = new WholeCells(0, over ?? []) { Grows = Math.Max(1, items), Least = item };
        var (b, step) = cells.On(ItemGrid.Default());
        return new SizeSteps(b, step, 0, Math.Max(0, most)) { Cells = cells };
    }

    /// <summary>
    /// [S-14100] An axis of <paramref name="items"/> like items each holding <paramref name="least"/> and
    /// <paramref name="count"/> lines of <paramref name="line"/> (a month's weeks, their cells' chip lines), over
    /// <paramref name="over"/>: a step is one more line in every item, each part, the least and a line whole cells of the
    /// card's grid, so the items fill the axis exactly at every count.
    /// </summary>
    public static SizeSteps Lined(double least, double line, int items, int minCount, int maxCount, int count, params double[] over)
    {
        var cells = new WholeCells(line, over ?? []) { Grows = Math.Max(1, items), Least = least };
        var (b, step) = cells.On(ItemGrid.Default());
        return new SizeSteps(b, step, minCount, maxCount, count) { Cells = cells };
    }

    /// <summary>[S-14100] The base and the step on <paramref name="grid"/>: each part whole cells of it (<see cref="Cells"/>), else as stated.</summary>
    public (double Base, double Step) On(ItemGrid grid) => Cells is { } cells ? cells.On(grid) : (Base, Step);

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
/// [S-14100] An axis of whole grid cells, part by part (owner, 2026-11-19, docs/WIDGET-RULES.md WR-6: "items inside widgets
/// should be designed in a way that they match the size of the grid on the desktop"; 2026-11-20, WR-5: "now the weather
/// forecast widget always has one block space at the bottom. this should obviously not be"): the natural length of the
/// repeated <see cref="Item"/> (a row of 28: three cells on the default grid) and of each fixed part <see cref="Over"/>
/// (a tool line's band from the card's top edge, column titles, an add row), each grown to whole cells of its own on a
/// grid, as the view lays them (<c>ItemStretch</c>, <c>LineRules</c>). Value equality over the parts, so a sizing made
/// twice from one config is the same sizing (<c>SizingNotes</c> compares them).
/// </summary>
public sealed record WholeCells(double Item, IReadOnlyList<double> Over)
{
    /// <summary>
    /// [S-14100] How many items a step takes: a list's one more row (1, the default), or the items that grow together (a
    /// month's six weeks), each by one <see cref="Item"/> (by one cell where the item is 0).
    /// </summary>
    public int Grows { get; init; } = 1;

    /// <summary>[S-14100] The least of each of the <see cref="Grows"/> items, in the base (a week's number line); 0: none.</summary>
    public double Least { get; init; }

    /// <summary>
    /// The fixed parts' and the item's lengths on <paramref name="grid"/>: each part whole cells of its own (none for a part of
    /// nothing), [S-14269] counted as the host counts an axis's cells (<see cref="GridCells.Up"/>).
    /// </summary>
    public (double Base, double Step) On(ItemGrid grid)
    {
        var pitch = grid.Pitch;
        var (b, step) = Count(length => GridCells.Up(length / pitch));
        return (b * pitch, step * pitch);
    }

    /// <summary>
    /// [S-14100] The cells the fixed parts (and the items' least) and one step take on <paramref name="grid"/> as a view lays
    /// them (<see cref="ItemGrid.Cells"/>).
    /// </summary>
    public (int Base, int Step) Count(ItemGrid grid) => Count(grid.Cells);

    /// <summary>
    /// [S-14269] The cells the fixed parts (and the items' least) and one step take, each length counted by
    /// <paramref name="cells"/> (an axis's own count: its display's pitch along it, the host's rounding).
    /// </summary>
    public (int Base, int Step) Count(Func<double, int> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        static bool Some(double length) => length > 0 && double.IsFinite(length);
        var items = Math.Max(1, Grows);
        var over = Over.Where(Some).Sum(cells) + (Some(Least) ? items * cells(Least) : 0);
        return (over, items * (Some(Item) ? cells(Item) : 1));
    }

    public bool Equals(WholeCells? other)
        => other is not null && Item.Equals(other.Item) && Grows == other.Grows && Least.Equals(other.Least) && Over.SequenceEqual(other.Over);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Item);
        hash.Add(Grows);
        hash.Add(Least);
        foreach (var part in Over)
        {
            hash.Add(part);
        }

        return hash.ToHashCode();
    }
}

/// <summary>
/// [S-2438] What sizes a widget kind may take, a pure function of its config
/// (<see cref="WidgetDescriptor.Sizing"/>): <see cref="Min"/> ≤ <see cref="Natural"/> ≤ <see cref="Max"/>
/// on both axes, in DIPs of the card's box at widget scale 1, a free card's (the inset on every side,
/// [S-12480]). An axis whose Min equals its Max is fixed: it has no resize
/// handle. Content decides the size; the size never decides the content ([S-2445]): inside its range a
/// kind shows the same parts, and what it shows is its config ([S-11800]).
/// </summary>
public sealed record WidgetSizing
{
    /// <summary>
    /// [S-08] The smallest any widget may be on either axis, in DIPs: one cell of the widget grid ([S-06], about
    /// 10), so a kind with no real minimum (a quick toggle, [S-12806]) goes as small as the grid goes.
    /// </summary>
    public const double Floor = 10;

    public required SizeDip Min { get; init; }

    /// <summary>[S-12833] [S-06b] The least a face that scales down is sized at: the grid's least widget.</summary>
    public const double LeastScaled = 32;

    /// <summary>
    /// [S-12833] This sizing for a face that scales down as one to <paramref name="least"/> (<c>ScaleFloor.Least</c> of its
    /// smallest text): its Min the natural box at that scale (whole DIPs, never under <see cref="LeastScaled"/>) on every
    /// axis it does not step; Natural and Max kept.
    /// </summary>
    public WidgetSizing ScalesDown(double least)
    {
        var k = least > 0 && double.IsFinite(least) ? Math.Min(1, least) : 1;
        return ScalesDownTo(new SizeDip(Natural.Width * k, Natural.Height * k));
    }

    /// <summary>
    /// [S-12833] This sizing for a face whose parts are <paramref name="face"/>: its Min the face's least box (its main
    /// reading alone at the legibility floor, <c>FaceParts.LeastBox</c>) in the card's inset, which does not scale, with
    /// <paramref name="over"/> more over it (a tool line of whole cells), on every axis it does not step.
    /// </summary>
    public WidgetSizing ScalesDownTo(Themia.Core.Widgets.FaceParts face, double over = 0)
    {
        ArgumentNullException.ThrowIfNull(face);
        var least = face.LeastBox();
        var inset = 2 * Kit.FamilySpace.Inset;
        return ScalesDownTo(new SizeDip(least.Width + inset, least.Height + inset + over));
    }

    /// <summary>
    /// [S-12833] This sizing for a face that scales down as one to <paramref name="least"/>, the box it states for its main
    /// reading at the legibility floor in the card's inset (<c>FaceFloor.LeastBox</c> plus the inset): its Min that box
    /// (whole DIPs, never under <see cref="LeastScaled"/>, never over the stated Min) on every axis it does not step.
    /// </summary>
    public WidgetSizing ScalesDownTo(SizeDip least)
    {
        static double Axis(double wanted, double min, SizeSteps? steps)
            => steps is not null || !(wanted > 0) || !double.IsFinite(wanted) ? min : Math.Min(min, Math.Max(LeastScaled, Math.Ceiling(wanted)));
        return this with { Min = new SizeDip(Axis(least.Width, Min.Width, WidthSteps), Axis(least.Height, Min.Height, HeightSteps)) };
    }

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

/// <summary>[S-12411] What a kind's parts are, for how its parts stretch in a larger box.</summary>
public enum StretchRoom
{
    /// <summary>Text, readouts, rows of facts.</summary>
    Standard,

    /// <summary>A list.</summary>
    List,

    /// <summary>A chart, a picture, a tile, a grid of items.</summary>
    Picture,

    /// <summary>A size that is not the content's to give (a desktop icon: the desktop's cell): no stretch.</summary>
    None,
}

/// <summary>
/// [S-12411] The relaxed range (owner, 2026-11-15: "relax the size limitations on all the widgets"; 2026-11-16: "maximum
/// sizes should not really be a thing as this is for the user to decide"): a kind states the range its content needs
/// (<see cref="WidgetDescriptor.Sizing"/>'s maker), and the descriptor hands out that range with no practical maximum:
/// its Max raised to <see cref="Ceiling"/> on every axis the config does not step, a fixed one included, so the user
/// makes a widget as large as the work area holds (the host stops a resize there, <c>SizeFit.Limits</c>). Min and
/// Natural stay the content's, a stepped axis (a list's rows) stays its steps, a kind of <see cref="StretchRoom.None"/>
/// (a desktop icon) its own. In the larger box the parts stretch to their caps and the rest is spacing ([S-12410],
/// <c>ItemStretch</c>); what the widget shows is still its config ([S-11800]).
/// </summary>
public static class SizeStretch
{
    /// <summary>
    /// [S-12411] The Max of a relaxed axis, in DIPs at widget scale 1: a 4K display's width, larger than any work area a
    /// widget stands in at scale 1, so it bounds nothing the work area does not; finite, so a sizing stays one.
    /// </summary>
    public const double Ceiling = 3840;

    /// <summary>[S-12411] <paramref name="stated"/> with its Max relaxed to the ceiling.</summary>
    public static WidgetSizing Relax(WidgetSizing stated)
    {
        ArgumentNullException.ThrowIfNull(stated);
        if (stated.Room == StretchRoom.None)
        {
            return stated;
        }

        return stated with { Max = new SizeDip(Axis(stated.Max.Width, stated.WidthSteps), Axis(stated.Max.Height, stated.HeightSteps)) };
    }

    /// <summary>[S-12411] The relaxed Max of one axis: a stepped axis keeps its own.</summary>
    public static double Axis(double max, SizeSteps? steps)
        => steps is not null || !double.IsFinite(max) ? max : Math.Max(max, Ceiling);

    /// <summary>
    /// [S-2448] The large size a kind is checked at (renders, the showcase): with no practical Max, twice its Natural on
    /// each free axis (4-DIP steps), fitted into its sizing.
    /// </summary>
    public static SizeDip Large(WidgetSizing sizing)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        return sizing.Fit(new SizeDip(Math.Ceiling(sizing.Natural.Width * 2 / 4) * 4, Math.Ceiling(sizing.Natural.Height * 2 / 4) * 4));
    }
}