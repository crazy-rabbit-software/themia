using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>[S-2438] A size in DIPs of the card's box at widget scale 1.</summary>
public readonly record struct SizeDip(double Width, double Height);

/// <summary>
/// [S-2438] What sizes a widget kind may take, a pure function of its config
/// (<see cref="WidgetDescriptor.Sizing"/>): <see cref="Min"/> ≤ <see cref="Natural"/> ≤ <see cref="Max"/>
/// on both axes, in DIPs of the card's box at widget scale 1, a free card's (the inset on every side,
/// [S-12480]). An axis whose Min equals its Max is fixed: it has no resize
/// handle. Content decides the size; the size never decides the content ([S-2445]): inside its range a
/// kind shows the same parts, and what it shows is its config ([S-11800]). [S-14500] Every axis takes every whole grid
/// cell between its Min and its Max (owner, 2026-11-20: "the widgets should not have the size increments anymore but
/// should be incrementable in steps of one"): no sizing states a step; its items keep their size ([S-14810]).
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
    /// smallest text): its Min the natural box at that scale (whole DIPs, never under <see cref="LeastScaled"/>) on both
    /// axes; Natural and Max kept.
    /// </summary>
    public WidgetSizing ScalesDown(double least)
    {
        var k = least > 0 && double.IsFinite(least) ? Math.Min(1, least) : 1;
        return ScalesDownTo(new SizeDip(Natural.Width * k, Natural.Height * k));
    }

    /// <summary>
    /// [S-12833] This sizing for a face whose parts are <paramref name="face"/>: its Min the face's least box (its main
    /// reading alone at the legibility floor, <c>FaceParts.LeastBox</c>) in the card's inset, which does not scale, with
    /// <paramref name="over"/> more over it (a tool line of whole cells), on both axes.
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
    /// (whole DIPs, never under <see cref="LeastScaled"/>, never over the stated Min) on both axes.
    /// </summary>
    public WidgetSizing ScalesDownTo(SizeDip least)
    {
        static double Axis(double wanted, double min)
            => !(wanted > 0) || !double.IsFinite(wanted) ? min : Math.Min(min, Math.Max(LeastScaled, Math.Ceiling(wanted)));
        return this with { Min = new SizeDip(Axis(least.Width, Min.Width), Axis(least.Height, Min.Height)) };
    }

    /// <summary>[S-10] Where a newly added widget lands.</summary>
    public required SizeDip Natural { get; init; }

    public required SizeDip Max { get; init; }

    /// <summary>
    /// [S-13012] [S-12411] The height is the kind's own, never relaxed (<see cref="SizeStretch.Relax"/>): a fence rolled up
    /// to its title bar, which no handle makes taller.
    /// </summary>
    public bool KeepsHeight { get; init; }

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
    /// [S-2438] <paramref name="wanted"/> fitted into the sizing: each axis held to Min..Max ([S-14500]: no step).
    /// </summary>
    public SizeDip Fit(SizeDip wanted) => new(FitWidth(wanted.Width), FitHeight(wanted.Height));

    /// <inheritdoc cref="Fit"/>
    public double FitWidth(double width) => FitAxis(width, Min.Width, Natural.Width, Max.Width);

    /// <inheritdoc cref="Fit"/>
    public double FitHeight(double height) => FitAxis(height, Min.Height, Natural.Height, Max.Height);

    private static double FitAxis(double size, double min, double natural, double max)
        => double.IsFinite(size) ? Math.Clamp(size, min, Math.Max(min, max)) : natural;

    /// <summary>
    /// [S-2441] What is wrong with the sizing, in words; empty when it holds: every size positive and
    /// finite, Min ≤ Natural ≤ Max on both axes.
    /// </summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();

        void Axis(string name, double min, double natural, double max)
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
        }

        Axis("width", Min.Width, Natural.Width, Max.Width);
        Axis("height", Min.Height, Natural.Height, Max.Height);
        return problems;
    }
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
/// its Max raised to <see cref="Ceiling"/> on every axis, a fixed one included, so the user makes a widget as large as
/// the work area holds (the host stops a resize there, <c>SizeFit.Limits</c>). Min and Natural stay the content's, a
/// height the kind keeps (<see cref="WidgetSizing.KeepsHeight"/>: a fence rolled up) its own, a kind of
/// <see cref="StretchRoom.None"/> (a desktop icon) its own. In the larger box the parts stretch to their caps and the rest is spacing ([S-12410],
/// <c>ItemSize</c>); what the widget shows is still its config ([S-11800]).
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

        return stated with { Max = new SizeDip(Axis(stated.Max.Width), stated.KeepsHeight ? stated.Max.Height : Axis(stated.Max.Height)) };
    }

    /// <summary>[S-12411] The relaxed Max of one axis.</summary>
    public static double Axis(double max) => !double.IsFinite(max) ? max : Math.Max(max, Ceiling);

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