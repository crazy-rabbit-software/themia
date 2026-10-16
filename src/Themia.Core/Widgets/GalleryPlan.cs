using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What the gallery draws in its main area.</summary>
public enum GalleryMode
{
    /// <summary>One picture, full-bleed, still: a file, the photo of the day, or a layout with room for one.</summary>
    Single = 0,

    /// <summary>One picture at a time on the slideshow's timer.</summary>
    Slideshow = 1,

    Grid = 2,

    Scatter = 3,

    Mosaic = 4,
}

/// <summary>
/// [S-767] The gallery's disclosure ladder for one size: everything the view needs to lay out, and
/// nothing it has to work out for itself.
/// </summary>
/// <param name="Mode">What the main area draws.</param>
/// <param name="Pictures">How many pictures the main area holds (1 for Single and Slideshow).</param>
/// <param name="Counter">Show "3/12" in the header.</param>
/// <param name="Controls">Offer the previous / pause / next bar on hover.</param>
/// <param name="MainWidth">The main picture area's width.</param>
/// <param name="MainHeight">The main picture area's height.</param>
public sealed record GalleryPlan(
    GalleryMode Mode,
    int Pictures,
    bool Counter,
    bool Controls,
    double MainWidth,
    double MainHeight)
{
    /// <summary>
    /// Work out the ladder.
    /// </summary>
    /// <param name="area">The view's slot.</param>
    /// <param name="layout">The configured layout (ignored for single-picture sources).</param>
    /// <param name="singleSource">File or photo of the day: always one still picture.</param>
    /// <param name="available">How many pictures the scan produced.</param>
    /// <param name="gap">The gap between parts.</param>
    public static GalleryPlan For(WidgetArea area, GalleryStyle layout, bool singleSource, int available, double gap)
    {
        var width = Math.Max(0, area.Width);
        var height = Math.Max(0, area.Height);

        if (singleSource || available <= 1)
        {
            return Single(area);
        }

        switch (layout)
        {
            case GalleryStyle.Grid:
                return Many(GalleryMode.Grid, GalleryArrangement.GridCapacity(width, height, gap), area, available);

            case GalleryStyle.Scatter:
                return Many(GalleryMode.Scatter, GalleryArrangement.ScatterCapacity(width, height), area, available);

            case GalleryStyle.Mosaic:
                return Many(GalleryMode.Mosaic, GalleryArrangement.MosaicCapacity(width, height), area, available);

            default:
                return Slideshow(area);
        }
    }

    private static GalleryPlan Many(GalleryMode mode, int capacity, WidgetArea area, int available)
    {
        var pictures = Math.Min(Math.Min(capacity, available), GalleryArrangement.MaxTiles);

        // [S-767] Rung 0 at XS: a layout with room for only one picture is one picture, full-bleed,
        // rather than a grid of one or a lone tilted print in a sea of card.
        return pictures <= 1
            ? Single(area)
            : new GalleryPlan(mode, pictures, false, false, area.Width, area.Height);
    }

    /// <summary>
    /// [S-2760] One picture, the whole slot: no title, credit or file name under it at any size.
    /// </summary>
    private static GalleryPlan Single(WidgetArea area)
        => new(GalleryMode.Single, 1, false, false, area.Width, area.Height);

    /// <summary>
    /// [S-767] The slide is the whole slot at every size: no "up next" thumbnails beside or under
    /// it, and [S-2760] no file name.
    /// </summary>
    private static GalleryPlan Slideshow(WidgetArea area)
    {
        // The counter and the hover bar need a card wide enough to carry three buttons and
        // "12/24" without covering the picture; at XS the picture is the whole widget.
        var regular = area.WidthClass >= SizeClass.Regular;
        return new GalleryPlan(GalleryMode.Slideshow, 1, regular, regular, area.Width, area.Height);
    }
}
