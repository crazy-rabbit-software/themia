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
/// [S-1762] [S-6300] How a meter lays its reading and the part beside it out, on the family's blocks
/// ([S-6702], [S-6717]): every look reads the same way. The view's area is the card under the header;
/// the content stands in the 12 inset at the sides and the foot and starts at the view's top (the
/// header's 8 above it). The reading is a Readout at the top (start-aligned) on the step its widest
/// case takes in the area, so a data widget's number takes 28, or 40 once expanded both ways, and stops ([S-6702]); what it
/// leaves under it, 8 down, is the look's: the history band edge to edge to the card's foot, the gauge,
/// the items' columns, rows, rings or graphs, the battery or the signal bars. Room left at L and XL goes
/// to that part (a longer chart, larger rings), never to a bigger number ([S-666]).
/// </summary>
public static class MeterPlan
{
    /// <summary>The least history worth drawing under a reading.</summary>
    public const double BandFloor = FamilySpace.S6;

    /// <summary>The least part (columns, rows, a picture) worth drawing under a reading.</summary>
    public const double PartFloor = FamilyShape.Icon;

    /// <summary>The least ring of an item worth drawing; under it the grid is not shown.</summary>
    public const double RingFloor = FamilyShape.Icon;

    /// <summary>The least gauge worth drawing: under it the reading stands alone.</summary>
    public const double GaugeFloor = FamilyShape.IconHuge;

    /// <summary>A core's graph cell with its figure: a Caption figure over at least an icon's height of graph, 4 between.</summary>
    public static double GraphFigureFloor => FamilyType.Caption.LineHeight + FamilySpace.S1 + FamilyShape.Icon;

    /// <summary>The least core's graph cell: an icon's height of graph alone (its figure shown from <see cref="GraphFigureFloor"/>).</summary>
    public const double GraphCellFloor = FamilyShape.Icon;

    /// <summary>The least width of a core's graph cell.</summary>
    public const double GraphCellWidthFloor = FamilyShape.IconLarge;

    /// <summary>[S-6317] Whether a core's cell of <paramref name="height"/> shows its figure over its graph.</summary>
    public static bool GraphFigure(double height) => height >= GraphFigureFloor;

    /// <summary>The battery's terminal cap's width.</summary>
    public const double Cap = FamilySpace.S1;

    /// <summary>The fill's inset in the battery's body: its outline, then 4 of air.</summary>
    public const double FillInset = FamilyShape.Stroke + FamilySpace.S1;

    /// <summary>The signal bars' box: a little wider than tall, as Windows' Wi-Fi glyph.</summary>
    public const double SignalAspect = 1.25;

    /// <summary>The content box of a view <paramref name="area"/>: the inset at the sides and the foot.</summary>
    public static (double Width, double Height) Content(WidgetArea area)
        => (Math.Max(0, area.Width - (2 * FamilySpace.Inset)), Math.Max(0, area.Height - FamilySpace.Inset));

    /// <summary>[S-6702] The reading's step: the Readout's for the content box and the widest reading.</summary>
    public static TypeStep Reading(WidgetArea area, ReadoutText worst)
    {
        var (w, h) = Content(area);
        return ReadoutRules.Step(area, w, h, worst);
    }

    /// <summary>Where the part under a reading of <paramref name="step"/> starts: its line, then 8.</summary>
    public static double PartTop(TypeStep step) => step.LineHeight + FamilySpace.S2;

    /// <summary>The part's height under the reading, inside the foot's inset (0 when there is no room).</summary>
    public static double PartHeight(WidgetArea area, TypeStep step) => Math.Max(0, Content(area).Height - PartTop(step));

    /// <summary>
    /// [S-1824] The history band's height: from 8 under the reading to the card's foot, edge to edge; 0
    /// when it is switched off or shorter than <see cref="BandFloor"/>.
    /// </summary>
    public static double Band(WidgetArea area, TypeStep step, bool graphOn)
    {
        var height = area.Height - PartTop(step);
        return graphOn && height >= BandFloor ? height : 0;
    }

    /// <summary>
    /// [S-6302] The gauge: a half ring as wide as the content allows while its half and its stroke fit
    /// the height, start-aligned; 0 when it would be smaller than <see cref="GaugeFloor"/>.
    /// </summary>
    public static double Gauge(WidgetArea area)
    {
        var (w, h) = Content(area);
        var diameter = Math.Floor(Math.Min(w, 2 * (h - (FamilyShape.Stroke / 2))));
        return diameter >= GaugeFloor ? diameter : 0;
    }

    /// <summary>
    /// [S-6302] The reading in a gauge's bowl: the Readout's step for the room inside the half ring (its
    /// width less 24 a side, its height less 8), standing on the gauge's foot.
    /// </summary>
    public static TypeStep GaugeReading(WidgetArea area, double diameter, ReadoutText worst)
        => ReadoutRules.Step(area, diameter - (2 * FamilySpace.S6), (diameter / 2) - FamilySpace.S2, worst);

    /// <summary>[S-6303] Whether the columns (one 4 track per item) are drawn: a part's room under the reading.</summary>
    public static bool Columns(WidgetArea area, TypeStep step, int count)
        => count > 0 && PartHeight(area, step) >= PartFloor && Content(area).Width / count >= FamilyShape.Track + FamilySpace.S1;

    /// <summary>[S-6304] How many of the items' rows (one 28 row each) fit under the reading.</summary>
    public static int Rows(WidgetArea area, TypeStep step, int count)
        => Math.Min(count, RowRules.Fit(PartHeight(area, step), 1));

    /// <summary>
    /// [S-6305] The items' rings: the grid of <paramref name="count"/> square cells 8 apart whose ring
    /// is largest in the room under the reading; none when that ring is under <see cref="RingFloor"/>.
    /// </summary>
    public static MeterGrid Rings(WidgetArea area, TypeStep step, int count)
        => Rings(Content(area).Width, PartHeight(area, step), count);

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

    /// <summary>
    /// [S-6317] The cores' graphs: the grid of <paramref name="count"/> cells 8 apart filling the room
    /// under the reading, its shape the one whose cells come closest to the band's proportion (half again
    /// as wide as tall) at their largest; none when a cell would be under its floor (32 wide, 16 tall). A
    /// cell shows its figure over its graph from <see cref="GraphFigureFloor"/>.
    /// </summary>
    public static MeterGrid Graphs(WidgetArea area, TypeStep step, int count)
        => Graphs(Content(area).Width, PartHeight(area, step), count);

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

    /// <summary>
    /// [S-6264] Two readings (the network's download and upload): side by side in two columns 12 apart
    /// on an expanded width, else one over the other 8 apart; both on the step the widest one takes in
    /// its half.
    /// </summary>
    public static (bool Side, TypeStep Step) Numbers(WidgetArea area, ReadoutText worst)
    {
        var (w, h) = Content(area);
        var side = area.WidthClass >= SizeClass.Expanded;
        var step = side
            ? ReadoutRules.Step(area, (w - FamilySpace.ColumnGap) / 2, h, worst)
            : ReadoutRules.Step(area, w, (h - FamilySpace.S2) / 2, worst);
        return (side, step);
    }

    /// <summary>
    /// [S-6262] The battery under the reading: a body twice as wide as tall and its 4 cap, as large as
    /// the room allows, start-aligned; none under <see cref="PartFloor"/>. The bolt is the largest icon
    /// size within three quarters of the fill's room.
    /// </summary>
    public static BatteryPicture? Battery(WidgetArea area, TypeStep step)
    {
        var width = Content(area).Width;
        var height = Math.Floor(Math.Min(PartHeight(area, step), (width - Cap) / 2));
        return height < PartFloor ? null : Picture(height);
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

    /// <summary>
    /// [S-6266] A ring of the reading's share, as large as the content allows, start-aligned; 0 under
    /// <see cref="GaugeFloor"/>.
    /// </summary>
    public static double Ring(WidgetArea area)
    {
        var (w, h) = Content(area);
        var diameter = Math.Floor(Math.Min(w, h));
        return diameter >= GaugeFloor ? diameter : 0;
    }

    /// <summary>[S-6266] The reading in a ring's hole: the Readout's step for three quarters of the hole's width and half its height.</summary>
    public static TypeStep RingReading(WidgetArea area, double diameter, ReadoutText worst)
    {
        var hole = diameter - (2 * FamilyShape.Stroke);
        return ReadoutRules.Step(area, hole * 0.75, hole / 2, worst);
    }

    /// <summary>
    /// [S-6263] The signal bars under the reading: a box of <see cref="SignalAspect"/> as tall as the room,
    /// four bars 4 apart rising left to right, bar <c>i</c> <c>(i + 1) / 4</c> of the height; none when
    /// the room is under <see cref="PartFloor"/>.
    /// </summary>
    public static IReadOnlyList<(double Width, double Height)> Signal(WidgetArea area, TypeStep step)
    {
        var height = Math.Floor(PartHeight(area, step));
        if (height < PartFloor)
        {
            return [];
        }

        return SignalBars(Content(area).Width, height);
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
