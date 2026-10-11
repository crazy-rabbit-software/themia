using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The gallery's layouts and its disclosure ladder: the scatter and mosaic generators (seeded, so
/// a resize re-derives the same arrangement), the grid's shape, the ladder at the seven size
/// tiers, the photo of the day's trust boundary, and the config's closed sets.
/// </summary>
public class GalleryLayoutTests
{
    private const double Gap = 4;

    /// <summary>The seven size tiers, less a 44-DIP header: the slots the view is given.</summary>
    public static TheoryData<string, double, double> Tiers => new()
    {
        { "XS", 120, 76 },
        { "S", 200, 106 },
        { "M", 320, 196 },
        { "Wide", 480, 116 },
        { "Tall", 200, 356 },
        { "L", 560, 376 },
        { "XL", 800, 516 },
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
            var prints = GalleryArrangement.Scatter(800, 516, 20, seed);

            Assert.Contains(prints, p => p.CenterX < 200 && p.CenterY < 129);
            Assert.Contains(prints, p => p.CenterX > 600 && p.CenterY < 129);
            Assert.Contains(prints, p => p.CenterX < 200 && p.CenterY > 387);
            Assert.Contains(prints, p => p.CenterX > 600 && p.CenterY > 387);
        }
    }

    [Fact]
    public void S764_scatter_is_reproducible_from_its_seed_and_varies_between_seeds()
    {
        var first = GalleryArrangement.Scatter(320, 196, 5, 42);
        var again = GalleryArrangement.Scatter(320, 196, 5, 42);
        var other = GalleryArrangement.Scatter(320, 196, 5, 43);

        Assert.Equal(first, again);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void S764_print_size_follows_the_room_each_print_has_not_the_card_width()
    {
        // A wide strip: the original's 45%-of-width print would be 216 DIPs on a 116-DIP strip.
        var prints = GalleryArrangement.Scatter(480, 116, 4, 9);
        Assert.All(prints, print => Assert.True(print.Side <= 116 * 0.9 + 0.001));
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
            var cells = GalleryArrangement.Mosaic(480, 116, 2, seed);

            // Wide: the cut is vertical, so both cells keep the full height.
            Assert.All(cells, cell => Assert.Equal(116, cell.Height, 3));
            Assert.InRange(cells[0].Width / 480, GalleryArrangement.MinSplit - 0.0001, GalleryArrangement.MaxSplit + 0.0001);
        }
    }

    [Fact]
    public void S765_mosaic_is_reproducible_from_its_seed()
        => Assert.Equal(GalleryArrangement.Mosaic(320, 196, 6, 5), GalleryArrangement.Mosaic(320, 196, 6, 5));

    // -----------------------------------------------------------------------------------------
    // [S-766] grid
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(320, 196, 6, 3, 2)]
    [InlineData(480, 116, 4, 4, 1)]
    [InlineData(200, 356, 6, 2, 3)]
    [InlineData(800, 516, 24, 6, 4)]
    public void S766_grid_shape_keeps_tiles_close_to_square(double width, double height, int count, int columns, int rows)
        => Assert.Equal((columns, rows), GalleryArrangement.GridShape(width, height, count, Gap));

    [Fact]
    public void S766_grid_capacity_grows_with_the_card_and_stops_at_the_cap()
    {
        Assert.Equal(1, GalleryArrangement.GridCapacity(120, 76, Gap));
        Assert.Equal(6, GalleryArrangement.GridCapacity(320, 196, Gap));
        Assert.Equal(GalleryArrangement.MaxTiles, GalleryArrangement.GridCapacity(4000, 4000, Gap));
    }

    // -----------------------------------------------------------------------------------------
    // [S-767] the ladder
    // -----------------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S767_every_layout_fits_its_slot_at_every_tier(string tier, double width, double height)
    {
        var area = new WidgetArea(width, height);

        foreach (var layout in Enum.GetValues<GalleryLayout>())
        {
            var plan = GalleryPlan.For(area, layout, singleSource: false, available: 24, caption: false, Gap);

            Assert.True(plan.MainWidth > 0 && plan.MainHeight > 0, $"{tier} {layout}: no main area");
            Assert.True(plan.MainWidth <= width + 0.001, $"{tier} {layout}: main too wide");

            var stripHeight = plan.Strip == GalleryStrip.Below ? plan.StripExtent + Gap : 0;
            var total = plan.MainHeight + stripHeight + GalleryPlan.CaptionHeight(plan.CaptionLines, Gap);
            Assert.True(total <= height + 0.5, $"{tier} {layout}: {total} > {height}");
        }
    }

    [Fact]
    public void S767_xs_is_one_picture_with_nothing_on_it_whatever_the_layout()
    {
        var xs = new WidgetArea(120, 76);

        foreach (var layout in Enum.GetValues<GalleryLayout>())
        {
            var plan = GalleryPlan.For(xs, layout, false, 24, false, Gap);
            Assert.Equal(1, plan.Pictures);
            Assert.False(plan.Counter);
            Assert.False(plan.Controls);
            Assert.Equal(GalleryStrip.None, plan.Strip);
        }
    }

    [Fact]
    public void S767_slideshow_discloses_counter_then_strip_then_caption()
    {
        GalleryPlan At(double w, double h) => GalleryPlan.For(new WidgetArea(w, h), GalleryLayout.Slideshow, false, 24, false, Gap);

        var s = At(200, 106);
        Assert.True(s.Counter && s.Controls);
        Assert.Equal(GalleryStrip.None, s.Strip);

        var wide = At(480, 116);
        Assert.Equal(GalleryStrip.Beside, wide.Strip);
        Assert.True(wide.MainWidth >= 116 * GalleryPlan.BesideAspect);

        var tall = At(200, 356);
        Assert.Equal(GalleryStrip.Below, tall.Strip);
        Assert.Equal(0, tall.CaptionLines);

        var l = At(560, 376);
        Assert.Equal(GalleryStrip.Below, l.Strip);
        Assert.Equal(5, l.StripCount);
        Assert.Equal(1, l.CaptionLines);

        // XL gives the pictures more room, not more words: the file name stays the one line
        // (no "3 of 24 · folder" line under it).
        var xl = At(800, 516);
        Assert.Equal(1, xl.CaptionLines);
        Assert.True(xl.StripCount > l.StripCount);
    }

    [Fact]
    public void S767_layouts_show_more_pictures_as_the_card_grows()
    {
        foreach (var layout in new[] { GalleryLayout.Grid, GalleryLayout.Scatter, GalleryLayout.Mosaic })
        {
            var m = GalleryPlan.For(new WidgetArea(320, 196), layout, false, 24, false, Gap).Pictures;
            var l = GalleryPlan.For(new WidgetArea(560, 376), layout, false, 24, false, Gap).Pictures;
            var xl = GalleryPlan.For(new WidgetArea(800, 516), layout, false, 24, false, Gap).Pictures;

            Assert.True(m > 1, $"{layout} M");
            Assert.True(l > m, $"{layout} L");
            Assert.True(xl >= l, $"{layout} XL");
        }
    }

    [Fact]
    public void S767_a_layout_never_asks_for_more_pictures_than_the_scan_found()
        => Assert.Equal(3, GalleryPlan.For(new WidgetArea(800, 516), GalleryLayout.Grid, false, 3, false, Gap).Pictures);

    [Fact]
    public void S763_photo_of_the_day_caption_sheds_credit_then_title()
    {
        GalleryPlan At(double w, double h) => GalleryPlan.For(new WidgetArea(w, h), GalleryLayout.Grid, true, 1, true, Gap);

        Assert.Equal(GalleryMode.Single, At(560, 376).Mode);
        Assert.Equal(2, At(560, 376).CaptionLines);
        Assert.Equal(1, At(200, 106).CaptionLines);
        Assert.Equal(0, At(120, 76).CaptionLines);

        // A wide strip keeps the photograph rather than a letterbox with words under it.
        Assert.Equal(0, At(480, 116).CaptionLines);

        // A file never captions; only the photo of the day has words to show.
        Assert.Equal(0, GalleryPlan.For(new WidgetArea(560, 376), GalleryLayout.Grid, true, 1, false, Gap).CaptionLines);
    }

    // -----------------------------------------------------------------------------------------
    // [S-762] the photo of the day
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S762_a_bing_reply_becomes_a_photo_on_bings_own_host()
    {
        const string json = """
            {"images":[{"url":"/th?id=OHR.Example_EN-US123_1920x1080.jpg&rf=x&pid=hp","title":"A lake","copyright":"© Someone"}]}
            """;

        var photo = BingPhoto.Parse(json);

        Assert.NotNull(photo);
        Assert.Equal("https", photo.Image.Scheme);
        Assert.Equal("www.bing.com", photo.Image.Host);
        Assert.StartsWith("/th", photo.Image.PathAndQuery, StringComparison.Ordinal);
        Assert.Equal("A lake", photo.Caption.Title);
        Assert.Equal("© Someone", photo.Caption.Credit);
    }

    [Theory]
    [InlineData("""{"images":[{"url":"//evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"https://evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"th?id=x"}]}""")]
    [InlineData("""{"images":[{"url":"/\\evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[{"url":"@evil.example/x.jpg"}]}""")]
    [InlineData("""{"images":[]}""")]
    [InlineData("""{"images":[{"title":"no url"}]}""")]
    [InlineData("""{}""")]
    [InlineData("null")]
    [InlineData("<html>not json</html>")]
    [InlineData("")]
    public void S762_anything_that_is_not_a_path_on_bing_is_refused(string json)
        => Assert.Null(BingPhoto.Parse(json));

    [Fact]
    public void S762_missing_words_are_empty_not_null()
    {
        var photo = BingPhoto.Parse("""{"images":[{"url":"/th?id=x.jpg"}]}""");
        Assert.NotNull(photo);
        Assert.Equal(string.Empty, photo.Caption.Title);
        Assert.Equal(string.Empty, photo.Caption.Credit);
    }

    // -----------------------------------------------------------------------------------------
    // [S-760]/[S-772] config
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(5, 5)]
    [InlineData(15, 15)]
    [InlineData(60, 60)]
    [InlineData(0, 5)]
    [InlineData(-30, 5)]
    [InlineData(20, 15)]
    [InlineData(100000, 60)]
    public void S772_refresh_snaps_a_corrupt_value_to_an_offered_cadence(int stored, int expected)
    {
        var config = new GalleryConfig { RefreshMinutes = stored };
        Assert.Equal(expected, config.EffectiveRefreshMinutes);
        Assert.Equal(TimeSpan.FromMinutes(expected), config.TickInterval);
    }

    [Fact]
    public void S760_clone_carries_the_source_settings()
    {
        var config = new GalleryConfig
        {
            Source = GallerySource.File,
            FilePath = @"C:\Pictures\a.jpg",
            ShowCaption = false,
            RefreshMinutes = 15,
            Layout = GalleryLayout.Mosaic,
        };

        var clone = (GalleryConfig)config.Clone();

        Assert.Equal(GallerySource.File, clone.Source);
        Assert.Equal(@"C:\Pictures\a.jpg", clone.FilePath);
        Assert.False(clone.ShowCaption);
        Assert.Equal(15, clone.RefreshMinutes);
        Assert.Equal(GalleryLayout.Mosaic, clone.Layout);
        Assert.True(clone.IsSingleSource);
    }

    [Theory]
    [InlineData(GalleryStatus.NotFound, GallerySource.File, "picture")]
    [InlineData(GalleryStatus.NotFound, GallerySource.Folder, "folder")]
    [InlineData(GalleryStatus.Offline, GallerySource.Bing, "Offline")]
    [InlineData(GalleryStatus.Unavailable, GallerySource.Bing, "today's photo")]
    [InlineData(GalleryStatus.Loading, GallerySource.Bing, "today's photo")]
    public void S761_each_source_names_its_own_empty_state(GalleryStatus status, GallerySource source, string expected)
        => Assert.Contains(expected, GalleryFormatting.StatusMessage(status, FolderPathProblem.None, source), StringComparison.Ordinal);

    [Theory]
    [InlineData(GalleryStatus.NoFolder, GallerySource.Folder)]
    [InlineData(GalleryStatus.NoFile, GallerySource.File)]
    public void S1862_nothing_chosen_is_the_set_up_button_not_a_sentence(GalleryStatus status, GallerySource source)
        => Assert.Equal(string.Empty, GalleryFormatting.StatusMessage(status, FolderPathProblem.None, source));

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
