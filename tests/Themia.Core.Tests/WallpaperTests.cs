using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The blurred wallpaper behind the cards: placement, composing, a card's part, the blur ([S-2112]..[S-2115]).</summary>
public sealed class WallpaperTests
{
    private static readonly PixelRect Display = new(0, 0, 1920, 1080);

    // ---- [S-2113] placing the image per fit ---------------------------------------------------------

    [Fact]
    public void S2113_fill_covers_the_display_cropped_and_centred()
    {
        var (rect, repeat) = WallpaperLayout.Place(WallpaperFit.Fill, 1000, 1000, Display, Display);

        Assert.False(repeat);
        Assert.Equal(new PixelRect(0, -420, 1920, 1920), rect);
    }

    [Fact]
    public void S2113_fit_fits_inside_and_leaves_the_background_around()
    {
        var (rect, _) = WallpaperLayout.Place(WallpaperFit.Fit, 1000, 1000, Display, Display);

        Assert.Equal(new PixelRect(420, 0, 1080, 1080), rect);
    }

    [Fact]
    public void S2113_stretch_is_the_display_exactly()
        => Assert.Equal(new PixelRect(0, 0, 1920, 1080), WallpaperLayout.Place(WallpaperFit.Stretch, 640, 480, Display, Display).Rect);

    [Fact]
    public void S2113_center_keeps_the_images_own_size()
    {
        Assert.Equal(new PixelRect(640, 340, 640, 400), WallpaperLayout.Place(WallpaperFit.Center, 640, 400, Display, Display).Rect);

        // Larger than the display: centred and cropped on every side.
        Assert.Equal(new PixelRect(-40, -60, 2000, 1200), WallpaperLayout.Place(WallpaperFit.Center, 2000, 1200, Display, Display).Rect);
    }

    [Fact]
    public void S2113_tile_repeats_the_image_at_its_own_size_from_the_top_left()
    {
        var (rect, repeat) = WallpaperLayout.Place(WallpaperFit.Tile, 64, 48, Display, Display);

        Assert.True(repeat);
        Assert.Equal(new PixelRect(0, 0, 64, 48), rect);
    }

    [Fact]
    public void S2113_span_fills_every_display_and_each_shows_its_part()
    {
        var left = new PixelRect(-1920, 0, 1920, 1080);
        var right = new PixelRect(0, 0, 1920, 1080);
        var desktop = new PixelRect(-1920, 0, 3840, 1080);

        var onLeft = WallpaperLayout.Place(WallpaperFit.Span, 3840, 1080, left, desktop).Rect;
        var onRight = WallpaperLayout.Place(WallpaperFit.Span, 3840, 1080, right, desktop).Rect;

        Assert.Equal(new PixelRect(0, 0, 3840, 1080), onLeft);
        Assert.Equal(new PixelRect(-1920, 0, 3840, 1080), onRight);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(double.NaN, 100)]
    public void S2113_an_image_with_no_size_covers_the_display(double w, double h)
        => Assert.Equal(new PixelRect(0, 0, 1920, 1080), WallpaperLayout.Place(WallpaperFit.Center, w, h, Display, Display).Rect);

    // ---- [S-2112] the picture's size and composing ------------------------------------------------

    [Fact]
    public void S2112_the_picture_keeps_one_pixel_in_twelve_per_side()
    {
        Assert.Equal((160, 90), WallpaperLayout.PictureSize(Display));
        Assert.Equal((214, 120), WallpaperLayout.PictureSize(new PixelRect(0, 0, 2561, 1440)));
        Assert.Equal((1, 1), WallpaperLayout.PictureSize(new PixelRect(0, 0, 0, 0)));
    }

    [Fact]
    public void S2113_compose_lays_the_image_over_the_background_colour()
    {
        var red = new RgbColor(200, 0, 0);
        var image = Pixels(2, 1, (0, 0, 255, 255), (0, 255, 0, 255));

        var picture = WallpaperLayout.Compose(4, 2, red, new WallpaperPlan(1, 1, 2, 1, false), image);

        Assert.Equal((0, 0, 200, 255), At(picture, 0, 0));
        Assert.Equal((0, 0, 200, 255), At(picture, 0, 1));
        Assert.Equal((0, 0, 255, 255), At(picture, 1, 1));
        Assert.Equal((0, 255, 0, 255), At(picture, 2, 1));
        Assert.Equal((0, 0, 200, 255), At(picture, 3, 1));
    }

    [Fact]
    public void S2113_a_transparent_image_shows_the_background_colour()
    {
        var picture = WallpaperLayout.Compose(1, 1, new RgbColor(10, 20, 30), new WallpaperPlan(0, 0, 1, 1, false), Pixels(1, 1, (0, 0, 0, 0)));

        Assert.Equal((30, 20, 10, 255), At(picture, 0, 0));
    }

    [Fact]
    public void S2113_a_tile_repeats_both_ways_and_before_its_origin()
    {
        var image = Pixels(2, 1, (1, 1, 1, 255), (2, 2, 2, 255));

        var picture = WallpaperLayout.Compose(5, 2, new RgbColor(0, 0, 0), new WallpaperPlan(1, 0, 2, 1, true), image);

        Assert.Equal([2, 1, 2, 1, 2], Enumerable.Range(0, 5).Select(x => (int)At(picture, x, 0).B));
        Assert.Equal([2, 1, 2, 1, 2], Enumerable.Range(0, 5).Select(x => (int)At(picture, x, 1).B));
    }

    [Fact]
    public void S2113_compose_refuses_an_image_of_the_wrong_size()
        => Assert.Throws<ArgumentException>(() => WallpaperLayout.Compose(2, 2, default, new WallpaperPlan(0, 0, 2, 2, false), new byte[12]));

    [Fact]
    public void S2112_a_solid_colour_is_a_picture_of_that_colour()
    {
        var picture = WallpaperLayout.Solid(Display, new RgbColor(0, 99, 177));

        Assert.Equal(160 * 90 * 4, picture.Pixels.Length);
        Assert.All(Enumerable.Range(0, 160 * 90), i => Assert.Equal(177, picture.Pixels[i * 4]));
    }

    [Fact]
    public void S2112_the_plan_scales_a_placement_into_the_picture()
    {
        var plan = WallpaperLayout.Plan(new PixelRect(0, -420, 1920, 1920), repeat: false);

        Assert.Equal(new WallpaperPlan(0, -35, 160, 160, false), plan);
        Assert.Equal(1, WallpaperLayout.Plan(new PixelRect(0, 0, 4, 4), true).Width);
    }

    // ---- [S-2114] the part a card shows ------------------------------------------------------------

    [Fact]
    public void S2114_a_card_shows_the_part_of_the_picture_behind_it()
    {
        // A 1280 × 720 DIP display (1920 × 1080 at 150 %) and its 160 × 90 picture.
        var picture = new WallpaperPicture(160, 90, new byte[160 * 90 * 4]);

        var part = WallpaperLayout.Part(new PixelRect(640, 360, 320, 240), 1280, 720, picture);

        Assert.Equal(new PixelRect(80, 45, 40, 30), part);
    }

    [Fact]
    public void S2114_moving_the_card_moves_its_part_by_the_same_fraction()
    {
        var picture = new WallpaperPicture(160, 90, new byte[160 * 90 * 4]);
        var before = WallpaperLayout.Part(new PixelRect(100, 100, 200, 100), 1920, 1080, picture);
        var after = WallpaperLayout.Part(new PixelRect(196, 100, 200, 100), 1920, 1080, picture);

        Assert.Equal(before.Width, after.Width);
        Assert.Equal(8, after.X - before.X, 6);
    }

    // ---- [S-2115] the blur -------------------------------------------------------------------------

    [Fact]
    public void S2115_the_blur_is_deterministic()
    {
        var a = Noise(37, 23, seed: 4);
        var b = (byte[])a.Clone();

        BoxBlur.Apply(a, 37, 23, BoxBlur.BackdropRadius, BoxBlur.BackdropPasses);
        BoxBlur.Apply(b, 37, 23, BoxBlur.BackdropRadius, BoxBlur.BackdropPasses);

        Assert.Equal(a, b);
    }

    [Fact]
    public void S2115_a_flat_picture_stays_flat_up_to_its_edges()
    {
        var pixels = Enumerable.Repeat<byte>(77, 9 * 5 * 4).ToArray();

        BoxBlur.Apply(pixels, 9, 5, 4, 3);

        Assert.All(pixels, p => Assert.Equal(77, p));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 9)]
    [InlineData(9, 1)]
    [InlineData(2, 3)]
    public void S2115_a_radius_wider_than_the_picture_never_reads_outside_it(int width, int height)
    {
        var pixels = Noise(width, height, seed: width + height);

        // A read past either end would throw; the edge repeats instead.
        BoxBlur.Apply(pixels, width, height, 20, 3);

        Assert.Equal(width * height * 4, pixels.Length);
    }

    [Fact]
    public void S2115_the_blur_spreads_a_bright_dot_into_its_neighbours()
    {
        var pixels = new byte[15 * 15 * 4];
        var centre = ((7 * 15) + 7) * 4;
        for (var c = 0; c < 4; c++)
        {
            pixels[centre + c] = 255;
        }

        BoxBlur.Apply(pixels, 15, 15, 2, 1);

        Assert.True(pixels[centre] < 255);
        Assert.True(pixels[(((7 * 15) + 9) * 4)] > 0);
        Assert.Equal(0, pixels[0]);
    }

    [Fact]
    public void S2115_a_buffer_of_the_wrong_size_is_refused()
        => Assert.Throws<ArgumentException>(() => BoxBlur.Apply(new byte[10], 2, 2, 1, 1));

    private static byte[] Pixels(int width, int height, params (byte B, byte G, byte R, byte A)[] colours)
    {
        Assert.Equal(width * height, colours.Length);
        return colours.SelectMany(c => new[] { c.B, c.G, c.R, c.A }).ToArray();
    }

    private static (byte B, byte G, byte R, byte A) At(WallpaperPicture picture, int x, int y)
    {
        var at = ((y * picture.Width) + x) * 4;
        return (picture.Pixels[at], picture.Pixels[at + 1], picture.Pixels[at + 2], picture.Pixels[at + 3]);
    }

    private static byte[] Noise(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * 4];
        random.NextBytes(pixels);
        return pixels;
    }
}
