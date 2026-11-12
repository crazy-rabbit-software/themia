using Themia.App.Widgets.Meters;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-11004] The battery picture of a reading atom: its body's height (its width twice that, the cap beside it).</summary>
public readonly record struct BatteryBody(double Height)
{
    public double Width => 2 * Height;
}

/// <summary>
/// [S-11000]..[S-11009] The sizes of a system subject's atoms and where their parts stand, on the family's
/// tokens (inset 12, header 36, the reading at 28, rows 28): one rule for the sizing a kind states and
/// the view that draws it, so the two agree. Pure.
/// <list type="bullet">
/// <item>A <b>reading</b> keeps its header: the number at 28 under it, 120..320 wide, one height; its other
/// drawings (a gauge, a ring, a bar row, the battery, the signal bars) each their own sizing.</item>
/// <item>A <b>history</b> starts headerless: the band edge to edge, 160..640 × 48..200.</item>
/// <item><b>Items</b> (cores) start headerless and stand in the card's inset; rows of items (engines) keep the header.</item>
/// </list>
/// </summary>
public static class MeterSizing
{
    public const double ReadingMinWidth = 120;

    public const double ReadingNaturalWidth = 160;

    public const double ReadingMaxWidth = 320;

    /// <summary>The reading's line: the family's 28 ([S-2449]).</summary>
    public static double Line => FamilyType.Big28.LineHeight;

    /// <summary>Where a part under the reading starts: the reading's line, then 8.</summary>
    public static double PartTop => Line + FamilySpace.S2;

    /// <summary>A gauge, a ring: the smallest and largest diameters offered.</summary>
    public const double DialMax = ReadingMaxWidth - (2 * FamilySpace.Inset);

    /// <summary>The battery's body: least, natural and most height.</summary>
    public const double BodyMin = 24;

    public const double BodyNatural = 40;

    public const double BodyMax = 64;

    /// <summary>The signal bars' box: least, natural and most height.</summary>
    public const double SignalMin = 24;

    public const double SignalNatural = 32;

    public const double SignalMax = 48;

    /// <summary>The battery's terminal cap's width.</summary>
    public const double Cap = FamilySpace.S1;

    /// <summary>The widest reading at 28 (its lead glyph, value and unit).</summary>
    public static double ReadingWidth(ReadoutText worst) => Math.Ceiling(ReadoutRules.Width(FamilyType.Big28, worst));

    /// <summary>[S-11001] The width range of a reading whose content is <paramref name="content"/> wide at least.</summary>
    private static (double Min, double Natural, double Max) Widths(double content, double natural = 0)
    {
        var least = Math.Max(ReadingMinWidth, Four(content + (2 * FamilySpace.Inset)));
        var start = Math.Max(least, Math.Max(ReadingNaturalWidth, natural));
        return (least, start, Math.Max(start, ReadingMaxWidth));
    }

    /// <summary>
    /// [S-11001] A reading atom's sizing in <paramref name="look"/>, for its widest reading
    /// <paramref name="worst"/>; <paramref name="rows"/>: the bar rows a <see cref="MeterLook.Rows"/> reading draws.
    /// </summary>
    public static WidgetSizing Reading(MeterLook look, ReadoutText worst, int rows = 1)
    {
        var reading = ReadingWidth(worst);
        var top = FamilySpace.ContentTop;
        var foot = FamilySpace.Inset;

        switch (look)
        {
            case MeterLook.Numbers:
            {
                var w = Widths(ReadingWidth(worst with { Lead = true }));
                var h = top + Line + FamilySpace.S2 + Line + foot;
                return WidgetSizing.Range(new(w.Min, h), new(w.Natural, h), new(w.Max, h));
            }

            case MeterLook.Gauge:
            {
                var (least, start) = GaugeDiameters(worst);
                return Dial(least, start, d => top + ChartRules.GaugeHeight(d) + foot);
            }

            case MeterLook.Ring:
            {
                var (least, start) = RingDiameters(worst);
                return Dial(least, start, d => top + d + foot);
            }

            case MeterLook.Rows:
            {
                // [S-11001] The bar row keeps its meter between its two figures only from 240 wide.
                var h = top + PartTop + (Math.Max(1, rows) * FamilyShape.Row1) + foot;
                return WidgetSizing.Range(new(240, h), new(280, h), new(480, h));
            }

            case MeterLook.Battery:
            {
                double Height(double body) => top + PartTop + body + foot;
                double Width(double body) => Math.Max(ReadingMinWidth, Four(Math.Max(reading, (2 * body) + Cap) + (2 * FamilySpace.Inset)));
                return WidgetSizing.Range(
                    new(Width(BodyMin), Height(BodyMin)),
                    new(Math.Max(ReadingNaturalWidth, Width(BodyNatural)), Height(BodyNatural)),
                    new(Math.Max(ReadingMaxWidth, Width(BodyMax)), Height(BodyMax)));
            }

            case MeterLook.Signal:
            {
                double Height(double box) => top + PartTop + box + foot;
                var w = Widths(Math.Max(reading, Math.Round(SignalMax * MeterPlan.SignalAspect)));
                return WidgetSizing.Range(new(w.Min, Height(SignalMin)), new(w.Natural, Height(SignalNatural)), new(w.Max, Height(SignalMax)));
            }

            default:
            {
                var w = Widths(reading);
                var h = top + Line + foot;
                return WidgetSizing.Range(new(w.Min, h), new(w.Natural, h), new(w.Max, h));
            }
        }
    }

    private static WidgetSizing Dial(double least, double start, Func<double, double> height)
    {
        var inset = 2 * FamilySpace.Inset;
        least = Math.Max(least, ReadingMinWidth - inset);
        start = Math.Max(least, start);
        var most = Math.Max(start, DialMax);
        return WidgetSizing.Range(
            new(least + inset, height(least)),
            new(start + inset, height(start)),
            new(most + inset, height(most)));
    }

    /// <summary>[S-11003] The gauge's least diameter (the reading at 28 in its bowl: 24 either side, 8 over its foot) and its natural one.</summary>
    public static (double Least, double Natural) GaugeDiameters(ReadoutText worst)
    {
        var least = Four(Math.Max(MeterPlan.GaugeFloor, Math.Max(ReadingWidth(worst) + (2 * FamilySpace.S6), 2 * (Line + FamilySpace.S2))));
        return (least, Math.Max(least, 136));
    }

    /// <summary>[S-11003] The ring's least diameter (the reading at 28 in its hole, [S-6717]) and its natural one.</summary>
    public static (double Least, double Natural) RingDiameters(ReadoutText worst)
    {
        var least = Four(Math.Max(MeterPlan.GaugeFloor, Math.Max((ReadingWidth(worst) / 0.75) + (2 * FamilyShape.Stroke), (2 * Line) + (2 * FamilyShape.Stroke))));
        return (least, Math.Max(least, 104));
    }

    /// <summary>A length up to the next multiple of 4, the family's scale ([S-6703]).</summary>
    private static double Four(double length) => Math.Ceiling(length / FamilySpace.S1) * FamilySpace.S1;

    /// <summary>[S-11003] The gauge in a content box: as wide as it is, while its half fits the height.</summary>
    public static double GaugeIn(double width, double height)
        => Math.Max(0, Math.Floor(Math.Min(width, 2 * (height - (FamilyShape.Stroke / 2)))));

    /// <summary>[S-11003] The ring in a content box: its short side.</summary>
    public static double RingIn(double width, double height) => Math.Max(0, Math.Floor(Math.Min(width, height)));

    /// <summary>[S-11004] The battery under the reading in a content box: as tall as the room under the reading, while twice that and its cap fit the width.</summary>
    public static BatteryBody BatteryIn(double width, double height)
        => new(Math.Clamp(Math.Floor(Math.Min(height - PartTop, (width - Cap) / 2)), 0, BodyMax));

    /// <summary>[S-11004] The signal bars' box height under the reading in a content box.</summary>
    public static double SignalIn(double width, double height)
        => Math.Clamp(Math.Floor(Math.Min(height - PartTop, width / MeterPlan.SignalAspect)), 0, SignalMax);

    /// <summary>[S-11002] A history atom: the band edge to edge, no header by default.</summary>
    public static WidgetSizing History { get; } = WidgetSizing.Range(new(160, 48), new(160, 64), new(640, 200));

    /// <summary>[S-11005] Items as columns (the cores' bars), headerless, in the card's inset.</summary>
    public static WidgetSizing Columns { get; } = WidgetSizing.Range(new(120, 48), new(160, 64), new(640, 200));

    /// <summary>[S-11005] Items as rings (the cores' grid), headerless, in the card's inset.</summary>
    public static WidgetSizing Tiles { get; } = WidgetSizing.Range(new(112, 112), new(144, 144), new(360, 360));

    /// <summary>[S-11005] Items as graphs (the cores' own histories), headerless, in the card's inset.</summary>
    public static WidgetSizing Graphs { get; } = WidgetSizing.Range(new(200, 128), new(280, 200), new(640, 400));

    /// <summary>[S-11006] <paramref name="rows"/> rows of items (the engines) under a header: one 28 row each, one height.</summary>
    public static WidgetSizing ItemRows(int rows)
    {
        var h = FamilySpace.ContentTop + (Math.Max(1, rows) * FamilyShape.Row1) + FamilySpace.Inset;
        return WidgetSizing.Range(new(200, h), new(240, h), new(420, h));
    }
}
