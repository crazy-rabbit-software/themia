using Themia.Core.Model;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-15503] [S-15504] [S-15514] [S-15515] A repeater's slots on its display, in fractions of it: the template row's
/// extent (from the repeater's corner to its members' far edges), the pitch from one slot to the next (the extent and
/// the spacing in grid cells), and how many slots its box holds across and down. Slot 0 is the template row at the
/// repeater's corner; the others follow across, then down. Pure.
/// </summary>
/// <param name="Box">The repeater's rect.</param>
/// <param name="ExtentW">The template row's width.</param>
/// <param name="ExtentH">The template row's height.</param>
/// <param name="PitchX">From one column of slots to the next.</param>
/// <param name="PitchY">From one line of slots to the next.</param>
/// <param name="Columns">Slots across the box (Down: 1).</param>
/// <param name="Lines">Slots down the box (Across: 1).</param>
/// <param name="Direction">Which way the rows run.</param>
public readonly record struct RepeaterGrid(FracRect Box, double ExtentW, double ExtentH, double PitchX, double PitchY, int Columns, int Lines, RepeatDirection Direction)
{
    private const double Slack = 1e-9;

    /// <summary>[S-15514] How many slots the box holds.</summary>
    public int Fit => Columns * Lines;

    /// <summary>[S-15515] How many values one step of the scroll passes: a row, or (Wrap) a line of columns.</summary>
    public int ScrollUnit => Direction == RepeatDirection.Wrap ? Columns : 1;

    /// <summary>The slots of <paramref name="box"/> for a template whose members stand at <paramref name="template"/>.</summary>
    public static RepeaterGrid Of(FracRect box, IEnumerable<FracRect> template, IRepeater repeater, ScreenMetrics screen)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(repeater);
        var rects = template.Where(r => r.IsFinite).ToList();
        var (cols, rows) = GridGeometry.GridDims(screen);
        var cellW = 1.0 / cols;
        var cellH = 1.0 / rows;

        // [S-15503] The day box: its set size in cells, never less than its parts reach from its corner. No part and no size:
        // one slot the size of the box, nothing repeats.
        var partsW = rects.Count == 0 ? 0 : rects.Max(r => r.Right) - box.X;
        var partsH = rects.Count == 0 ? 0 : rects.Max(r => r.Bottom) - box.Y;
        var extentW = rects.Count == 0 && repeater.DayWidth <= 0 ? box.W : Math.Max(cellW, Math.Max(partsW, repeater.DayWidth * cellW));
        var extentH = rects.Count == 0 && repeater.DayHeight <= 0 ? box.H : Math.Max(cellH, Math.Max(partsH, repeater.DayHeight * cellH));
        var (gapX, gapY) = Gaps(repeater, cellW, cellH);
        var pitchX = extentW + gapX;
        var pitchY = extentH + gapY;
        var across = Math.Max(1, (int)Math.Floor((box.W + gapX + Slack) / pitchX));
        var down = Math.Max(1, (int)Math.Floor((box.H + gapY + Slack) / pitchY));
        return repeater.Direction switch
        {
            RepeatDirection.Across => new(box, extentW, extentH, pitchX, pitchY, across, 1, repeater.Direction),
            RepeatDirection.Wrap => new(box, extentW, extentH, pitchX, pitchY, across, down, repeater.Direction),
            _ => new(box, extentW, extentH, pitchX, pitchY, 1, down, repeater.Direction),
        };
    }

    /// <summary>[S-15503] [S-15504] The spacing as fractions: along the rows for Down and Across, both ways for Wrap.</summary>
    private static (double X, double Y) Gaps(IRepeater repeater, double cellW, double cellH)
    {
        var spacing = Math.Max(0, repeater.Spacing);
        return repeater.Direction switch
        {
            RepeatDirection.Across => (spacing * cellW, 0),
            RepeatDirection.Wrap => (Math.Max(0, repeater.ColumnSpacing) * cellW, spacing * cellH),
            _ => (0, spacing * cellH),
        };
    }

    /// <summary>How far slot <paramref name="slot"/> stands from the template's corner.</summary>
    public (double X, double Y) Offset(int slot)
    {
        var columns = Math.Max(1, Columns);
        return (slot % columns * PitchX, slot / columns * PitchY);
    }

    /// <summary>Slot <paramref name="slot"/>'s box: the template row's extent moved there.</summary>
    public FracRect Slot(int slot)
    {
        var (x, y) = Offset(slot);
        return new FracRect(Box.X + x, Box.Y + y, ExtentW, ExtentH);
    }

    /// <summary>
    /// [S-15526] The slot whose pitch holds the point (<paramref name="x"/>, <paramref name="y"/>): its spacing after it
    /// counts as that slot's. -1 off the box.
    /// </summary>
    public int SlotAt(double x, double y)
    {
        if (x < Box.X - Slack || y < Box.Y - Slack || x > Box.Right + Slack || y > Box.Bottom + Slack || !(PitchX > 0) || !(PitchY > 0))
        {
            return -1;
        }

        var column = Math.Clamp((int)Math.Floor((x - Box.X) / PitchX), 0, Columns - 1);
        var line = Math.Clamp((int)Math.Floor((y - Box.Y) / PitchY), 0, Lines - 1);
        return (line * Columns) + column;
    }

    /// <summary>[S-15515] The furthest scroll for <paramref name="count"/> values: none while they all fit.</summary>
    public int MaxScroll(int count)
    {
        if (count <= Fit)
        {
            return 0;
        }

        return Direction == RepeatDirection.Wrap
            ? ((count + Columns - 1) / Columns) - Lines
            : count - Fit;
    }

    /// <summary>[S-15515] The index of the first value shown (slot 0's) at <paramref name="scroll"/>, kept in range for <paramref name="count"/>.</summary>
    public int First(int scroll, int count) => Math.Clamp(scroll, 0, MaxScroll(count)) * ScrollUnit;

    /// <summary>
    /// [S-15514] The repeater's box at its natural for <paramref name="count"/> values: every value's slot (no value: the
    /// template and the status's slot), Down and Across along their axis at the template's extent across it, Wrap keeping
    /// its width (and so its columns) and taking the lines it needs. Kept on the display (<paramref name="screen"/>'s
    /// allowed area as far as it reaches down and right).
    /// </summary>
    public FracRect Natural(int count)
    {
        var slots = Math.Max(2, count);
        var gapX = PitchX - ExtentW;
        var gapY = PitchY - ExtentH;
        var (w, h) = Direction switch
        {
            RepeatDirection.Across => ((slots * PitchX) - gapX, ExtentH),
            RepeatDirection.Wrap => (Box.W, ((slots + Columns - 1) / Columns * PitchY) - gapY),
            _ => (ExtentW, (slots * PitchY) - gapY),
        };

        return new FracRect(Box.X, Box.Y, Math.Min(w, Math.Max(ExtentW, 1 - Box.X)), Math.Min(h, Math.Max(ExtentH, 1 - Box.Y)));
    }
}
