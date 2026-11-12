using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Gallery.Tests;

/// <summary>
/// The gallery's layouts and its disclosure ladder: the scatter and mosaic generators (seeded, so
/// a resize re-derives the same arrangement), the grid's shape, the ladder at the seven size
/// tiers, and the config's clone and slide time. Moved with the widget from Core's
/// GalleryLayoutTests (the photo of the day's rules stay Core's GalleryRulesTests), unchanged in
/// what they assert.
/// </summary>
public class GalleryLayoutTests
{
    private const double Gap = 4;

    /// <summary>The seven size tiers, less a 36 header: the slots the view is given.</summary>
    public static TheoryData<string, double, double> Tiers => new()
    {
        { "XS", 120, 84 },
        { "S", 200, 114 },
        { "M", 320, 204 },
        { "Wide", 480, 124 },
        { "Tall", 200, 364 },
        { "L", 560, 384 },
        { "XL", 800, 524 },
    };

    // -----------------------------------------------------------------------------------------
    // [S-764] scatter
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(1234567)]
    public void S764_scatter_stays_on_the_card_within_its_size_and_tilt(int seed)
    {
        const double width = 560;
        const double height = 376;
        var prints = GalleryArrangement.Scatter(width, height, 17, seed);

        Assert.Equal(17, prints.Count);

        foreach (var print in prints)
        {
            Assert.InRange(print.CenterX, 0, width);
            Assert.InRange(print.CenterY, 0, height);
            Assert.InRange(print.Rotation, -GalleryArrangement.MaxTilt, GalleryArrangement.MaxTilt);
            Assert.True(print.Side > 0);
            Assert.True(print.Side <= Math.Min(width, height) * 0.9 + 0.001);
        }
    }

    [Fact]
    public void S764_scatter_reaches_every_quarter_of_a_big_card_not_a_heap_in_the_middle()
    {
        for (var seed = 0; seed < 20; seed++)
        {
            var prints = GalleryArrangement.Scatter(800, 524, 20, seed);

            Assert.Contains(prints, p => p.CenterX < 200 && p.CenterY < 129);
            Assert.Contains(prints, p => p.CenterX > 600 && p.CenterY < 129);
            Assert.Contains(prints, p => p.CenterX < 200 && p.CenterY > 387);
            Assert.Contains(prints, p => p.CenterX > 600 && p.CenterY > 387);
        }
    }

    [Fact]
    public void S764_scatter_is_reproducible_from_its_seed_and_varies_between_seeds()
    {
        var first = GalleryArrangement.Scatter(320, 204, 5, 42);
        var again = GalleryArrangement.Scatter(320, 204, 5, 42);
        var other = GalleryArrangement.Scatter(320, 204, 5, 43);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void S764_print_size_follows_the_room_each_print_has_not_the_card_width()
    {
        // A wide strip: the original's 45%-of-width print would be 216 DIPs on a 124-DIP strip.
        var prints = GalleryArrangement.Scatter(480, 124, 4, 9);
        Assert.All(prints, print => Assert.True(print.Side <= 124 * 0.9 + 0.001));
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(5, 0, 100)]
    [InlineData(5, double.NaN, 100)]
    public void S764_scatter_of_nothing_or_an_unmeasured_card_is_empty(int count, double width, double height)
        => Assert.Empty(GalleryArrangement.Scatter(width, height, count, 1));

    // -----------------------------------------------------------------------------------------
    // [S-765] mosaic
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(2, 3)]
    [InlineData(7, 11)]
    [InlineData(24, 99)]
    public void S765_mosaic_tiles_the_card_exactly(int count, int seed)
    {
        const double width = 560;
        const double height = 376;
        var cells = GalleryArrangement.Mosaic(width, height, count, seed);

        Assert.Equal(count, cells.Count);
        Assert.Equal(width * height, cells.Sum(c => c.Width * c.Height), 3);

        foreach (var cell in cells)
        {
            Assert.True(cell.Width > 0 && cell.Height > 0);
            Assert.True(cell.X >= -0.001 && cell.Y >= -0.001);
            Assert.True(cell.X + cell.Width <= width + 0.001);
            Assert.True(cell.Y + cell.Height <= height + 0.001);
        }

        // No two cells overlap.
        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                var a = cells[i];
                var b = cells[j];
                var overlapX = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
                var overlapY = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
                Assert.False(overlapX > 0.001 && overlapY > 0.001, $"cells {i} and {j} overlap");
            }
        }
    }

    [Fact]
    public void S765_two_cells_split_within_35_to_65_percent_across_the_longer_side()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var cells = GalleryArrangement.Mosaic(480, 124, 2, seed);

            // Wide: the cut is vertical, so both cells keep the full height.
            Assert.All(cells, cell => Assert.Equal(124, cell.Height, 3));
            Assert.InRange(cells[0].Width / 480, GalleryArrangement.MinSplit - 0.0001, GalleryArrangement.MaxSplit + 0.0001);
        }
    }

    [Fact]
    public void S765_mosaic_is_reproducible_from_its_seed()
        => Assert.Equal(GalleryArrangement.Mosaic(320, 204, 6, 5), GalleryArrangement.Mosaic(320, 204, 6, 5));

    // -----------------------------------------------------------------------------------------
    // [S-766] grid
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(320, 204, 6, 3, 2)]
    [InlineData(480, 124, 4, 4, 1)]
    [InlineData(200, 364, 6, 2, 3)]
    [InlineData(800, 524, 24, 6, 4)]
    public void S766_grid_shape_keeps_tiles_close_to_square(double width, double height, int count, int columns, int rows)
        => Assert.Equal((columns, rows), GalleryArrangement.GridShape(width, height, count, Gap));

    // -----------------------------------------------------------------------------------------
    // [S-11207] what it shows is the config's; its size follows the grid's cells
    // -----------------------------------------------------------------------------------------

    private static GalleryConfig Of(GalleryStyle style) => new() { Style = style };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S11207_what_each_layout_shows_is_the_same_at_every_size(string tier, double width, double height)
    {
        var area = new WidgetArea(width, height);

        Assert.Equal((6, 3, 2), (GalleryPlan.For(area, Of(GalleryStyle.Grid), 24).Pictures, GalleryPlan.For(area, Of(GalleryStyle.Grid), 24).Columns, GalleryPlan.For(area, Of(GalleryStyle.Grid), 24).Rows));
        Assert.Equal(6, GalleryPlan.For(area, Of(GalleryStyle.Scatter), 24).Pictures);
        Assert.Equal(6, GalleryPlan.For(area, Of(GalleryStyle.Mosaic), 24).Pictures);

        var slide = GalleryPlan.For(area, Of(GalleryStyle.Slideshow), 24);
        Assert.True(slide.Counter && slide.Controls, tier);

        foreach (var layout in Enum.GetValues<GalleryStyle>())
        {
            // [S-2760] No words under any picture: the picture keeps the whole slot.
            var plan = GalleryPlan.For(area, Of(layout), 24);
            Assert.Equal(width, plan.MainWidth, 3);
            Assert.Equal(height, plan.MainHeight, 3);
        }
    }

    [Fact]
    public void S11207_a_single_source_or_one_picture_is_that_picture_and_a_layout_never_asks_for_more_than_the_scan_found()
    {
        var area = new WidgetArea(320, 204);
        var file = new GalleryConfig { Source = GallerySource.File, Style = GalleryStyle.Grid };

        Assert.Equal(GalleryMode.Single, GalleryPlan.For(area, file, 1).Mode);
        Assert.Equal(GalleryMode.Single, GalleryPlan.For(area, Of(GalleryStyle.Mosaic), 1).Mode);
        Assert.Equal(3, GalleryPlan.For(area, Of(GalleryStyle.Grid), 3).Pictures);
        Assert.Equal(12, GalleryPlan.For(area, new GalleryConfig { Style = GalleryStyle.Scatter, Shown = 40 }, 24).Pictures);
    }

    [Fact]
    public void S11207_the_grid_is_its_cells_and_the_other_layouts_a_free_range()
    {
        // Three columns and two rows of 96, 4 apart, full-bleed.
        var grid = GalleryPlan.Sizing(Of(GalleryStyle.Grid));
        Assert.Equal(new Themia.Sdk.SizeDip((3 * 96) + (2 * 4), (2 * 96) + 4), grid.Natural);
        Assert.Equal((3, 2), (grid.WidthSteps!.Count, grid.HeightSteps!.Count));

        var small = new GalleryConfig { Style = GalleryStyle.Grid, CellSize = GalleryCellSize.Small, Columns = 6, Rows = 1 };
        Assert.Equal(new Themia.Sdk.SizeDip((6 * 64) + (5 * 4), 64), GalleryPlan.Sizing(small).Natural);

        foreach (var free in new[] { Of(GalleryStyle.Slideshow), Of(GalleryStyle.Scatter), Of(GalleryStyle.Mosaic), new GalleryConfig { Source = GallerySource.Bing, Style = GalleryStyle.Grid } })
        {
            var sizing = GalleryPlan.Sizing(free);
            Assert.Equal((new Themia.Sdk.SizeDip(160, 120), new Themia.Sdk.SizeDip(320, 240), new Themia.Sdk.SizeDip(800, 600)), (sizing.Min, sizing.Natural, sizing.Max));
        }
    }

    [Fact]
    public void S11207_dragging_the_grid_sets_its_columns_and_rows_on_a_copy()
    {
        var config = Of(GalleryStyle.Grid);
        var next = Assert.IsType<GalleryConfig>(GalleryPlan.SizeSets(config, new Themia.Sdk.SizeDip((4 * 96) + (3 * 4), (3 * 96) + 8)));

        Assert.Equal((4, 3), (next.Columns, next.Rows));
        Assert.Equal((3, 2), (config.Columns, config.Rows));
        Assert.Same(config, GalleryPlan.SizeSets(config, new Themia.Sdk.SizeDip(296, 196)));
        var slide = Of(GalleryStyle.Slideshow);
        Assert.Same(slide, GalleryPlan.SizeSets(slide, new Themia.Sdk.SizeDip(500, 500)));
    }

    // -----------------------------------------------------------------------------------------
    // [S-760] [S-1620] config
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S760_clone_carries_the_source_settings()
    {
        var config = new GalleryConfig
        {
            Source = GallerySource.File,
            FilePath = @"C:\Pictures\a.jpg",
            Style = GalleryStyle.Mosaic,
        };

        var clone = (GalleryConfig)config.Clone();

        Assert.Equal(GallerySource.File, clone.Source);
        Assert.Equal(@"C:\Pictures\a.jpg", clone.FilePath);
        Assert.Equal(GalleryStyle.Mosaic, clone.Style);
        Assert.True(clone.IsSingleSource);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(10, 10)]
    [InlineData(99999, GalleryConfig.MaxSlideSeconds)]
    public void S1620_a_slide_can_be_held_for_as_little_as_one_second(int stored, int effective)
    {
        var config = new GalleryConfig { SlideshowSeconds = stored };

        Assert.Equal(effective, config.EffectiveSlideshowSeconds);
        Assert.Equal(TimeSpan.FromSeconds(effective), config.SlideInterval);
    }
}
