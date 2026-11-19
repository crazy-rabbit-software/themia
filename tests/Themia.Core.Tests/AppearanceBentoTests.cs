using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2311] The Appearance preview's bento: its tiles pack the rectangle, and their boxes stand on the grid a gap apart, a join's meeting.</summary>
public sealed class AppearanceBentoTests
{
    [Fact]
    public void S2311_the_bento_tiles_pack_its_rectangle_with_no_hole_or_overlap()
    {
        var covered = new int[AppearanceBento.Columns, AppearanceBento.Rows];
        foreach (var tile in AppearanceBento.Tiles)
        {
            Assert.True(tile.Columns >= 1 && tile.Rows >= 1, $"{tile.Kind} spans no cell");
            Assert.True(tile.Column >= 0 && tile.Row >= 0, $"{tile.Kind} starts outside the bento");
            Assert.True(tile.Column + tile.Columns <= AppearanceBento.Columns && tile.Row + tile.Rows <= AppearanceBento.Rows, $"{tile.Kind} reaches outside the bento");
            for (var c = tile.Column; c < tile.Column + tile.Columns; c++)
            {
                for (var r = tile.Row; r < tile.Row + tile.Rows; r++)
                {
                    covered[c, r]++;
                }
            }
        }

        for (var c = 0; c < AppearanceBento.Columns; c++)
        {
            for (var r = 0; r < AppearanceBento.Rows; r++)
            {
                Assert.True(covered[c, r] == 1, $"cell ({c}, {r}) is covered {covered[c, r]} times");
            }
        }
    }

    [Fact]
    public void S2311_the_bento_is_eight_to_twelve_cards_of_mixed_sizes_with_a_joined_group()
    {
        var tiles = AppearanceBento.Tiles;
        Assert.InRange(tiles.Count, 8, 12);
        Assert.Equal(tiles.Count, tiles.Select(t => t.Kind).Distinct().Count());
        Assert.Contains(tiles, t => t is { Columns: 1, Rows: 1 });
        Assert.Contains(tiles, t => t is { Columns: 2, Rows: 1 });
        Assert.Contains(tiles, t => t is { Columns: 2, Rows: 2 });
        Assert.Contains(tiles.Where(t => t.Join != 0).GroupBy(t => t.Join), g => g.Count() >= 2);
    }

    [Fact]
    public void S2311_boxes_stand_a_gap_apart_on_the_grid_and_a_joins_tiles_meet()
    {
        var tiles = AppearanceBento.Tiles;
        var boxes = AppearanceBento.Boxes(tiles);
        Assert.Equal(tiles.Count, boxes.Count);

        // The bento's outer edges are the tiles' outer edges: the whole is a rectangle of Width × Height grid cells.
        Assert.Equal(0, boxes.Min(b => b.X));
        Assert.Equal(0, boxes.Min(b => b.Y));
        Assert.Equal(AppearanceBento.Width, boxes.Max(b => b.Right));
        Assert.Equal(AppearanceBento.Height, boxes.Max(b => b.Bottom));

        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var (a, b) = (boxes[i], boxes[j]);
                var gapX = Math.Max(b.X - a.Right, a.X - b.Right);
                var gapY = Math.Max(b.Y - a.Bottom, a.Y - b.Bottom);
                var apart = Math.Max(gapX, gapY);
                Assert.True(apart >= 0, $"{tiles[i].Kind} and {tiles[j].Kind} overlap");

                // Side by side in the bento (sharing a stretch of a side): a join's tiles meet, any others stand exactly the gap apart.
                if (Beside(tiles[i], tiles[j]))
                {
                    var expected = tiles[i].Join != 0 && tiles[i].Join == tiles[j].Join ? 0 : AppearanceBento.GapCells;
                    Assert.True(apart == expected, $"{tiles[i].Kind} and {tiles[j].Kind} stand {apart} apart, not {expected}");
                }
            }
        }
    }

    private static bool Beside(BentoTile a, BentoTile b)
    {
        static bool Overlap(int s, int n, int t, int m) => s < t + m && t < s + n;
        var acrossRows = Overlap(a.Row, a.Rows, b.Row, b.Rows) && (a.Column + a.Columns == b.Column || b.Column + b.Columns == a.Column);
        var acrossColumns = Overlap(a.Column, a.Columns, b.Column, b.Columns) && (a.Row + a.Rows == b.Row || b.Row + b.Rows == a.Row);
        return acrossRows || acrossColumns;
    }

    [Fact]
    public void S2311_two_joined_tiles_split_the_gap_and_a_third_beside_them_keeps_it()
    {
        BentoTile[] tiles = [new("a", 0, 0, 1, 1, 7), new("b", 1, 0, 1, 1, 7), new("c", 2, 0, 1, 1)];
        var boxes = AppearanceBento.Boxes(tiles);
        var half = AppearanceBento.GapCells / 2;
        Assert.Equal(new BentoBox(0, 0, AppearanceBento.UnitCells + half, AppearanceBento.UnitCells), boxes[0]);
        Assert.Equal(boxes[0].Right, boxes[1].X);
        Assert.Equal(AppearanceBento.UnitCells + half, boxes[1].Width);
        Assert.Equal(AppearanceBento.GapCells, boxes[2].X - boxes[1].Right);
        Assert.Equal(new BentoBox(AppearanceBento.Span(2) + AppearanceBento.GapCells, 0, AppearanceBento.UnitCells, AppearanceBento.UnitCells), boxes[2]);
    }
}
