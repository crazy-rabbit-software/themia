namespace Themia.Core.Widgets;

/// <summary>A rectangle on the card, in DIPs from its top-left corner.</summary>
public readonly record struct GalleryRect(double X, double Y, double Width, double Height);

/// <summary>
/// [S-764] One print in the scatter layout: a square photo centred at a point, turned a little.
/// </summary>
/// <param name="CenterX">Centre, in DIPs from the left edge.</param>
/// <param name="CenterY">Centre, in DIPs from the top edge.</param>
/// <param name="Side">The print's side, in DIPs. Prints are square, as in the original.</param>
/// <param name="Rotation">Clockwise tilt in degrees, within ±<see cref="GalleryArrangement.MaxTilt"/>.</param>
public readonly record struct ScatterPrint(double CenterX, double CenterY, double Side, double Rotation);

/// <summary>
/// Where the pictures go in the grid, scatter and mosaic layouts, and how many of them fit.
/// <para>
/// Pure and seeded. The original rolled <c>Math.random()</c> on every render, so the same folder
/// re-shuffled itself whenever anything redrew; here the view rolls one seed per scan (and per
/// "Refresh images") and every resize re-derives the same arrangement from it, so dragging the card
/// bigger moves the prints rather than dealing a new hand.
/// </para>
/// </summary>
public static class GalleryArrangement
{
    /// <summary>
    /// The most pictures any one layout draws. Past this a tile is a postage stamp, and it is also
    /// the decoded-bitmap cache's capacity, so a full card never evicts its own pictures.
    /// </summary>
    public const int MaxTiles = 24;

    /// <summary>Roughly how big a grid tile wants to be: a wider card shows more tiles, not bigger ones.</summary>
    public const double TargetTile = 120;

    /// <summary>[S-764] Card area per scatter print. Fewer prints on a small card, more on a big one.</summary>
    public const double ScatterAreaPerPrint = 12_000;

    /// <summary>[S-765] Card area per mosaic cell; about a 95-DIP square each.</summary>
    public const double MosaicAreaPerCell = 9_000;

    /// <summary>[S-764] How far a print's centre may wander inside its cell, as a share of the cell (the middle half).</summary>
    public const double ScatterJitter = 0.5;

    /// <summary>[S-764] A print's side as a multiple of its cell's short side: big enough to overlap its neighbours.</summary>
    public const double MinPrintScale = 1.05;

    public const double MaxPrintScale = 1.4;

    /// <summary>[S-764] The most a print is turned either way, in degrees.</summary>
    public const double MaxTilt = 12;

    /// <summary>[S-765] A mosaic split never gives either side less than this share.</summary>
    public const double MinSplit = 0.35;

    public const double MaxSplit = 0.65;

    /// <summary>
    /// How many grid tiles of about <see cref="TargetTile"/> the area holds, capped at
    /// <see cref="MaxTiles"/>. At least one.
    /// </summary>
    public static int GridCapacity(double width, double height, double gap)
    {
        if (!Usable(width) || !Usable(height))
        {
            return 1;
        }

        gap = Math.Max(0, gap);
        var columns = Math.Max(1, (int)Math.Round((width + gap) / (TargetTile + gap)));
        var rows = Math.Max(1, (int)Math.Round((height + gap) / (TargetTile + gap)));
        return Math.Clamp(columns * rows, 1, MaxTiles);
    }

    /// <summary>
    /// The columns and rows for <paramref name="count"/> tiles: the shape whose tiles are closest to
    /// square, with a small penalty per empty cell so seven pictures do not get a 3×3 with two holes
    /// when a 4×2 with one is nearly as square.
    /// </summary>
    public static (int Columns, int Rows) GridShape(double width, double height, int count, double gap)
    {
        if (count <= 1 || !Usable(width) || !Usable(height))
        {
            return (1, 1);
        }

        gap = Math.Max(0, gap);
        var best = (Columns: count, Rows: 1);
        var bestScore = double.MaxValue;

        for (var columns = 1; columns <= count; columns++)
        {
            var rows = (int)Math.Ceiling((double)count / columns);
            var tileWidth = (width - ((columns - 1) * gap)) / columns;
            var tileHeight = (height - ((rows - 1) * gap)) / rows;

            if (tileWidth <= 0 || tileHeight <= 0)
            {
                continue;
            }

            var score = Math.Abs(Math.Log(tileWidth / tileHeight)) + (0.15 * ((columns * rows) - count));

            if (score < bestScore)
            {
                bestScore = score;
                best = (columns, rows);
            }
        }

        return best;
    }

    /// <summary>[S-764] How many prints the scatter layout deals onto this area. At least one.</summary>
    public static int ScatterCapacity(double width, double height)
        => Capacity(width, height, ScatterAreaPerPrint);

    /// <summary>[S-765] How many cells the mosaic cuts this area into. At least one.</summary>
    public static int MosaicCapacity(double width, double height)
        => Capacity(width, height, MosaicAreaPerCell);

    /// <summary>
    /// [S-764] Deal <paramref name="count"/> prints onto a <paramref name="width"/> ×
    /// <paramref name="height"/> card.
    /// <para>
    /// The original threw every print within 28% of the centre, which on a big card is a heap in
    /// the middle and a frame of empty card around it. Here the card is cut into one cell per print
    /// (the grid's shape, with the spare cells left empty at random), each print lands somewhere in
    /// the middle half of its cell, is 1.05–1.4× the cell's short side so neighbours overlap, and is
    /// tilted up to ±12° as in the original. The stacking order is shuffled, so no corner is always
    /// on top.
    /// </para>
    /// </summary>
    public static IReadOnlyList<ScatterPrint> Scatter(double width, double height, int count, int seed)
    {
        if (count <= 0 || !Usable(width) || !Usable(height))
        {
            return [];
        }

        var random = new Random(seed);
        var (columns, rows) = GridShape(width, height, count, 0);
        var cellWidth = width / columns;
        var cellHeight = height / rows;
        var largest = Math.Min(width, height) * 0.9;

        // Which cells get a print: all of them but the spares, chosen at random.
        var cells = Enumerable.Range(0, columns * rows).ToArray();
        random.Shuffle(cells);

        var prints = new ScatterPrint[count];

        for (var i = 0; i < count; i++)
        {
            var cell = cells[i];
            var x = ((cell % columns) + 0.5 + ((random.NextDouble() - 0.5) * ScatterJitter)) * cellWidth;
            var y = ((cell / columns) + 0.5 + ((random.NextDouble() - 0.5) * ScatterJitter)) * cellHeight;
            var scale = MinPrintScale + (random.NextDouble() * (MaxPrintScale - MinPrintScale));
            var tilt = (random.NextDouble() - 0.5) * 2 * MaxTilt;

            prints[i] = new ScatterPrint(x, y, Math.Min(largest, Math.Min(cellWidth, cellHeight) * scale), tilt);
        }

        return prints;
    }

    /// <summary>
    /// [S-765] Cut the area into <paramref name="count"/> rectangles that tile it exactly: split the
    /// picture list in half, give the first half a random 35–65% share of the rectangle, recurse.
    /// The original alternated the cut's direction; this cuts across the <i>longer</i> side, which
    /// is the same on a square card and stops a wide strip being sliced into slivers.
    /// </summary>
    public static IReadOnlyList<GalleryRect> Mosaic(double width, double height, int count, int seed)
    {
        if (count <= 0 || !Usable(width) || !Usable(height))
        {
            return [];
        }

        var random = new Random(seed);
        var cells = new List<GalleryRect>(count);
        Subdivide(cells, random, count, new GalleryRect(0, 0, width, height));
        return cells;
    }

    private static void Subdivide(List<GalleryRect> cells, Random random, int count, GalleryRect area)
    {
        if (count == 1)
        {
            cells.Add(area);
            return;
        }

        var first = count / 2;
        var ratio = MinSplit + (random.NextDouble() * (MaxSplit - MinSplit));

        if (area.Width >= area.Height)
        {
            var left = area.Width * ratio;
            Subdivide(cells, random, first, area with { Width = left });
            Subdivide(cells, random, count - first, area with { X = area.X + left, Width = area.Width - left });
        }
        else
        {
            var top = area.Height * ratio;
            Subdivide(cells, random, first, area with { Height = top });
            Subdivide(cells, random, count - first, area with { Y = area.Y + top, Height = area.Height - top });
        }
    }

    private static int Capacity(double width, double height, double per)
    {
        if (!Usable(width) || !Usable(height))
        {
            return 1;
        }

        return Math.Clamp((int)(width * height / per), 1, MaxTiles);
    }

    private static bool Usable(double value) => double.IsFinite(value) && value > 0;
}
