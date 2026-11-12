using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>[S-1762] A grid of a meter's items (a core's ring, a core's graph): its columns, rows and one cell's size.</summary>
public readonly record struct MeterGrid(int Columns, int Rows, double CellWidth, double CellHeight)
{
    public static MeterGrid None { get; } = new(0, 0, 0, 0);

    public bool Shown => Columns > 0;
}

/// <summary>[S-6262] The battery style's picture: the body (stroke 2, the one radius), its fill's room and the terminal cap.</summary>
/// <param name="Height">The body's height; its width is twice that.</param>
/// <param name="CapHeight">The cap's height (its width is <see cref="MeterPlan.Cap"/>).</param>
/// <param name="Bolt">The charging bolt's glyph size (16, 32 or 48), or 0 for a body too small for one.</param>
public readonly record struct BatteryPicture(double Height, double CapHeight, double Bolt)
{
    public double BodyWidth => 2 * Height;

    /// <summary>The fill's room inside the body: its outline and 4 of air either side.</summary>
    public double InnerWidth => Math.Max(0, BodyWidth - (2 * MeterPlan.FillInset));

    public double InnerHeight => Math.Max(0, Height - (2 * MeterPlan.FillInset));

    /// <summary>The fill's width at a charge (0..1).</summary>
    public double FillWidth(double fraction) => double.IsFinite(fraction) ? Math.Clamp(fraction, 0, 1) * InnerWidth : 0;
}

/// <summary>
/// [S-6300] [S-11000] Where a meter atom's parts stand in the box it is given, on the family's blocks
/// ([S-6717]): the items' grids (rings, graphs), the battery's picture, the signal bars and the role a
/// pressure paints in. The sizes the atoms state are <see cref="MeterSizing"/>'s.
/// </summary>
public static class MeterPlan
{
    /// <summary>The least ring of an item worth drawing; under it the grid is not shown.</summary>
    public const double RingFloor = FamilyShape.Icon;

    /// <summary>The least dial (a gauge, a ring) a reading atom draws.</summary>
    public const double GaugeFloor = FamilyShape.IconHuge;

    /// <summary>The least core's graph cell: an icon's height of graph, its figure over its corner ([S-11005]).</summary>
    public const double GraphCellFloor = FamilyShape.Icon;

    /// <summary>The least width of a core's graph cell.</summary>
    public const double GraphCellWidthFloor = FamilyShape.IconLarge;

    /// <summary>The battery's terminal cap's width.</summary>
    public const double Cap = FamilySpace.S1;

    /// <summary>The fill's inset in the battery's body: its outline, then 4 of air.</summary>
    public const double FillInset = FamilyShape.Stroke + FamilySpace.S1;

    /// <summary>The signal bars' box: a little wider than tall, as Windows' Wi-Fi glyph.</summary>
    public const double SignalAspect = 1.25;

    /// <summary>The content box of a view <paramref name="area"/>: the inset at the sides and the foot (the weather and air widgets' faces read it).</summary>
    public static (double Width, double Height) Content(WidgetArea area)
        => (Math.Max(0, area.Width - (2 * FamilySpace.Inset)), Math.Max(0, area.Height - FamilySpace.Inset));

    /// <summary>
    /// [S-6302] The reading in a gauge's bowl: the Readout's step for the room inside the half ring (its
    /// width less 24 a side, its height less 8), standing on the gauge's foot (the air quality's gauge reads it).
    /// </summary>
    public static TypeStep GaugeReading(WidgetArea area, double diameter, ReadoutText worst)
        => ReadoutRules.Step(area, diameter - (2 * FamilySpace.S6), (diameter / 2) - FamilySpace.S2, worst);

    /// <summary>[S-6305] [S-11005] The rings' grid in a box of <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static MeterGrid Rings(double width, double height, int count)
    {
        var best = MeterGrid.None;

        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var size = Math.Floor(Math.Min(Cell(width, columns), Cell(height, rows)));
            if (size >= RingFloor && size > best.CellWidth)
            {
                best = new MeterGrid(columns, rows, size, size);
            }
        }

        return best;
    }

    /// <summary>[S-6317] [S-11005] The graphs' grid in a box of <paramref name="width"/> × <paramref name="height"/>.</summary>
    public static MeterGrid Graphs(double width, double height, int count)
    {
        var best = MeterGrid.None;
        var bestScore = 0.0;

        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (count + columns - 1) / columns;
            var cellWidth = Math.Floor(Cell(width, columns));
            var cellHeight = Math.Floor(Cell(height, rows));
            if (cellWidth < GraphCellWidthFloor || cellHeight < GraphCellFloor)
            {
                continue;
            }

            var score = Math.Min(cellWidth / 1.5, cellHeight);
            if (score > bestScore)
            {
                bestScore = score;
                best = new MeterGrid(columns, rows, cellWidth, cellHeight);
            }
        }

        return best;
    }

    /// <summary>[S-6262] [S-11004] The battery's picture for a body <paramref name="height"/> tall: its cap and the bolt that fits it.</summary>
    public static BatteryPicture Picture(double height)
    {
        var inner = height - (2 * FillInset);
        var bolt = 0.0;
        foreach (var icon in FamilyShape.Icons)
        {
            if (icon <= inner * 0.75)
            {
                bolt = icon;
            }
        }

        return new BatteryPicture(height, Math.Round(height * 0.4), bolt);
    }

    /// <summary>[S-6263] [S-11004] Four bars in a box <paramref name="height"/> tall, at most <paramref name="room"/> wide.</summary>
    public static IReadOnlyList<(double Width, double Height)> SignalBars(double room, double height)
    {
        var width = Math.Min(room, Math.Round(height * SignalAspect));
        var bar = Math.Floor((width - (3 * FamilySpace.S1)) / PingSignal.BarCount);
        if (bar < 1)
        {
            return [];
        }

        return [.. Enumerable.Range(0, PingSignal.BarCount).Select(i => (bar, Math.Round(height * (i + 1) / PingSignal.BarCount)))];
    }

    /// <summary>
    /// [S-1123] [S-6706] The role a reading's pressure paints its chart in: the accent while it is normal
    /// or healthy (no success green), Caution when elevated, Critical when critical; the disabled role for
    /// a thing not on this PC ([S-1940]). Never the reading's own text, which stays legible.
    /// </summary>
    public static ColourRole Role(MeterPressure pressure, bool absent) => absent ? ColourRole.TextDisabled : pressure switch
    {
        MeterPressure.Critical => ColourRole.Critical,
        MeterPressure.Elevated => ColourRole.Caution,
        _ => ColourRole.Accent,
    };

    /// <summary>A cell's side when <paramref name="count"/> of them, 8 apart, share <paramref name="length"/>.</summary>
    private static double Cell(double length, int count) => count <= 0 ? 0 : (length - ((count - 1) * FamilySpace.S2)) / count;
}
