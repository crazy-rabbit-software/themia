using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3410]..[S-3413] The gallery's shared sources, decoded-picture cache and per-widget place.</summary>
public sealed class GalleryMemoryTests
{
    private const long Mb = 1024 * 1024;

    private static readonly string[] Folder = [@"C:\P\a.jpg", @"C:\P\b.jpg", @"C:\P\c.jpg", @"C:\P\d.jpg", @"C:\P\e.jpg", @"C:\P\f.jpg", @"C:\P\g.jpg"];

    // ---- [S-3410] source identity ----------------------------------------------------------------

    [Fact]
    public void S3410_widgets_naming_the_same_folder_share_one_source()
    {
        var a = GallerySourceKey.For(GallerySource.Folder, @"C:\Users\Me\Pictures", recursive: false);
        var b = GallerySourceKey.For(GallerySource.Folder, @"c:\users\me\pictures\", recursive: false);

        Assert.Equal(a.Id, b.Id);
    }

    [Fact]
    public void S3410_recursion_kind_and_path_make_different_sources()
    {
        var flat = GallerySourceKey.For(GallerySource.Folder, @"C:\P", recursive: false).Id;
        var tree = GallerySourceKey.For(GallerySource.Folder, @"C:\P", recursive: true).Id;
        var file = GallerySourceKey.For(GallerySource.File, @"C:\P", recursive: true).Id;
        var other = GallerySourceKey.For(GallerySource.Folder, @"C:\Q", recursive: false).Id;

        Assert.Equal(4, new[] { flat, tree, file, other }.Distinct().Count());
    }

    [Fact]
    public void S3410_the_photo_of_the_day_is_one_source_whatever_the_path_says()
        => Assert.Equal(GallerySourceKey.Bing.Id, GallerySourceKey.For(GallerySource.Bing, @"C:\ignored", recursive: true).Id);

    [Fact]
    public void S3410_a_drive_root_keeps_its_separator()
        => Assert.Equal(
            GallerySourceKey.For(GallerySource.Folder, @"D:\", false).Id,
            GallerySourceKey.For(GallerySource.Folder, @"d:", false).Id);

    // ---- [S-3411] decoded-picture cache ---------------------------------------------------------

    [Fact]
    public void S3411_widths_are_bucketed_and_capped()
    {
        Assert.Equal(DecodeWidth.Step, DecodeWidth.Bucket(0));
        Assert.Equal(DecodeWidth.Step, DecodeWidth.Bucket(double.NaN));
        Assert.Equal(320, DecodeWidth.Bucket(301));
        Assert.Equal(320, DecodeWidth.Bucket(320));
        Assert.Equal(DecodeWidth.Max, DecodeWidth.Bucket(100_000));
    }

    [Fact]
    public void S3411_the_idle_allowance_follows_what_is_on_screen_between_a_floor_and_a_ceiling()
    {
        Assert.Equal(PictureBudget.MinIdleBytes, PictureBudget.IdleAllowance(0));
        Assert.Equal(100 * Mb, PictureBudget.IdleAllowance(100 * Mb));
        Assert.Equal(PictureBudget.MaxIdleBytes, PictureBudget.IdleAllowance(10_000 * Mb));
    }

    [Fact]
    public void S3411_the_estimate_follows_the_source_aspect_ratio()
    {
        Assert.Equal(1920L * 1080 * 4, PictureBudget.Estimate(1920, 3840, 2160));
        Assert.Equal(1000L * 750 * 4, PictureBudget.Estimate(1000, 0, 0));
    }

    [Fact]
    public void S3411_the_least_recently_used_picture_nobody_shows_goes_first()
    {
        var cache = new PictureCache<object>();
        var size = 20 * Mb;
        cache.Add(new("a", 64), new object(), size);
        cache.Add(new("b", 64), new object(), size);

        // Touch a, so b is the least recently used.
        Assert.True(cache.TryGet("a", 64, out _, out _));
        cache.Add(new("c", 64), new object(), size);

        Assert.True(cache.Contains(new("a", 64)));
        Assert.False(cache.Contains(new("b", 64)));
        Assert.True(cache.Contains(new("c", 64)));
        Assert.True(cache.Bytes - cache.PinnedBytes <= PictureBudget.IdleAllowance(cache.PinnedBytes));
    }

    [Fact]
    public void S3411_a_picture_on_screen_is_never_evicted()
    {
        var cache = new PictureCache<object>();
        var shown = new PictureKey("shown", 64);
        cache.Add(shown, new object(), 300 * Mb);
        cache.Pin(shown);

        for (var i = 0; i < 40; i++)
        {
            cache.Add(new($"p{i}", 64), new object(), 30 * Mb);
        }

        Assert.True(cache.Contains(shown));
        Assert.Equal(300 * Mb, cache.PinnedBytes);
        Assert.True(cache.Bytes - cache.PinnedBytes <= PictureBudget.IdleAllowance(cache.PinnedBytes));
    }

    [Fact]
    public void S3411_a_picture_just_decoded_is_not_evicted_before_its_frame_pins_it()
    {
        // Larger than the idle allowance on its own: added, it must still be there to pin.
        var cache = new PictureCache<object>();
        var big = new PictureKey("wall", DecodeWidth.Max);
        cache.Add(big, new object(), PictureBudget.MaxIdleBytes + Mb);

        Assert.True(cache.Contains(big));

        cache.Pin(big);
        cache.Unpin(big);

        // Nobody shows it now, and it is over the allowance by itself: it goes.
        Assert.False(cache.Contains(big));
    }

    [Fact]
    public void S3411_letting_go_keeps_the_picture_for_a_view_switch_back()
    {
        var cache = new PictureCache<object>();
        var key = new PictureKey("slide", 1920);
        cache.Add(key, new object(), PictureBudget.Estimate(1920, 1920, 1080));
        cache.Pin(key);

        // The view is switched away: its frame lets go, and nothing else is on screen.
        cache.Unpin(key);

        Assert.True(cache.Contains(key));
        Assert.Equal(0, cache.Pins(key));
        Assert.True(cache.TryGet("slide", 1920, out _, out _));
    }

    [Fact]
    public void S3411_a_larger_decode_serves_a_smaller_ask_the_smallest_first()
    {
        var cache = new PictureCache<object>();
        var big = new object();
        var mid = new object();
        cache.Add(new("p", 1024), big, Mb);
        cache.Add(new("p", 512), mid, Mb);

        Assert.True(cache.TryGet("p", 320, out var key, out var value));
        Assert.Equal(512, key.Width);
        Assert.Same(mid, value);
        Assert.False(cache.TryGet("p", 2048, out _, out _));
    }

    [Fact]
    public void S3411_trim_drops_everything_nobody_shows()
    {
        var cache = new PictureCache<object>();
        var shown = new PictureKey("shown", 64);
        cache.Add(shown, new object(), Mb);
        cache.Add(new("idle", 64), new object(), Mb);
        cache.Pin(shown);

        Assert.Equal(1, cache.Trim());
        Assert.Equal(1, cache.Count);
        Assert.True(cache.Contains(shown));
    }

    [Fact]
    public void S3411_pins_are_counted_and_a_stray_unpin_changes_nothing()
    {
        var cache = new PictureCache<object>();
        var key = new PictureKey("p", 64);
        cache.Add(key, new object(), Mb);
        cache.Pin(key);
        cache.Pin(key);
        cache.Unpin(key);

        Assert.Equal(1, cache.Pins(key));
        Assert.Equal(Mb, cache.PinnedBytes);

        cache.Unpin(key);
        cache.Unpin(key);
        cache.Unpin(new PictureKey("absent", 64));

        Assert.Equal(0, cache.Pins(key));
        Assert.Equal(0, cache.PinnedBytes);
    }

    [Fact]
    public void S3411_a_resize_and_a_removal_keep_the_byte_count_true()
    {
        var cache = new PictureCache<object>();
        var key = new PictureKey("remote", 1920);
        cache.Add(key, new object(), Mb);
        cache.Pin(key);
        cache.Resize(key, 8 * Mb);

        Assert.Equal(8 * Mb, cache.Bytes);
        Assert.Equal(8 * Mb, cache.PinnedBytes);

        cache.Remove(key);

        Assert.Equal(0, cache.Bytes);
        Assert.Equal(0, cache.PinnedBytes);
    }

    // ---- [S-3412] place, deal and shuffle -------------------------------------------------------

    [Fact]
    public void S3412_a_widget_built_again_resumes_on_the_picture_it_showed()
    {
        Assert.Equal(4, GalleryDeal.Resume(Folder, @"C:\P\e.jpg", index: 0));
    }

    [Fact]
    public void S3412_a_picture_gone_keeps_the_position_inside_the_list()
    {
        Assert.Equal(2, GalleryDeal.Resume(Folder, @"C:\P\gone.jpg", index: 2));
        Assert.Equal(Folder.Length - 1, GalleryDeal.Resume(Folder, null, index: 99));
        Assert.Equal(0, GalleryDeal.Resume(Folder, null, index: -3));
        Assert.Equal(0, GalleryDeal.Resume([], @"C:\P\a.jpg", index: 5));
    }

    [Theory]
    [InlineData(GalleryRefresh.Initial, true, 100, 10, false)]
    [InlineData(GalleryRefresh.Changed, true, 100, 10, false)]
    [InlineData(GalleryRefresh.Scheduled, true, 100, 10, true)]
    [InlineData(GalleryRefresh.Scheduled, true, 10, 10, false)]
    [InlineData(GalleryRefresh.Scheduled, false, 100, 10, false)]
    [InlineData(GalleryRefresh.Forced, false, 3, 10, true)]
    public void S3412_only_refresh_images_and_a_scheduled_rotation_deal_again(GalleryRefresh reason, bool shuffle, int found, int max, bool expected)
        => Assert.Equal(expected, GalleryDeal.ShouldReroll(reason, shuffle, found, max));

    [Fact]
    public void S3412_a_file_arriving_moves_no_other_picture_in_a_shuffle()
    {
        var before = ImageFiles.Choose(Folder, shuffle: true, seed: 11, max: Folder.Length);
        var after = ImageFiles.Choose([.. Folder, @"C:\P\new.jpg"], shuffle: true, seed: 11, max: Folder.Length + 1);

        Assert.Equal(before, after.Where(p => p != @"C:\P\new.jpg"));
    }

    [Fact]
    public void S3412_a_file_going_moves_no_other_picture_in_a_shuffle()
    {
        var before = ImageFiles.Choose(Folder, shuffle: true, seed: 5, max: Folder.Length);
        var after = ImageFiles.Choose(Folder.Where(p => p != @"C:\P\c.jpg").ToList(), shuffle: true, seed: 5, max: Folder.Length);

        Assert.Equal(before.Where(p => p != @"C:\P\c.jpg"), after);
    }

    [Fact]
    public void S3412_the_shuffle_does_not_depend_on_the_order_the_disk_listed_files_in()
    {
        var listed = ImageFiles.Choose(Folder, shuffle: true, seed: 3, max: 4);
        var reversed = ImageFiles.Choose(Folder.Reverse().ToList(), shuffle: true, seed: 3, max: 4);

        Assert.Equal(listed, reversed);
    }

    [Fact]
    public void S3412_positions_are_kept_by_widget_id_and_bounded()
    {
        var positions = new GalleryPositions();
        positions.Remember("w1", new GalleryPosition("folder|C:\\P", @"C:\P\b.jpg", 1, 42, Paused: true));

        Assert.Equal(new GalleryPosition("folder|C:\\P", @"C:\P\b.jpg", 1, 42, true), positions.Recall("w1"));
        Assert.Null(positions.Recall("w2"));
        Assert.Null(positions.Recall(null));

        for (var i = 0; i < GalleryPositions.Capacity + 10; i++)
        {
            positions.Remember($"x{i}", new GalleryPosition("bing", null, 0, i, false));
        }

        Assert.Equal(GalleryPositions.Capacity, positions.Count);
        Assert.Null(positions.Recall("w1"));
        Assert.NotNull(positions.Recall($"x{GalleryPositions.Capacity + 9}"));
    }

    // ---- [S-3413] linger ------------------------------------------------------------------------

    [Fact]
    public void S3413_a_source_nobody_shows_lingers_then_is_released()
    {
        var made = 0;
        var table = new LingerTable<string, object>(_ => { made++; return new object(); });
        var linger = (long)table.Linger.TotalMilliseconds;
        var first = table.Join("folder|C:\\P", out var created);

        Assert.True(created);
        Assert.True(table.Leave("folder|C:\\P", nowMs: 1_000));
        Assert.Equal(1, table.Lingering);
        Assert.Equal(1_000 + linger, table.NextExpiry);
        Assert.Empty(table.Expire(1_000 + linger - 1));

        // A view switch back within the linger finds the same source, read once.
        var again = table.Join("folder|C:\\P", out created);
        Assert.False(created);
        Assert.Same(first, again);
        Assert.Equal(1, made);
        Assert.Null(table.NextExpiry);

        table.Leave("folder|C:\\P", nowMs: 5_000);
        Assert.Single(table.Expire(5_000 + linger));
        Assert.Equal(0, table.Count);
    }

    [Fact]
    public void S3413_the_gallery_lingers_two_minutes()
        => Assert.Equal(TimeSpan.FromMinutes(2), new LingerTable<string, object>(_ => new object()).Linger);

    [Fact]
    public void S3413_a_double_release_and_a_clock_running_backwards_change_nothing()
    {
        var table = new LingerTable<string, object>(_ => new object());
        table.Join("k", out _);

        Assert.True(table.Leave("k", 10_000));
        Assert.False(table.Leave("k", 10_001));
        Assert.Empty(table.Expire(9_000));
        Assert.Equal(1, table.Count);
    }
}
