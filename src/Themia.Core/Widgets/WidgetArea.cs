namespace Themia.Core.Widgets;

/// <summary>
/// [S-660] How much room a widget has along one axis, in four steps.
/// <para>
/// A class, not a pixel count, because the question a view asks is "is this a strip or a panel",
/// not "is it 312 or 318 wide". Every widget asks it against the same breakpoints, so a tile that
/// is Expanded is Expanded for every widget on the desktop and they shed and grow together.
/// </para>
/// </summary>
public enum SizeClass
{
    /// <summary>Barely more than the one thing the widget must show.</summary>
    Compact = 0,

    /// <summary>The default-sized tile: the essentials plus their secondary text.</summary>
    Regular = 1,

    /// <summary>Room for extras: more rows, a chart, details, a second column.</summary>
    Expanded = 2,

    /// <summary>A panel: everything the widget has to say, and a second view of it.</summary>
    Huge = 3,
}

/// <summary>[S-660] The proportion of a widget's content area, for layouts that turn with it.</summary>
public enum AreaShape
{
    /// <summary>Neither axis dominates.</summary>
    Square = 0,

    /// <summary>At least twice as wide as tall: a strip wants things side by side.</summary>
    Wide = 1,

    /// <summary>At least 1.6 times as tall as wide: a column wants things stacked.</summary>
    Tall = 2,
}

/// <summary>
/// [S-660] The content area a card gives its view, classified per axis.
/// <para>
/// Width and height are judged separately on purpose: a 480×160 strip and a 200×400 column have
/// about the same area and want opposite things, so a single "small / medium / large" would get
/// one of them wrong. The breakpoints are chosen so the seven size tiers (<see cref="WidgetTiers"/>) land in seven distinct
/// pairs (content area = card less the header, about 44 DIPs):
/// </para>
/// <code>
///   XS   120×120 → Compact  × Compact      Tall 200×400 → Regular  × Expanded
///   S    200×150 → Regular  × Compact      L    560×420 → Expanded × Expanded
///   M    320×240 → Regular  × Regular      XL   800×560 → Huge     × Huge
///   Wide 480×160 → Expanded × Compact
/// </code>
/// <para>
/// Classes decide <i>which</i> parts a view considers; <see cref="Disclosure"/> then decides which
/// of them actually fit, so a part is never drawn clipped just because its class allowed it.
/// </para>
/// </summary>
/// <param name="Width">Usable width in DIPs.</param>
/// <param name="Height">Usable height in DIPs.</param>
public readonly record struct WidgetArea(double Width, double Height)
{
    /// <summary>Width at which a tile stops being Compact, Regular and Expanded respectively.</summary>
    public static IReadOnlyList<double> WidthBreakpoints { get; } = [180, 400, 720];

    /// <summary>Height at which a tile stops being Compact, Regular and Expanded respectively.</summary>
    public static IReadOnlyList<double> HeightBreakpoints { get; } = [128, 280, 480];

    /// <summary>The ratio past which an area is a strip or a column rather than a tile.</summary>
    private const double WideRatio = 2.0;

    private const double TallRatio = 1.6;

    /// <summary>
    /// False before the first layout pass (or for a degenerate box). An unmeasured area classifies
    /// as Compact, so a view that lays out before it knows its size starts minimal and grows, rather
    /// than drawing everything and then visibly shedding it.
    /// </summary>
    public bool IsMeasured => IsUsable(Width) && IsUsable(Height);

    public SizeClass WidthClass => Classify(Width, WidthBreakpoints);

    public SizeClass HeightClass => Classify(Height, HeightBreakpoints);

    /// <summary>The smaller of the two classes: how big the tile is "overall" for a part that needs both axes.</summary>
    public SizeClass Least => WidthClass < HeightClass ? WidthClass : HeightClass;

    public AreaShape Shape => !IsMeasured ? AreaShape.Square
        : Width >= Height * WideRatio ? AreaShape.Wide
        : Height >= Width * TallRatio ? AreaShape.Tall
        : AreaShape.Square;

    /// <summary>True when both axes are at least the given classes.</summary>
    public bool AtLeast(SizeClass width, SizeClass height) => WidthClass >= width && HeightClass >= height;

    /// <summary>The area left inside a padding, never negative.</summary>
    public WidgetArea Deflate(double horizontal, double vertical)
        => new(Math.Max(0, Width - (2 * horizontal)), Math.Max(0, Height - (2 * vertical)));

    /// <summary>Which class a length falls in. Each breakpoint is the first length of the next class.</summary>
    public static SizeClass Classify(double length, IReadOnlyList<double> breakpoints)
    {
        ArgumentNullException.ThrowIfNull(breakpoints);

        if (!IsUsable(length))
        {
            return SizeClass.Compact;
        }

        var steps = 0;
        while (steps < breakpoints.Count && steps < (int)SizeClass.Huge && length >= breakpoints[steps])
        {
            steps++;
        }

        return (SizeClass)steps;
    }

    private static bool IsUsable(double value) => double.IsFinite(value) && value > 0;
}
