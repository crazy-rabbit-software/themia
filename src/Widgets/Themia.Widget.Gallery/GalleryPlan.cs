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
/// <param name="Counter">Show "3/12" in the header (the slideshow's).</param>
/// <param name="Controls">Offer the previous / pause / next bar on hover (the slideshow's).</param>
/// <param name="MainWidth">The main picture area's width.</param>
/// <param name="MainHeight">The main picture area's height.</param>
/// <param name="Columns">The grid's columns (the config's), else 1.</param>
/// <param name="Rows">The grid's rows (the config's), else 1.</param>
public sealed record GalleryPlan(
    GalleryMode Mode,
    int Pictures,
    bool Counter,
    bool Controls,
    double MainWidth,
    double MainHeight,
    int Columns = 1,
    int Rows = 1)
{
    /// <summary>
    /// [S-11207] The plan for <paramref name="config"/> with <paramref name="available"/> pictures in
    /// <paramref name="area"/> (the view's slot): a picture file or the photo of the day is one still
    /// picture; the slideshow one at a time with its counter and its hover bar; the grid its Columns ×
    /// Rows cells; the scatter and the mosaic as many pictures as Pictures shown says. Fewer pictures than
    /// that show what there is; one alone is that picture, full-bleed.
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
            GalleryStyle.Grid => Many(GalleryMode.Grid, config.EffectiveColumns * config.EffectiveRows, area, available) with
            {
                Columns = config.EffectiveColumns,
                Rows = config.EffectiveRows,
            },
            GalleryStyle.Scatter => Many(GalleryMode.Scatter, config.EffectiveShown, area, available),
            GalleryStyle.Mosaic => Many(GalleryMode.Mosaic, config.EffectiveShown, area, available),
            _ => new GalleryPlan(GalleryMode.Slideshow, 1, true, true, area.Width, area.Height),
        };
    }

    private static GalleryPlan Many(GalleryMode mode, int wanted, WidgetArea area, int available)
    {
        var pictures = Math.Min(Math.Min(wanted, available), GalleryArrangement.MaxTiles);
        return pictures <= 1 ? Single(area) : new GalleryPlan(mode, pictures, false, false, area.Width, area.Height);
    }

    /// <summary>[S-2760] One picture, the whole slot: no title, credit or file name under it at any size.</summary>
    private static GalleryPlan Single(WidgetArea area)
        => new(GalleryMode.Single, 1, false, false, area.Width, area.Height);

    /// <summary>[S-11207] A grid cell's side for its Cell size: Small 64, Medium 96, Large 128.</summary>
    public static double CellSide(GalleryCellSize size) => size switch
    {
        GalleryCellSize.Small => 2 * FamilyShape.IconLarge,
        GalleryCellSize.Large => 4 * FamilyShape.IconLarge,
        _ => 3 * FamilyShape.IconLarge,
    };

    /// <summary>[S-11207] The gap between grid cells.</summary>
    public const double Gap = FamilySpace.S1;

    /// <summary>[S-11207] A free gallery's range: the slideshow, the scatter, the mosaic, one picture.</summary>
    public static readonly SizeDip Least = new(160, 120), Natural = new(320, 240), Largest = new(800, 600);

    /// <summary>[S-11207] The grid's steps on one axis: a cell and its gap each, the config's count of them.</summary>
    public static SizeSteps GridSteps(GalleryCellSize size, int count)
        => new(-Gap, CellSide(size) + Gap, GalleryConfig.MinCells, GalleryConfig.MaxCells, count);

    /// <summary>
    /// [S-11207] The gallery's sizes (headerless, its default): the grid is its Columns × Rows cells at
    /// its Cell size, 4 apart, full-bleed, both axes stepped by a cell and following the config;
    /// everything else a free range, 160..800 × 120..600, a new one 320 × 240.
    /// </summary>
    public static WidgetSizing Sizing(GalleryConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style == GalleryStyle.Grid && !config.IsSingleSource)
        {
            return WidgetSizing.Range(new(1, 1), new(1, 1), new(1, 1))
                .WithWidthSteps(GridSteps(config.EffectiveCellSize, config.EffectiveColumns))
                .WithHeightSteps(GridSteps(config.EffectiveCellSize, config.EffectiveRows));
        }

        return WidgetSizing.Range(Least, Natural, Largest);
    }

    /// <summary>[S-11207] The config a grid of <paramref name="size"/> means: the columns and rows it holds. A copy when they change.</summary>
    public static WidgetConfig SizeSets(GalleryConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.Style != GalleryStyle.Grid || config.IsSingleSource)
        {
            return config;
        }

        var columns = GridSteps(config.EffectiveCellSize, config.EffectiveColumns).Nearest(size.Width);
        var rows = GridSteps(config.EffectiveCellSize, config.EffectiveRows).Nearest(size.Height);
        if (columns == config.EffectiveColumns && rows == config.EffectiveRows)
        {
            return config;
        }

        var copy = (GalleryConfig)config.Clone();
        copy.Columns = columns;
        copy.Rows = rows;
        return copy;
    }
}
