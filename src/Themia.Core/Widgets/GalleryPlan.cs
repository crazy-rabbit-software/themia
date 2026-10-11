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

/// <summary>[S-768] Where the slideshow's "up next" thumbnails go, if anywhere.</summary>
public enum GalleryStrip
{
    None = 0,

    /// <summary>A row under the picture, on a card with height to spare.</summary>
    Below = 1,

    /// <summary>Squares to the right of the picture, on a wide strip — beside costs no height.</summary>
    Beside = 2,
}

/// <summary>
/// [S-767] The gallery's disclosure ladder for one size: everything the view needs to lay out, and
/// nothing it has to work out for itself.
/// </summary>
/// <param name="Mode">What the main area draws.</param>
/// <param name="Pictures">How many pictures the main area holds (1 for Single and Slideshow).</param>
/// <param name="Strip">Where the thumbnail strip goes.</param>
/// <param name="StripCount">How many thumbnails it holds; 0 without a strip.</param>
/// <param name="StripExtent">The strip's thickness: its height when below, its width when beside.</param>
/// <param name="CaptionLines">Text lines under the picture: 0, 1 (title) or 2 (title and detail).</param>
/// <param name="Counter">Show "3/12" in the header.</param>
/// <param name="Controls">Offer the previous / pause / next bar on hover.</param>
/// <param name="MainWidth">The main picture area's width.</param>
/// <param name="MainHeight">The main picture area's height.</param>
public sealed record GalleryPlan(
    GalleryMode Mode,
    int Pictures,
    GalleryStrip Strip,
    int StripCount,
    double StripExtent,
    int CaptionLines,
    bool Counter,
    bool Controls,
    double MainWidth,
    double MainHeight)
{
    /// <summary>The shortest the main picture is allowed to get before a strip or caption is refused.</summary>
    public const double MinPicture = 56;

    /// <summary>A slideshow's main picture keeps at least this much height under a strip and caption.</summary>
    public const double MinSlide = 120;

    /// <summary>A thumbnail strip below the picture aims at thumbnails about this wide.</summary>
    public const double StripTarget = 110;

    public const double MinThumb = 48;

    public const double MaxThumb = 96;

    /// <summary>The slideshow's picture keeps at least this aspect beside a strip: a photograph, not a slot.</summary>
    public const double BesideAspect = 1.5;

    /// <summary>A caption's first line also pays for the padding under it, so text never sits on the card's edge.</summary>
    public static double TitleCost(double bottomPadding) => FluentType.BodyStrong.LineHeight + bottomPadding;

    public static double DetailCost => FluentType.Caption.LineHeight;

    /// <summary>
    /// Work out the ladder.
    /// </summary>
    /// <param name="area">The view's slot.</param>
    /// <param name="layout">The configured layout (ignored for single-picture sources).</param>
    /// <param name="singleSource">File or photo of the day: always one still picture.</param>
    /// <param name="available">How many pictures the scan produced.</param>
    /// <param name="caption">The source has words to show under its picture (the photo of the day).</param>
    /// <param name="gap">The gap between parts, which is also the caption's bottom padding.</param>
    public static GalleryPlan For(WidgetArea area, GalleryLayout layout, bool singleSource, int available, bool caption, double gap)
    {
        var width = Math.Max(0, area.Width);
        var height = Math.Max(0, area.Height);

        if (singleSource || available <= 1)
        {
            return Single(area, caption && singleSource, gap);
        }

        switch (layout)
        {
            case GalleryLayout.Grid:
                return Many(GalleryMode.Grid, GalleryArrangement.GridCapacity(width, height, gap), area, available, gap);

            case GalleryLayout.Scatter:
                return Many(GalleryMode.Scatter, GalleryArrangement.ScatterCapacity(width, height), area, available, gap);

            case GalleryLayout.Mosaic:
                return Many(GalleryMode.Mosaic, GalleryArrangement.MosaicCapacity(width, height), area, available, gap);

            default:
                return Slideshow(area, available, gap);
        }
    }

    private static GalleryPlan Many(GalleryMode mode, int capacity, WidgetArea area, int available, double gap)
    {
        var pictures = Math.Min(Math.Min(capacity, available), GalleryArrangement.MaxTiles);

        // [S-767] Rung 0 at XS: a layout with room for only one picture is one picture, full-bleed,
        // rather than a grid of one or a lone tilted print in a sea of card.
        return pictures <= 1
            ? Single(area, caption: false, gap)
            : new GalleryPlan(mode, pictures, GalleryStrip.None, 0, 0, 0, false, false, area.Width, area.Height);
    }

    private static GalleryPlan Single(WidgetArea area, bool caption, double gap)
    {
        var lines = 0;

        if (caption && area.WidthClass >= SizeClass.Regular)
        {
            // The picture keeps at least a quarter of its width in height: on a wide strip the
            // words would otherwise squeeze the photograph into a letterbox slot.
            var room = new Disclosure(area, gap);
            room.Reserve(Math.Max(MinPicture, area.Width / 4));

            if (room.Take(TitleCost(gap)))
            {
                lines = area.HeightClass >= SizeClass.Regular && room.Take(DetailCost) ? 2 : 1;
            }
        }

        var used = CaptionHeight(lines, gap);
        return new GalleryPlan(GalleryMode.Single, 1, GalleryStrip.None, 0, 0, lines, false, false,
            area.Width, Math.Max(0, area.Height - used));
    }

    private static GalleryPlan Slideshow(WidgetArea area, int available, double gap)
    {
        // [S-767] The counter and the hover bar need a card wide enough to carry three buttons and
        // "12/24" without covering the picture; at XS the picture is the whole widget.
        var regular = area.WidthClass >= SizeClass.Regular;
        var width = area.Width;
        var height = area.Height;
        var upcoming = Math.Max(0, available - 1);

        // A wide strip: the extras go beside, where they cost no height. Square thumbnails the
        // strip's full height, as many as leave the picture a photograph's proportions.
        if (area.Shape == AreaShape.Wide && area.WidthClass >= SizeClass.Expanded)
        {
            var fit = (int)Math.Floor((width - (height * BesideAspect)) / (height + gap));
            var count = Math.Clamp(fit, 0, Math.Min(upcoming, 6));

            if (count > 0)
            {
                return new GalleryPlan(GalleryMode.Slideshow, 1, GalleryStrip.Beside, count, height, 0, regular, regular,
                    width - (count * (height + gap)), height);
            }
        }

        var room = new Disclosure(area, gap);
        room.Reserve(MinSlide);

        var strip = GalleryStrip.None;
        var stripCount = 0;
        var thumb = 0.0;

        if (area.HeightClass >= SizeClass.Expanded && upcoming > 0)
        {
            var wanted = Math.Clamp((int)Math.Round(width / StripTarget), 3, 8);
            var count = Math.Min(wanted, upcoming);
            var thumbWidth = (width - ((wanted - 1) * gap)) / wanted;
            var extent = Math.Clamp(thumbWidth * 0.75, MinThumb, MaxThumb);

            if (room.Take(extent))
            {
                strip = GalleryStrip.Below;
                stripCount = count;
                thumb = extent;
            }
        }

        // The file name from L, once the pictures have all the room they can use. One line only:
        // where it sits in the set is the header's counter already, and XL gives the pictures the
        // room rather than more words.
        var lines = area.AtLeast(SizeClass.Expanded, SizeClass.Expanded) && room.Take(TitleCost(gap)) ? 1 : 0;

        var used = (strip == GalleryStrip.Below ? thumb + gap : 0) + CaptionHeight(lines, gap);
        return new GalleryPlan(GalleryMode.Slideshow, 1, strip, stripCount, thumb, lines, regular, regular,
            width, Math.Max(0, height - used));
    }

    /// <summary>The caption block's height for a number of lines, including its gap above and padding below.</summary>
    public static double CaptionHeight(int lines, double gap) => lines switch
    {
        <= 0 => 0,
        1 => gap + TitleCost(gap),
        _ => gap + TitleCost(gap) + gap + DetailCost,
    };
}
