using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11120] [S-11121] The moon's two looks and their sizes. Disc: a Readout of how much of the moon is
/// lit on the family's 28 ("23%"), the moon drawn at its phase as its lead at the step's size, the
/// phase's name the Caption under it: one height, 140..320 wide. Phase month: a disc a day of the month
/// holding now on a fixed grid of seven columns and five rows (31 days at most), a 20 disc on a 28 pitch,
/// the month's name the Caption 8 over it: one size. The rise, set and the next full and new moon are
/// the moon times' atom.
/// </summary>
public static class MoonLayout
{
    /// <summary>The widest share the moon prints, led by its disc, its phase's name under it.</summary>
    public static ReadoutText Worst(string full = "100%") => ReadoutRules.Split(full) with { Lead = true, LineUnder = true };

    /// <summary>[S-11121] A day's cell on the month's grid, and its disc in it (the gap round it holds today's ring).</summary>
    public const double Pitch = FamilyShape.Row1;

    public const double Disc = Pitch - FamilySpace.S2;

    public const int Columns = 7;

    public const int Rows = 5;

    public const double Narrowest = 140;

    public const double Widest = 320;

    /// <summary>[S-11121] The discs of a full month at a new card's size on their pitch: 188 × 132, what its natural size is made for.</summary>
    public static SizeDip Grid => new(((Columns - 1) * Pitch) + Disc, ((Rows - 1) * Pitch) + Disc);

    /// <summary>[S-11121] The month's grid, its name over it: its width and height.</summary>
    public static SizeDip Month => new(Grid.Width, FamilyType.Caption.LineHeight + FamilySpace.S2 + Grid.Height);

    /// <summary>
    /// [S-15805] The month's day cells as the card's grid (WR-9, owner 2026-11-19: "the calendar does not follow this design
    /// philosophy"; WR-2, WR-3): seven columns across the whole <paramref name="width"/> and a line a week of the month's
    /// <paramref name="days"/> down the whole <paramref name="height"/>, every cell the same, touching; each holds its day's
    /// disc in the middle, the share of its cell a disc had of its 28 pitch (20), the gap round it today's ring's. Before, the
    /// discs stood on a fixed 28 pitch, centred: 275 DIP of a 900 card empty either side.
    /// </summary>
    public static MoonCells Cells(double width, double height, int days)
    {
        var rows = Math.Max(1, (Math.Max(1, days) + Columns - 1) / Columns);
        var cellWidth = double.IsFinite(width) && width > 0 ? width / Columns : 0;
        var cellHeight = double.IsFinite(height) && height > 0 ? height / rows : 0;
        var room = Math.Min(cellWidth, cellHeight);
        return new MoonCells(rows, cellWidth, cellHeight, room * Disc / Pitch, room);
    }

    /// <summary>
    /// [S-12833] The disc's parts (<see cref="FaceParts"/>): the share its main reading, at 9 in its least card, the moon
    /// beside it; its unit and the phase's name give way on the way down. The phase month is no face scaled as one but the
    /// card's grid ([S-15805]): none.
    /// </summary>
    public static FaceParts? Face(MoonConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == MoonStyle.PhaseMonth)
        {
            return null;
        }

        var disc = FamilyType.Big28.FontSize + FamilySpace.GlyphGap;
        return FaceParts.Readout(FamilyType.Big28, Worst() with { Lead = false }, 160 - (2 * FamilySpace.Inset) - disc)
            .Led(FacePiece.Box(disc, FamilyType.Big28.LineHeight));
    }

    /// <summary>[S-11121] The moon's sizing.</summary>
    public static WidgetSizing Sizing(MoonConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == MoonStyle.PhaseMonth)
        {
            // [S-15805] Down to where a cell's disc is the glyph floor (12, WR-14), the name over the cells kept.
            var natural = new SizeDip((2 * FamilySpace.Inset) + Month.Width, FamilySpace.ContentTop + Month.Height + FamilySpace.Inset);
            var room = Math.Ceiling(ScaleFloor.Glyph * Pitch / Disc);
            var least = new SizeDip(Columns * room, FamilySpace.ContentTop + FamilyType.Caption.LineHeight + (Rows * room));
            return WidgetSizing.Range(least, natural, natural);
        }

        var height = FamilySpace.ContentTop + ReadoutRules.Height(FamilyType.Big28, Worst()) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(160, height), new(Widest, height));
    }
}

/// <summary>[S-15805] The moon month's cells: its lines, a cell's width and height, the disc in it and today's ring round it.</summary>
public readonly record struct MoonCells(int Rows, double CellWidth, double CellHeight, double Disc, double Ring);
