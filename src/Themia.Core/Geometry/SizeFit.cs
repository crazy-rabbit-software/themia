using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-2448] One size a kind is drawn and checked at, in DIPs of the painted card: its Min, its Natural
/// or its Max (<see cref="SizeFit.CheckedAt"/>), or a size a render was asked for ("320x240").
/// </summary>
public readonly record struct CheckedSize(string Name, double Width, double Height)
{
    public const string Min = "min";

    public const string Natural = "natural";

    public const string Max = "max";
}

/// <summary>
/// [S-2444] One axis of a widget's sizing on a display, as fractions of the screen of the widget's
/// <b>cell</b> (the card plus the gap): the range a resize may take (<see cref="Min"/>..<see cref="Max"/>).
/// [S-11807] With <see cref="Lines"/> set the axis is on the snap grid (<see cref="GridFit.Axis"/>): its Min and Max
/// are whole cells of <c>1 / Lines</c>, and every size it gives is whole cells too. [S-14500] Every whole cell between
/// them (owner, 2026-11-20: "the widgets should not have the size increments anymore but should be incrementable in
/// steps of one").
/// </summary>
public readonly record struct AxisLimits(double Min, double Max, int Lines = 0)
{
    /// <summary>[S-2444] No handle resizes it.</summary>
    public bool Fixed => Max - Min < 1e-9;

    /// <summary>[S-11807] The axis is on the snap grid.</summary>
    public bool OnGrid => Lines > 0;

    /// <summary>
    /// [S-2445] [S-2446] A size of this axis: held to the range. [S-11807] On the grid: rounded up to whole cells (content
    /// never gets less than it was given; a snapped edge is on a whole cell already).
    /// </summary>
    public double Range(double size)
    {
        var top = Math.Max(Min, Max);
        var held = double.IsFinite(size) ? Math.Clamp(size, Min, top) : Min;
        return OnGrid ? Math.Clamp(GridFit.Up(held, Lines), Min, top) : held;
    }
}

/// <summary>
/// [S-17100] How a face's most height follows its width on one display, as fractions of that screen: at a width
/// <c>w</c> the card is at most <see cref="Base"/> + <see cref="PerWidth"/> × <c>w</c> tall (its shape there, the
/// content scale applied).
/// </summary>
public readonly record struct HeightFollow(double Base, double PerWidth)
{
    /// <summary>[S-17100] The most height at <paramref name="width"/>, in fractions of the screen.</summary>
    public double At(double width) => Base + (PerWidth * width);
}

/// <summary>
/// [S-2444] A widget's sizing on one display, as fractions of that screen's cell. [S-17100] A face that scales as one has
/// its height follow its width (<see cref="Follow"/>): <see cref="At"/> gives the limits a card of a width has.
/// </summary>
public sealed record SizeLimits(AxisLimits Width, AxisLimits Height, HeightFollow? Follow = null)
{
    /// <summary>
    /// [S-17100] The limits of a card <paramref name="width"/> wide (a fraction of the screen): a face's most height its shape
    /// at that width, in the whole cells that hold it on the grid (so a card at its natural keeps it), never under its Min nor
    /// over its Max; every other sizing's as they are.
    /// </summary>
    public SizeLimits At(double width)
    {
        if (Follow is not { } follow || !double.IsFinite(width))
        {
            return this;
        }

        var most = follow.At(width);
        most = Height.OnGrid ? GridFit.Up(most, Height.Lines) : most;
        var max = Math.Max(Height.Min, Math.Min(Height.Max, most));
        return this with { Height = Height with { Max = max } };
    }

    /// <summary>
    /// [S-17100] The limits of a card that keeps its <paramref name="height"/> (a fraction of the screen) while its width
    /// changes (a block resized across: its members' heights are the block's): a face's least width the one whose shape still
    /// takes that height, so no member is left taller than its width takes; every other sizing's as they are.
    /// </summary>
    public SizeLimits ForHeight(double height)
    {
        if (Follow is not { } follow || !double.IsFinite(height) || !(follow.PerWidth > 0))
        {
            return this;
        }

        // The least width whose most height (in whole cells) holds the height: on the grid a cell under it rounds up to it.
        var below = Height.OnGrid ? height - (1.0 / Height.Lines) : height;
        var least = Math.Max(0, (below - follow.Base) / follow.PerWidth);
        least = Width.OnGrid ? GridFit.Up(least, Width.Lines) : least;
        while (Width.OnGrid && At(least).Height.Max < height - 1e-9 && least < Width.Max)
        {
            least += 1.0 / Width.Lines;
        }

        var min = Math.Min(Math.Max(Width.Min, least), Math.Max(Width.Min, Width.Max));
        return this with { Width = Width with { Min = min } };
    }
}

/// <summary>
/// [S-2444]..[S-2447] The host's side of the sizing contract ([S-2438]): a kind's sizing turned into the
/// fractions a display stores, for the layout's fit, the resize's limits and the refit after a config
/// change. Pure: the card, the surface, the keyboard and the tests share it.
/// </summary>
public static class SizeFit
{
    /// <summary>
    /// [S-2444] A widget's sizing on <paramref name="screen"/>: each DIP size times the content
    /// <paramref name="scale"/> ([S-6532]: the display's and the widget's own), as a fraction of the
    /// screen; the card paints its whole cell ([S-11884]: no gap). Null for an unusable
    /// screen (one not measured yet): nothing to fit against. [S-11807] Always whole grid cells
    /// (<see cref="GridFit.OnGrid"/>), every widget, a desktop icon included ([S-5588]): the grid is always on.
    /// </summary>
    public static SizeLimits? Limits(WidgetSizing sizing, ScreenMetrics screen, double scale)
        => ContentLimits(sizing, screen, scale) is { } content ? GridFit.OnGrid(content, screen) : null;

    private static SizeLimits? ContentLimits(WidgetSizing sizing, ScreenMetrics screen, double scale)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        if (!screen.IsUsable)
        {
            return null;
        }

        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;

        AxisLimits Axis(double min, double max, double screenPx)
        {
            double Cell(double dip) => dip * s / screenPx;

            // [S-12411] No larger than the screen: its Max is the ceiling, the work area the bound.
            return new AxisLimits(Cell(min), Math.Max(Cell(min), Math.Min(1, Cell(max))));
        }

        // [S-17100] A face's most height follows its width: its shape's line in DIPs (the height round it less its rate across
        // what stands round it, plus the rate times the width) on this screen at this scale.
        HeightFollow? follow = sizing.Shape is { } shape
            ? new HeightFollow(s * (shape.Around.Height - (shape.PerWidth * shape.Around.Width)) / screen.Height, shape.PerWidth * screen.Width / screen.Height)
            : null;
        return new SizeLimits(Axis(sizing.Min.Width, sizing.Max.Width, screen.Width), Axis(sizing.Min.Height, sizing.Max.Height, screen.Height), follow);
    }

    /// <summary>
    /// [S-14500] The grid a card's view lays its items on, on <paramref name="screen"/> at content scale <paramref name="scale"/>:
    /// the grid's pitch in the card's content DIPs (<c>WidgetCard.CellPitch</c>, [S-12830]: its width over its columns, on
    /// both axes), what its items' optimal lengths are whole cells of.
    /// </summary>
    public static ItemGrid Grid(ScreenMetrics screen, double scale)
        => new(GridGeometry.CellPitch(screen) / (double.IsFinite(scale) && scale > 0 ? scale : 1));

    /// <summary>
    /// [S-2446] The box a widget is painted in: its stored rect fitted into its sizing, anchored at its
    /// top-left, then kept in reach as any rect ([S-09]). The stored rect is not changed by being shown;
    /// without limits (a screen not measured yet) it is shown as stored.
    /// </summary>
    public static FracRect Shown(FracRect stored, SizeLimits? limits, ScreenMetrics screen)
    {
        if (limits is null || !stored.IsFinite)
        {
            return stored;
        }

        // [S-17100] The width first: a face's height is held to what that width takes.
        var w = limits.Width.Range(stored.W);
        var fitted = stored with { W = w, H = limits.At(w).Height.Range(stored.H) };
        return fitted == stored ? stored : GridGeometry.Clamp(fitted, screen);
    }

    /// <summary>
    /// [S-11809] The boxes a picture paints its widgets in (a scene, the add page's tile): a lone widget
    /// as <see cref="Shown"/> paints it, widgets whose cells touch (<see cref="Lattice.Touching"/>, a ready-made
    /// group as laid) fitted together as one block (<see cref="GridFit.Layout"/>) so the edges they share
    /// stay shared at any scale and gap.
    /// <paramref name="limits"/> gives each widget's limits (its scale, the gap, the grid).
    /// </summary>
    public static IReadOnlyList<FracRect> ShownAll(IReadOnlyList<WidgetInstance> widgets, Func<WidgetInstance, SizeLimits?> limits, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(limits);
        var all = widgets.Select(limits).ToList();
        var shown = widgets.Select((w, i) => Shown(w.Rect, all[i], screen)).ToArray();
        foreach (var at in Lattice.Touching([.. widgets.Select(w => w.Rect)]))
        {
            if (at.Count < 2)
            {
                continue;
            }

            var laid = GridFit.Layout([.. at.Select(i => widgets[i].Rect)], [.. at.Select(i => all[i])], screen);
            for (var k = 0; k < at.Count; k++)
            {
                shown[at[k]] = laid[k];
            }
        }

        return shown;
    }

    /// <summary>[S-2444] A widget's sizing as its card shows it: its kind's for its config ([S-12480]: no card has a header).</summary>
    public static WidgetSizing Of(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Of(widget.Config);
    }

    /// <summary>[S-2444] The sizing of <paramref name="config"/>: its kind's.</summary>
    public static WidgetSizing Of(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ModuleCatalog.Of(config.Kind).Sizing(config);
    }

    /// <summary>
    /// [S-2448] The sizing a new widget of <paramref name="kind"/> has in <paramref name="style"/> (its
    /// default config, that style): what the renders and the showcase draw a kind at. [S-16683] A preset's: its new config's.
    /// </summary>
    public static WidgetSizing Of(WidgetPick kind, int? style = null)
    {
        var config = kind.CreateConfig();
        if (style is { } chosen && config is IStyled styled)
        {
            styled.StyleValue = chosen;
        }

        return Of(config);
    }

    /// <summary>[S-2448] The sizes a kind is checked at: its range, its Min, its Natural and its Max, named so.</summary>
    public static IReadOnlyList<CheckedSize> CheckedAt(WidgetSizing sizing)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        return
        [
            new(CheckedSize.Min, sizing.Min.Width, sizing.Min.Height),
            new(CheckedSize.Natural, sizing.Natural.Width, sizing.Natural.Height),
            // [S-12411] No practical Max: the large size checked is twice the natural ([S-2448]).
            new(CheckedSize.Max, SizeStretch.Large(sizing).Width, SizeStretch.Large(sizing).Height),
        ];
    }

    /// <summary>
    /// [S-14500] The sizes a kind is checked at cell by cell (owner, 2026-11-20: "the widgets should not have the size
    /// increments anymore but should be incrementable in steps of one"): its Natural on whole default cells, then one to
    /// <paramref name="most"/> cells more and fewer on each axis alone (fewer never under its Min), and odd heights of 11, 13
    /// and 17 cells at its natural width (within its range), named <c>h+1</c>, <c>w-2</c>, <c>h11</c>, ...: the sizes a
    /// resize lands on that whole-item steps once forbade.
    /// </summary>
    public static IReadOnlyList<CheckedSize> CheckedCells(WidgetSizing sizing, int most = 5)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        const double cell = ItemGrid.DefaultCell;
        static double Up(double dip) => Math.Ceiling((dip / cell) - Lattice.Epsilon) * cell;
        var (w, h) = (Up(sizing.Natural.Width), Up(sizing.Natural.Height));
        var (minW, minH) = (Up(sizing.Min.Width), Up(sizing.Min.Height));
        var (maxW, maxH) = (Math.Max(minW, sizing.Max.Width), Math.Max(minH, sizing.Max.Height));
        var sizes = new List<CheckedSize> { new(CheckedSize.Natural, w, h) };
        void Add(string name, double width, double height)
        {
            const double eps = Lattice.Epsilon;
            if (width >= minW - eps && width <= maxW + eps && height >= minH - eps && height <= Math.Min(maxH, Up(sizing.MostHeight(width))) + eps
                && !sizes.Exists(s => Lattice.Same(s.Width, width) && Lattice.Same(s.Height, height)))
            {
                sizes.Add(new CheckedSize(name, width, height));
            }
        }

        for (var n = 1; n <= most; n++)
        {
            Add($"h+{n}", w, h + (n * cell));
            Add($"h-{n}", w, h - (n * cell));
            Add($"w+{n}", w + (n * cell), h);
            Add($"w-{n}", w - (n * cell), h);
        }

        foreach (var odd in new[] { 11, 13, 17 })
        {
            Add($"h{odd}", w, odd * cell);
        }

        return sizes;
    }

    /// <summary>
    /// [S-2444] <see cref="Of(WidgetInstance)"/> on a display, on the grid ([S-11807]).
    /// </summary>
    public static SizeLimits? Limits(WidgetInstance widget, ScreenMetrics screen, double scale)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Limits(Of(widget), screen, scale);
    }

    /// <summary>
    /// [S-2447] A config change that changed the sizing: the stored rect fitted into the new one in the
    /// same mutation (anchored at its top-left, shifted back into reach where that would leave the work
    /// area); [S-11807] whole grid cells. [S-14503] Where the change moved the sizing's Natural on an axis and left its Min
    /// and Max there as they were (<paramref name="was"/>, the sizing before: a list's Rows, which decide its natural
    /// height), the card stands at its new Natural on that axis. Returns whether the rect changed.
    /// </summary>
    public static bool Refit(WidgetInstance widget, ScreenMetrics screen, double scale, WidgetSizing? was = null)
    {
        ArgumentNullException.ThrowIfNull(widget);
        var sizing = Of(widget);
        var limits = Limits(sizing, screen, scale);
        var stored = widget.Rect;
        if (limits is not null && was is not null && stored.IsFinite)
        {
            var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
            static bool Moved(double before, double now, double min0, double min1, double max0, double max1)
                => !Lattice.Same(before, now) && Lattice.Same(min0, min1) && Lattice.Same(max0, max1);
            if (Moved(was.Natural.Width, sizing.Natural.Width, was.Min.Width, sizing.Min.Width, was.Max.Width, sizing.Max.Width))
            {
                stored = stored with { W = limits.Width.Range(sizing.Natural.Width * s / screen.Width) };
            }

            if (Moved(was.Natural.Height, sizing.Natural.Height, was.Min.Height, sizing.Min.Height, was.Max.Height, sizing.Max.Height))
            {
                stored = stored with { H = limits.Height.Range(sizing.Natural.Height * s / screen.Height) };
            }

            // Kept in reach as any rect ([S-09]) where the natural moved it; [S-855] clear of a taskbar it was clear of, so
            // a card that grows is never pushed behind one (one the user hung over a taskbar may stay over it, [S-6608]).
            stored = stored == widget.Rect ? stored : GridGeometry.Clamp(stored, screen, GridGeometry.ClearSides(widget.Rect, screen.Allowed, screen.Allowed));
        }

        var shown = Shown(stored, limits, screen);
        if (shown == widget.Rect)
        {
            return false;
        }

        widget.Rect = shown;
        return true;
    }
}
