using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

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

    /// <summary>The least dial (a gauge, a ring) a reading atom draws.</summary>
    public const double GaugeFloor = FamilyShape.IconHuge;



    /// <summary>The battery's terminal cap's width.</summary>
    public const double Cap = FamilySpace.S1;

    /// <summary>The fill's inset in the battery's body: its outline, then 4 of air.</summary>
    public const double FillInset = FamilyShape.Stroke + FamilySpace.S1;

    /// <summary>The signal bars' box: a little wider than tall, as Windows' Wi-Fi glyph.</summary>
    public const double SignalAspect = 1.25;

    /// <summary>
    /// [S-15746] A core's column (the bars): one grid cell across, its 4 bar standing in the middle from the card's top edge
    /// to its foot (WR-4, WR-6: one fixed size; "Plot edge to edge").
    /// </summary>
    public const double CoreColumn = 10;

    /// <summary>[S-15746] A core's ring cell (the grid): three grid cells square, the ring 4 inside it on every side.</summary>
    public const double CoreRing = 30;

    /// <summary>[S-15746] A core's graph cell: its history edge to edge with its load in Caption over its top corner.</summary>
    public static SizeDip CoreGraph => new(60, 40);

    /// <summary>
    /// [S-15746] The cores' cells in a card <paramref name="width"/> × <paramref name="height"/> (owner, 2026-11-20: "the widget
    /// items should actually have a fixed size ... the last item will be cutoff to some extent then"; WR-4, WR-6): each
    /// core's cell its natural (<paramref name="cell"/>) in whole grid cells at every size of its card, touching from the
    /// card's left and top edges, line by line ([S-15758]); every whole column the width holds and every line the height
    /// reaches (the last cut by the foot). A core past the cells the card reaches is not drawn; a columns look
    /// (<paramref name="column"/>) is one line the card's whole height.
    /// </summary>
    public static CellReach CoreCells(double width, double height, SizeDip cell, ItemGrid grid, bool column = false)
    {
        var reach = ItemSize.Cells(width, height, cell.Width, cell.Height, grid);
        var tall = double.IsFinite(height) && height > 0 ? height : cell.Height;
        if (column)
        {
            return reach with { Down = new RowReach(tall, 1, grid.Raster) { Room = tall } };
        }

        // [S-15758] Whole columns only, filled line by line (WR-4: "the last item will be cutoff"): no line's last core is cut
        // by the right edge while lines go on under it; only the last line the height reaches is cut by the foot. What the
        // width leaves past the last whole column is less than a cell.
        var wide = double.IsFinite(width) && width > 0 ? width : cell.Width;
        var pitch = reach.Across.Pitch;
        var columns = Math.Max(1, (int)Math.Floor((wide + LayoutMath.Tolerance) / pitch));
        return reach with { Across = new RowReach(pitch, columns, grid.Raster) { Room = wide } };
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

}
