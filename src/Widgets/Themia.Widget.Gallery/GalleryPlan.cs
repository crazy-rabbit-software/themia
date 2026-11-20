using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>What the gallery draws in its main area.</summary>
public enum GalleryMode
{
    /// <summary>One picture, full-bleed, still: a file, the photo of the day, or a layout with one picture to show.</summary>
    Single = 0,

    /// <summary>One picture at a time on the slideshow's timer.</summary>
    Slideshow = 1,

    Grid = 2,

    Scatter = 3,

    Mosaic = 4,
}

/// <summary>
/// [S-767] [S-11207] What the gallery draws, from its config and the pictures it has: never from its
/// size. The size only scales what is drawn.
/// </summary>
/// <param name="Mode">What the main area draws.</param>
/// <param name="Pictures">How many pictures the main area holds (1 for Single and Slideshow).</param>
/// <param name="Controls">Offer the previous / pause / next bar on hover (the slideshow's).</param>
/// <param name="MainWidth">The main picture area's width.</param>
/// <param name="MainHeight">The main picture area's height.</param>
/// <param name="Columns">The grid's columns (what the card's width holds), else 1.</param>
/// <param name="Rows">The grid's rows (what the card's height holds), else 1.</param>
public sealed record GalleryPlan(
    GalleryMode Mode,
    int Pictures,
    bool Controls,
    double MainWidth,
    double MainHeight,
    int Columns = 1,
    int Rows = 1)
{
    /// <summary>
    /// [S-11207] The plan for <paramref name="config"/> with <paramref name="available"/> pictures in
    /// <paramref name="area"/> (the view's slot): a picture file or the photo of the day is one still
    /// picture; the slideshow one at a time with its counter and its hover bar; the grid as many cells across and down as
    /// its box holds ([S-14500], <see cref="Cells"/>); the scatter and the mosaic as many pictures as their box holds
    /// (<see cref="ShownIn"/>). Fewer pictures than that show what there is; one alone is that picture, full-bleed.
    /// </summary>
    public static GalleryPlan For(WidgetArea area, GalleryConfig config, int available)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.IsSingleSource || available <= 1)
        {
            return Single(area);
        }

        return config.Style switch
        {
            GalleryStyle.Grid => Many(GalleryMode.Grid, Cells(area.Width, GalleryConfig.DefaultColumns) * Cells(area.Height, config.EffectiveRows), area, available) with
            {
                Columns = Cells(area.Width, GalleryConfig.DefaultColumns),
                Rows = Cells(area.Height, config.EffectiveRows),
            },
            GalleryStyle.Scatter => Many(GalleryMode.Scatter, ShownIn(area), area, available),
            GalleryStyle.Mosaic => Many(GalleryMode.Mosaic, ShownIn(area), area, available),
            _ => new GalleryPlan(GalleryMode.Slideshow, 1, true, area.Width, area.Height),
        };
    }

    private static GalleryPlan Many(GalleryMode mode, int wanted, WidgetArea area, int available)
    {
        var pictures = Math.Min(Math.Min(wanted, available), GalleryArrangement.MaxTiles);
        return pictures <= 1 ? Single(area) : new GalleryPlan(mode, pictures, false, area.Width, area.Height);
    }

    /// <summary>[S-2760] One picture, the whole slot: no title, credit or file name under it at any size.</summary>
    private static GalleryPlan Single(WidgetArea area)
        => new(GalleryMode.Single, 1, false, area.Width, area.Height);

    /// <summary>[S-11207] A grid cell's side: 96 (no Cell size setting, [S-2449] retired).</summary>
    public const double CellSide = 3 * FamilyShape.IconLarge;

    /// <summary>
    /// [S-11207] [S-14410] How many pictures a scatter or a mosaic shows in <paramref name="area"/>: as many grid cells
    /// (96 and the gap) as its width and height hold, 1..<see cref="GalleryConfig.MaxShown"/>. The card's, never a setting
    /// (owner, 2026-11-20: "Anything that's just 'how many of the same item' ... follows the card's size"): a new one
    /// (320 × 240) six.
    /// </summary>
    public static int ShownIn(WidgetArea area)
    {
        static int Along(double length) => double.IsFinite(length) ? (int)Math.Floor((length + Gap + LayoutMath.Tolerance) / (CellSide + Gap)) : 0;
        return Math.Clamp(Math.Max(1, Along(area.Width)) * Math.Max(1, Along(area.Height)), 1, GalleryConfig.MaxShown);
    }

    /// <summary>[S-11207] The gap between grid cells.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>[S-11207] A free gallery's range: the slideshow, the scatter, the mosaic, one picture.</summary>
    public static readonly SizeDip Least = new(160, 120), Natural = new(320, 240), Largest = new(800, 600);

    /// <summary>[S-11207] The grid's length on one axis: <paramref name="count"/> cells of 96, 4 apart.</summary>
    public static double GridLength(int count) => (count * (CellSide + Gap)) - Gap;

    /// <summary>
    /// [S-11207] [S-14410] [S-14500] How many grid cells a length holds at their side and gap
    /// (<see cref="GalleryConfig.MinCells"/>..<see cref="GalleryConfig.MaxCells"/>; the cells stretch to fill it): the
    /// card's, never a setting; before the box is measured, <paramref name="unmeasured"/>.
    /// </summary>
    public static int Cells(double length, int unmeasured)
        => double.IsFinite(length) && length > 0
            ? Math.Clamp((int)Math.Floor((length + Gap + LayoutMath.Tolerance) / (CellSide + Gap)), GalleryConfig.MinCells, GalleryConfig.MaxCells)
            : unmeasured;

    /// <summary>
    /// [S-11207] [S-14500] The gallery's sizes (headerless, its default): the grid's cells of 96, 4 apart, full-bleed, a new
    /// one <see cref="GalleryConfig.DefaultColumns"/> across and its Rows down, every cell between (as many cells as it
    /// holds, stretched to fill it); everything else a free range, 160..800 × 120..600, a new one 320 × 240.
    /// </summary>
    public static WidgetSizing Sizing(GalleryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == GalleryStyle.Grid && !config.IsSingleSource)
        {
            var (least, most) = (GridLength(GalleryConfig.MinCells), GridLength(GalleryConfig.MaxCells));
            return WidgetSizing.Range(new(least, least), new(GridLength(GalleryConfig.DefaultColumns), GridLength(config.EffectiveRows)), new(most, most)) with { Room = StretchRoom.Picture };
        }

        return WidgetSizing.Range(Least, Natural, Largest) with { Room = StretchRoom.Picture };
    }
}
