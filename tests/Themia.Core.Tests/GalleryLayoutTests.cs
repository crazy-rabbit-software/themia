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

        foreach (var layout in Enum.GetValues<GalleryStyle>())
        {
            var plan = GalleryPlan.For(area, layout, singleSource: false, available: 24, Gap);

            Assert.True(plan.MainWidth > 0 && plan.MainHeight > 0, $"{tier} {layout}: no main area");
            Assert.True(plan.MainWidth <= width + 0.001, $"{tier} {layout}: main too wide");
            Assert.True(plan.MainHeight <= height + 0.5, $"{tier} {layout}: {plan.MainHeight} > {height}");
        }
    }

    [Fact]
    public void S767_xs_is_one_picture_with_nothing_on_it_whatever_the_layout()
    {
        var xs = new WidgetArea(120, 76);

        foreach (var layout in Enum.GetValues<GalleryStyle>())
        {
            var plan = GalleryPlan.For(xs, layout, false, 24, Gap);
            Assert.Equal(1, plan.Pictures);
            Assert.False(plan.Counter);
            Assert.False(plan.Controls);
        }
    }

    [Fact]
    public void S767_slideshow_discloses_the_counter_and_no_up_next_thumbnails()
    {
        // The "up next" thumbnails are retired (concise widgets): the slide is the whole slot at
        // every size, a wide strip and a tall column included.
        GalleryPlan At(double w, double h) => GalleryPlan.For(new WidgetArea(w, h), GalleryStyle.Slideshow, false, 24, Gap);

        var s = At(200, 106);
        Assert.True(s.Counter && s.Controls);

        foreach (var (w, h) in new[] { (480.0, 116.0), (200.0, 356.0), (560.0, 376.0), (800.0, 516.0) })
        {
            var plan = At(w, h);
            Assert.Equal(GalleryMode.Slideshow, plan.Mode);
            Assert.Equal(w, plan.MainWidth, 3);
            Assert.Equal(h, plan.MainHeight, 3);
        }
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S2760_no_words_under_any_picture_the_picture_takes_the_height(string tier, double width, double height)
    {
        var area = new WidgetArea(width, height);

        // A single source (a file, the photo of the day) is the picture and nothing else.
        var single = GalleryPlan.For(area, GalleryStyle.Grid, singleSource: true, available: 1, Gap);
        Assert.Equal(GalleryMode.Single, single.Mode);
        Assert.Equal(height, single.MainHeight, 3);

        foreach (var layout in Enum.GetValues<GalleryStyle>())
        {
            // No file name under a slide at any size: the picture keeps all the height.
            var plan = GalleryPlan.For(area, layout, singleSource: false, available: 24, Gap);
            Assert.True(Math.Abs(plan.MainHeight - height) < 0.001, $"{tier} {layout}: {plan.MainHeight} != {height}");
        }
    }

    [Fact]
    public void S767_layouts_show_more_pictures_as_the_card_grows()
    {
        foreach (var layout in new[] { GalleryStyle.Grid, GalleryStyle.Scatter, GalleryStyle.Mosaic })
        {
            var m = GalleryPlan.For(new WidgetArea(320, 196), layout, false, 24, Gap).Pictures;
            var l = GalleryPlan.For(new WidgetArea(560, 376), layout, false, 24, Gap).Pictures;
            var xl = GalleryPlan.For(new WidgetArea(800, 516), layout, false, 24, Gap).Pictures;

            Assert.True(m > 1, $"{layout} M");
            Assert.True(l > m, $"{layout} L");
            Assert.True(xl >= l, $"{layout} XL");
        }
    }

    [Fact]
    public void S767_a_layout_never_asks_for_more_pictures_than_the_scan_found()
        => Assert.Equal(3, GalleryPlan.For(new WidgetArea(800, 516), GalleryStyle.Grid, false, 3, Gap).Pictures);

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

    // -----------------------------------------------------------------------------------------
    // [S-3112] the photo of the day's credit tooltip
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S3112_a_bing_reply_carries_only_the_credit_not_the_title()
    {
        const string json = """
            {"images":[{"url":"/th?id=OHR.Example_EN-US123_1920x1080.jpg","title":"A lake","copyright":"Lake Bled, Slovenia (© Jane Doe/Agency)"}]}
            """;

        Assert.Equal("© Jane Doe/Agency", BingPhoto.Parse(json)?.Credit?.Text);
    }

    [Fact]
    public void S3112_a_missing_credit_is_no_tooltip_but_still_a_photo()
    {
        var photo = BingPhoto.Parse("""{"images":[{"url":"/th?id=x.jpg"}]}""");

        Assert.NotNull(photo);
        Assert.Null(photo.Credit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t\u0000")]
    [InlineData("‮​")]
    [InlineData("(© )")]
    public void S3112_a_blank_credit_is_none(string? raw)
        => Assert.Null(BingCredit.Parse(raw));

    [Theory]
    [InlineData("  © Someone  ", "© Someone")]
    [InlineData("© Some\r\none\u0007here", "© Some one here")]
    [InlineData("© Evil‮gnp.exe", "© Evilgnp.exe")]
    [InlineData("Place (© Open-ended", "© Open-ended")]
    [InlineData("Credit without a mark", "Credit without a mark")]
    [InlineData("A (© First) B (© Second)", "© Second")]
    public void S3112_the_credit_is_cleaned(string raw, string expected)
        => Assert.Equal(expected, BingCredit.Parse(raw)?.Text);

    [Fact]
    public void S3112_a_long_credit_is_capped()
    {
        var credit = BingCredit.Parse("© " + new string('x', 1000));

        Assert.NotNull(credit);
        Assert.Equal(BingCredit.MaxLength, credit.Text.Length);
    }

    [Fact]
    public void S3112_the_cap_never_splits_a_surrogate_pair()
    {
        var credit = BingCredit.Parse("© " + new string('x', BingCredit.MaxLength - 3) + "😀😀");

        Assert.NotNull(credit);
        Assert.True(credit.Text.Length <= BingCredit.MaxLength);
        Assert.False(char.IsHighSurrogate(credit.Text[^1]));
    }

    // -----------------------------------------------------------------------------------------
    // [S-760]/[S-772] config
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S3792_every_gallery_rescans_hourly()
    {
        Assert.Equal(TimeSpan.FromHours(1), new GalleryConfig().TickInterval);
        Assert.Equal(GallerySourceKey.RescanInterval, new GalleryConfig { Source = GallerySource.Bing }.TickInterval);
    }

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
