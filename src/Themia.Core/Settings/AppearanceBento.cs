using Themia.Sdk;

namespace Themia.Core.Settings;

/// <summary>
/// [S-2311] One tile of the Appearance preview's bento: a widget kind standing on <see cref="Columns"/> ×
/// <see cref="Rows"/> of the bento's cells from (<see cref="Column"/>, <see cref="Row"/>). Tiles of one
/// <see cref="Join"/> (not 0) that sit side by side meet with no gap, so they draw as one joined panel ([S-2240]).
/// </summary>
public sealed record BentoTile(string Kind, int Column, int Row, int Columns, int Rows, int Join = 0)
{
    /// <summary>The kind as an id.</summary>
    public WidgetId Id => new(Kind);
}

/// <summary>[S-2311] A tile's box in the display's grid cells ([S-06]): whole cells, from the bento's top-left corner.</summary>
public readonly record struct BentoBox(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>
/// [S-2311] The Appearance preview's bento (owner, 2026-11-19: "the appearance style preview should show a whole bento
/// grid of widgets not just three"): a small desktop of real cards of mixed sizes and kinds, so a style shows on a
/// readout, lists, a graph, a clock, a month, a toggle tile and a joined group at once. Pure: the tiles pack the
/// <see cref="Columns"/> × <see cref="Rows"/> rectangle with no hole and no overlap, and <see cref="Boxes"/> lays them on
/// the display's grid, a bento cell <see cref="UnitCells"/> grid cells square and the tiles <see cref="GapCells"/> apart;
/// the preview (<c>AppearancePreview</c>) draws a card on each box.
/// </summary>
public static class AppearanceBento
{
    /// <summary>The bento's columns.</summary>
    public const int Columns = 6;

    /// <summary>The bento's rows.</summary>
    public const int Rows = 4;

    /// <summary>A bento cell's side in grid cells: 140 DIPs on a 10-DIP grid, room for each tile's content at its kind's least size (a meter's 124 across on one cell, the clock's 256 on two).</summary>
    public const int UnitCells = 14;

    /// <summary>The space between two tiles in grid cells: even, so a join splits it in two whole halves.</summary>
    public const int GapCells = 2;

    /// <summary>The weather station's join: its three cards meet as one panel.</summary>
    private const int Station = 1;

    /// <summary>
    /// [S-2311] The tiles, in reading order: the clock and the month on top, a meter and a toggle under the clock, the
    /// tasks and a network graph and the music under them; down the right the weather station joined (the reading, its
    /// facts, the days ahead), and under it the battery and a power tile.
    /// </summary>
    public static IReadOnlyList<BentoTile> Tiles { get; } =
    [
        new("clock", 0, 0, 2, 1),
        new("calendar", 2, 0, 2, 2),
        new("weatherNow", 4, 0, 2, 1, Station),
        new("cpu", 0, 1, 1, 1),
        new("wifiToggle", 1, 1, 1, 1),
        new("weatherFacts", 4, 1, 2, 1, Station),
        new("tasks", 0, 2, 2, 2),
        new("networkHistory", 2, 2, 2, 1),
        new("weatherForecast", 4, 2, 2, 1, Station),
        new("music", 2, 3, 2, 1),
        new("battery", 4, 3, 1, 1),
        new("lock", 5, 3, 1, 1),
    ];

    /// <summary>The whole bento's width in grid cells.</summary>
    public static int Width => Span(Columns);

    /// <summary>The whole bento's height in grid cells.</summary>
    public static int Height => Span(Rows);

    /// <summary>How many grid cells <paramref name="count"/> bento cells and the gaps between them take.</summary>
    public static int Span(int count) => count <= 0 ? 0 : (count * UnitCells) + ((count - 1) * GapCells);

    /// <summary>
    /// [S-2311] Each tile's box in grid cells: its bento cells and the gaps between them, the gap round it left
    /// free; on a side where a tile of its join stands next to it, half the gap is its own, so the two meet.
    /// </summary>
    public static IReadOnlyList<BentoBox> Boxes(IReadOnlyList<BentoTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var half = GapCells / 2;
        return [.. tiles.Select(t =>
        {
            var x = t.Column * (UnitCells + GapCells);
            var y = t.Row * (UnitCells + GapCells);
            var w = Span(t.Columns);
            var h = Span(t.Rows);
            if (Joined(tiles, t, -1, 0))
            {
                x -= half;
                w += half;
            }

            if (Joined(tiles, t, 1, 0))
            {
                w += half;
            }

            if (Joined(tiles, t, 0, -1))
            {
                y -= half;
                h += half;
            }

            if (Joined(tiles, t, 0, 1))
            {
                h += half;
            }

            return new BentoBox(x, y, w, h);
        })];
    }

    /// <summary>Whether a tile of <paramref name="tile"/>'s join stands next to it on the side (dx, dy) and shares that side with it.</summary>
    private static bool Joined(IReadOnlyList<BentoTile> tiles, BentoTile tile, int dx, int dy)
        => tile.Join != 0 && tiles.Any(o => !ReferenceEquals(o, tile) && o.Join == tile.Join && (dx, dy) switch
        {
            (-1, 0) => o.Column + o.Columns == tile.Column && Overlap(o.Row, o.Rows, tile.Row, tile.Rows),
            (1, 0) => tile.Column + tile.Columns == o.Column && Overlap(o.Row, o.Rows, tile.Row, tile.Rows),
            (0, -1) => o.Row + o.Rows == tile.Row && Overlap(o.Column, o.Columns, tile.Column, tile.Columns),
            _ => tile.Row + tile.Rows == o.Row && Overlap(o.Column, o.Columns, tile.Column, tile.Columns),
        });

    private static bool Overlap(int a, int aCount, int b, int bCount) => a < b + bCount && b < a + aCount;
}
