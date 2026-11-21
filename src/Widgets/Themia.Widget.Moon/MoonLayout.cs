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

    /// <summary>[S-6050] [S-15673] How far today's ring reaches past its disc: the gap round the disc in its pitch.</summary>
    public const double Ring = (Pitch - Disc) / 2;

    /// <summary>[S-15673] The grid of a full month at a new card's size, no ring on its edge: its discs' ink, 188 × 132.</summary>
    public static SizeDip Grid => new(((Columns - 1) * Pitch) + Disc, ((Rows - 1) * Pitch) + Disc);

    /// <summary>[S-11121] The month's grid, its name over it: its width and height.</summary>
    public static SizeDip Month => new(Grid.Width, FamilyType.Caption.LineHeight + FamilySpace.S2 + Grid.Height);

    /// <summary>
    /// [S-15673] The grid of a month of <paramref name="days"/> with today the <paramref name="today"/>th (from 0; none
    /// outside them) as drawn: its box is its ink, the discs from its edges and today's ring where it stands on one (the ring
    /// reaches <see cref="Ring"/> past its disc), never a pitch's gap round the outer discs. A block scaled to fill its card
    /// (<c>ReadingFit</c>) stands by its box: the gap the outer discs left inside it stood the face 4 DIP off centre at twice
    /// its size. Its lines are the days' (a February of 28 four).
    /// </summary>
    public static MoonGridBox GridOf(int days, int today)
    {
        days = Math.Max(1, days);
        var columns = Math.Min(Columns, days);
        var rows = (days + Columns - 1) / Columns;
        var ringed = today >= 0 && today < days;
        var (column, row) = ringed ? (today % Columns, today / Columns) : (-1, -1);
        var left = column == 0 ? Ring : 0;
        var right = column == columns - 1 ? Ring : 0;
        var top = row == 0 ? Ring : 0;
        var foot = row == rows - 1 ? Ring : 0;
        return new MoonGridBox(left + ((columns - 1) * Pitch) + Disc + right, top + ((rows - 1) * Pitch) + Disc + foot, left, top);
    }

    /// <summary>
    /// [S-12833] The moon's parts (<see cref="FaceParts"/>). Disc: the share its main reading, at 9 in its least card, the
    /// moon beside it; its unit and the phase's name give way on the way down. Phase month: its discs are its reading, at
    /// their glyph floor (a 20 disc as 15 text, <see cref="ScaleFloor.AsText"/>); the month's name over them gives way.
    /// </summary>
    public static FaceParts Face(MoonConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == MoonStyle.PhaseMonth)
        {
            return new FaceParts(ScaleFloor.AsText(Disc),
            [
                new FaceRow([FacePiece.Text(FamilyType.Caption, Month.Width, FamilyType.Caption.FontSize)]),
                new FaceRow([FacePiece.Box(Grid.Width, Grid.Height)], GapOver: FamilySpace.S2),
            ]);
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
            return WidgetSizing.Fixed(new((2 * FamilySpace.Inset) + Month.Width, FamilySpace.ContentTop + Month.Height + FamilySpace.Inset));
        }

        var height = FamilySpace.ContentTop + ReadoutRules.Height(FamilyType.Big28, Worst()) + FamilySpace.Inset;
        return WidgetSizing.Range(new(Narrowest, height), new(160, height), new(Widest, height));
    }
}

/// <summary>[S-15673] The moon month's grid as drawn: its box (its ink) and where its first disc stands in it.</summary>
public readonly record struct MoonGridBox(double Width, double Height, double Left, double Top);
