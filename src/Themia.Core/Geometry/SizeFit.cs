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
/// <b>cell</b> (the card plus the gap): the range a resize may take (<see cref="Min"/>..<see cref="Max"/>,
/// on steps where the axis has them) and, where the axis follows the config, the one size it is laid
/// out at (<see cref="Pinned"/>). [S-11807] With <see cref="Lines"/> set the axis is on the snap grid
/// (<see cref="GridFit.Axis"/>): its Min, Max and Pinned are whole cells of <c>1 / Lines</c>, and every
/// size it gives is whole cells too.
/// </summary>
public readonly record struct AxisLimits(double Min, double Max, double StepBase = 0, double Step = 0, double? Pinned = null, int Lines = 0)
{
    /// <summary>The axis takes steps.</summary>
    public bool Stepped => Step > 0;

    /// <summary>[S-2444] No handle resizes it.</summary>
    public bool Fixed => Max - Min < 1e-9;

    /// <summary>[S-11807] The axis is on the snap grid.</summary>
    public bool OnGrid => Lines > 0;

    /// <summary>
    /// [S-2445] A resize's size on this axis: held to the range, on its nearest step. [S-11807] On the
    /// grid: rounded up to whole cells (content never gets less than it was given; a snapped edge is on
    /// a whole cell already), a stepped axis on the step whose size rounded up to whole cells is nearest.
    /// </summary>
    public double Range(double size)
    {
        var top = Math.Max(Min, Max);
        var held = double.IsFinite(size) ? Math.Clamp(size, Min, top) : Min;
        if (!Stepped)
        {
            return OnGrid ? Math.Clamp(GridFit.Up(held, Lines), Min, top) : held;
        }

        if (OnGrid)
        {
            var n = Math.Floor(((held - StepBase) / Step) + 1e-9);
            var below = GridFit.Up(StepBase + (n * Step), Lines);
            var above = GridFit.Up(StepBase + ((n + 1) * Step), Lines);
            return Math.Clamp(held - below <= above - held ? below : above, Min, top);
        }

        var m = Math.Round((held - StepBase) / Step, MidpointRounding.AwayFromZero);
        return Math.Clamp(StepBase + (m * Step), Min, top);
    }

    /// <summary>
    /// [S-11807] What a size of this axis holds of its content: on the grid a stepped axis's largest
    /// step that fits in it (so the config a resize stores is the rows its whole cells hold); the size
    /// itself otherwise.
    /// </summary>
    public double Holds(double size)
        => OnGrid && Stepped && double.IsFinite(size)
            ? StepBase + (Math.Floor(((size - StepBase) / Step) + 1e-9) * Step)
            : size;

    /// <summary>[S-2446] The size the axis is laid out at: the config's where it follows one, else <see cref="Range"/>.</summary>
    public double Hold(double size) => Pinned ?? Range(size);
}

/// <summary>[S-2444] A widget's sizing on one display, as fractions of that screen's cell.</summary>
public sealed record SizeLimits(AxisLimits Width, AxisLimits Height);

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

        AxisLimits Axis(double min, double max, SizeSteps? steps, double screenPx)
        {
            double Cell(double dip) => dip * s / screenPx;
            // [S-12411] A free axis no larger than the screen: its Max is the ceiling, the work area the bound.
            return steps is null
                ? new AxisLimits(Cell(min), Math.Max(Cell(min), Math.Min(1, Cell(max))))
                : new AxisLimits(Cell(min), Cell(max), Cell(steps.Base), steps.Step * s / screenPx, steps.Count is { } n ? Cell(steps.At(n)) : null);
        }

        return new SizeLimits(
            Axis(sizing.Min.Width, sizing.Max.Width, sizing.WidthSteps, screen.Width),
            Axis(sizing.Min.Height, sizing.Max.Height, sizing.HeightSteps, screen.Height));
    }

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

        var fitted = stored with { W = limits.Width.Hold(stored.W), H = limits.Height.Hold(stored.H) };
        return fitted == stored ? stored : GridGeometry.Clamp(fitted, screen);
    }

    /// <summary>
    /// [S-11809] The boxes a picture paints its widgets in (a scene, the add page's tile): a lone widget
    /// as <see cref="Shown"/> paints it, the members of a group fitted together as one block
    /// (<see cref="GridFit.Layout"/>) so the edges they share stay shared at any scale and gap.
    /// <paramref name="limits"/> gives each widget's limits (its scale, the gap, the grid).
    /// </summary>
    public static IReadOnlyList<FracRect> ShownAll(IReadOnlyList<WidgetInstance> widgets, Func<WidgetInstance, SizeLimits?> limits, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(limits);
        var all = widgets.Select(limits).ToList();
        var shown = widgets.Select((w, i) => Shown(w.Rect, all[i], screen)).ToArray();
        foreach (var group in widgets.Select((w, i) => (w.Group, i)).Where(m => m.Group is not null).GroupBy(m => m.Group, StringComparer.Ordinal))
        {
            var at = group.Select(m => m.i).ToList();
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

    /// <summary>[S-2442] The card's box of a cell, in DIPs at widget scale 1: what a resize asks a kind's <see cref="WidgetDescriptor.SizeSets"/> for.</summary>
    public static SizeDip Wanted(FracRect cell, ScreenMetrics screen, double scale)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        return new(cell.W * screen.Width / s, cell.H * screen.Height / s);
    }

    /// <summary>
    /// [S-2442] The config a resize to <paramref name="cell"/> means, for a kind that has the hook; the
    /// config itself (the same instance) for every other kind or when the kind answers with it.
    /// [S-11807] On the grid (<paramref name="limits"/> on it) a stepped axis's cell is asked as the
    /// largest step its whole cells hold (<see cref="AxisLimits.Holds"/>), never the step its rounding
    /// comes nearest.
    /// </summary>
    public static WidgetConfig ConfigFor(WidgetConfig config, FracRect cell, ScreenMetrics screen, double scale, SizeLimits? limits)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!screen.IsUsable)
        {
            return config;
        }

        var held = limits is null ? cell : cell with { W = limits.Width.Holds(cell.W), H = limits.Height.Holds(cell.H) };
        return ConfigAt(config, Wanted(held, screen, scale));
    }

    /// <summary>
    /// [S-2442] The config a card box of <paramref name="box"/> DIPs (at widget scale 1) means, for a kind that has the hook: what a resize to that box
    /// gives, and what the renders draw a kind's min, natural and max with ([S-2448]). The
    /// config itself (the same instance) for every other kind or when the kind answers with it.
    /// </summary>
    public static WidgetConfig ConfigAt(WidgetConfig config, SizeDip box)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ModuleCatalog.Of(config.Kind).SizeSets is { } sets ? sets(config, box) : config;
    }

    /// <summary>
    /// [S-11601] What a resize to <paramref name="box"/> DIPs leaves, as the desktop does it: the config
    /// that size means (<see cref="ConfigAt"/>) and the box that config is painted at, the asked one fitted
    /// into its sizing ([S-2446]: an axis that follows the other, as a camera's height its width, or a
    /// grid's rows its columns, takes the size its config gives).
    /// </summary>
    public static (WidgetConfig Config, SizeDip Box) Resized(WidgetConfig config, SizeDip box)
    {
        var drawn = ConfigAt(config, box);
        return (drawn, Of(drawn).Fit(box));
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
    /// default config, that style): what the renders and the showcase draw a kind at.
    /// </summary>
    public static WidgetSizing Of(WidgetId kind, int? style = null)
    {
        var config = WidgetConfig.CreateDefault(kind);
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
    /// area); [S-11807] whole grid cells. Returns whether the rect changed.
    /// </summary>
    public static bool Refit(WidgetInstance widget, ScreenMetrics screen, double scale)
    {
        ArgumentNullException.ThrowIfNull(widget);
        var shown = Shown(widget.Rect, Limits(widget, screen, scale), screen);
        if (shown == widget.Rect)
        {
            return false;
        }

        widget.Rect = shown;
        return true;
    }
}
