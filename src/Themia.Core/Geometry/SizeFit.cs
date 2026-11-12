using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-2444] One axis of a widget's sizing on a display, as fractions of the screen of the widget's
/// <b>cell</b> (the card plus the gap): the range a resize may take (<see cref="Min"/>..<see cref="Max"/>,
/// on steps where the axis has them) and, where the axis follows the config, the one size it is laid
/// out at (<see cref="Pinned"/>).
/// </summary>
public readonly record struct AxisLimits(double Min, double Max, double StepBase = 0, double Step = 0, double? Pinned = null)
{
    /// <summary>The axis takes steps.</summary>
    public bool Stepped => Step > 0;

    /// <summary>[S-2444] No handle resizes it.</summary>
    public bool Fixed => Max - Min < 1e-9;

    /// <summary>[S-2445] A resize's size on this axis: held to the range, on its nearest step.</summary>
    public double Range(double size)
    {
        var held = double.IsFinite(size) ? Math.Clamp(size, Min, Math.Max(Min, Max)) : Min;
        if (!Stepped)
        {
            return held;
        }

        var n = Math.Round((held - StepBase) / Step, MidpointRounding.AwayFromZero);
        return Math.Clamp(StepBase + (n * Step), Min, Math.Max(Min, Max));
    }

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
    /// [S-2444] What a top or bottom header adds to a card's height (its 20 line and the 4 under it;
    /// the inset is there either way), and what a side strip adds to its width.
    /// </summary>
    public const double HeaderCost = FamilySpace.HeaderLine + FamilySpace.HeaderGap;

    /// <summary>
    /// [S-2444] <paramref name="sizing"/>, stated with <paramref name="stated"/> (the kind's default
    /// header), for a card that shows <paramref name="shown"/>: a header the kind did not count adds its
    /// line, one it counted and the card hides takes it away. A legacy sizing is left as it is.
    /// </summary>
    public static WidgetSizing ForHeader(WidgetSizing sizing, HeaderPlacement stated, HeaderPlacement shown)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        if (sizing.IsLegacy)
        {
            return sizing;
        }

        var (sw, sh) = Cost(stated);
        var (cw, ch) = Cost(shown);
        var (dw, dh) = (cw - sw, ch - sh);
        if (dw == 0 && dh == 0)
        {
            return sizing;
        }

        SizeDip Add(SizeDip s) => new(s.Width + dw, s.Height + dh);
        SizeSteps? Shift(SizeSteps? s, double d) => s is null ? null : s with { Base = s.Base + d };
        return sizing with
        {
            Min = Add(sizing.Min),
            Natural = Add(sizing.Natural),
            Max = Add(sizing.Max),
            WidthSteps = Shift(sizing.WidthSteps, dw),
            HeightSteps = Shift(sizing.HeightSteps, dh),
        };
    }

    /// <summary>
    /// [S-2444] A widget's sizing on <paramref name="screen"/>: each DIP size times the content
    /// <paramref name="scale"/> ([S-6532]: the display's and the widget's own) plus the
    /// <paramref name="gapPx"/> round the card, as a fraction of the screen. Null for a legacy sizing
    /// or an unusable screen: the old way.
    /// </summary>
    public static SizeLimits? Limits(WidgetSizing sizing, ScreenMetrics screen, double scale, double gapPx)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        if (sizing.IsLegacy || !screen.IsUsable)
        {
            return null;
        }

        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var gap = double.IsFinite(gapPx) && gapPx > 0 ? gapPx : 0;

        AxisLimits Axis(double min, double max, SizeSteps? steps, double screenPx)
        {
            double Cell(double dip) => ((dip * s) + gap) / screenPx;
            return steps is null
                ? new AxisLimits(Cell(min), Cell(max))
                : new AxisLimits(Cell(min), Cell(max), Cell(steps.Base), steps.Step * s / screenPx, steps.Count is { } n ? Cell(steps.At(n)) : null);
        }

        return new SizeLimits(
            Axis(sizing.Min.Width, sizing.Max.Width, sizing.WidthSteps, screen.Width),
            Axis(sizing.Min.Height, sizing.Max.Height, sizing.HeightSteps, screen.Height));
    }

    /// <summary>
    /// [S-2446] The box a widget is painted in: its stored rect fitted into its sizing, anchored at its
    /// top-left, then kept in reach as any rect ([S-09]). The stored rect is not changed by being shown;
    /// without limits (a legacy kind) it is shown as stored.
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

    /// <summary>[S-2442] The card's box of a cell, in DIPs at widget scale 1: what a resize asks a kind's <see cref="WidgetDescriptor.SizeSets"/> for.</summary>
    public static SizeDip Wanted(FracRect cell, ScreenMetrics screen, double scale, double gapPx)
    {
        var s = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var gap = double.IsFinite(gapPx) && gapPx > 0 ? gapPx : 0;
        return new(((cell.W * screen.Width) - gap) / s, ((cell.H * screen.Height) - gap) / s);
    }

    /// <summary>
    /// [S-2442] The config a resize to <paramref name="cell"/> means, for a kind that has the hook; the
    /// config itself (the same instance) for every other kind or when the kind answers with it.
    /// </summary>
    public static WidgetConfig ConfigFor(WidgetConfig config, FracRect cell, ScreenMetrics screen, double scale, double gapPx, HeaderPlacement shown)
    {
        ArgumentNullException.ThrowIfNull(config);
        var kind = ModuleCatalog.Of(config.Kind);
        if (kind.SizeSets is not { } sets || !screen.IsUsable)
        {
            return config;
        }

        // The kind counts its default header; the card's own header is taken off before it is asked.
        var wanted = Wanted(cell, screen, scale, gapPx);
        var (cw, ch) = Cost(shown);
        var (sw, sh) = Cost(WidgetInstance.DefaultHeader(config.Kind));
        return sets(config, new SizeDip(wanted.Width - (cw - sw), wanted.Height - (ch - sh)));
    }

    /// <summary>What a header at <paramref name="h"/> costs the card: its line and gap, across the edge it stands on.</summary>
    private static (double W, double H) Cost(HeaderPlacement h) => h switch
    {
        HeaderPlacement.Top or HeaderPlacement.Bottom => (0, HeaderCost),
        HeaderPlacement.Left or HeaderPlacement.Right => (HeaderCost, 0),
        _ => (0, 0),
    };

    /// <summary>[S-2444] A widget's sizing as its card shows it: its kind's for its config, for the header it shows.</summary>
    public static WidgetSizing Of(WidgetInstance widget)
    {
        ArgumentNullException.ThrowIfNull(widget);
        return Of(widget.Config, widget.ShownHeader);
    }

    /// <summary>[S-2444] The sizing of <paramref name="config"/> on a card that shows <paramref name="shown"/>.</summary>
    public static WidgetSizing Of(WidgetConfig config, HeaderPlacement shown)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ForHeader(ModuleCatalog.Of(config.Kind).Sizing(config), WidgetInstance.DefaultHeader(config.Kind), shown);
    }

    /// <summary>
    /// [S-2448] The sizing a new widget of <paramref name="kind"/> has in <paramref name="style"/> (its
    /// default config, that style), on a card showing <paramref name="header"/> (its default when null):
    /// what the renders and the showcase draw a converted kind at.
    /// </summary>
    public static WidgetSizing Of(WidgetId kind, int? style = null, HeaderPlacement? header = null)
    {
        var config = WidgetConfig.CreateDefault(kind);
        if (style is { } chosen && config is IStyled styled)
        {
            styled.StyleValue = chosen;
        }

        return Of(config, header ?? WidgetInstance.DefaultHeader(kind));
    }

    /// <summary>
    /// [S-2448] A converted kind is checked at its range, not the seven tiers: its Min, its Natural and
    /// its Max, named so. None for a legacy kind (it keeps the tiers).
    /// </summary>
    public static IReadOnlyList<WidgetTier> RangeTiers(WidgetSizing sizing)
    {
        ArgumentNullException.ThrowIfNull(sizing);
        return sizing.IsLegacy
            ? []
            : [new("min", sizing.Min.Width, sizing.Min.Height), new("natural", sizing.Natural.Width, sizing.Natural.Height), new("max", sizing.Max.Width, sizing.Max.Height)];
    }

    /// <summary>[S-2448] The sizes a kind is checked at: its range when converted, else the seven tiers.</summary>
    public static IReadOnlyList<WidgetTier> CheckedAt(WidgetSizing sizing)
        => RangeTiers(sizing) is { Count: > 0 } range ? range : WidgetTiers.All;

    /// <summary>[S-2444] <see cref="Of(WidgetInstance)"/> on a display.</summary>
    public static SizeLimits? Limits(WidgetInstance widget, ScreenMetrics screen, double scale, double gapPx)
        => Limits(Of(widget), screen, scale, gapPx);

    /// <summary>
    /// [S-2447] A config change that changed the sizing: the stored rect fitted into the new one in the
    /// same mutation (anchored at its top-left, shifted back into reach where that would leave the work
    /// area). Returns whether the rect changed.
    /// </summary>
    public static bool Refit(WidgetInstance widget, ScreenMetrics screen, double scale, double gapPx)
    {
        ArgumentNullException.ThrowIfNull(widget);
        var shown = Shown(widget.Rect, Limits(widget, screen, scale, gapPx), screen);
        if (shown == widget.Rect)
        {
            return false;
        }

        widget.Rect = shown;
        return true;
    }
}
