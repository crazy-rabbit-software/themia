using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-15900] [S-15901] A render settles on its page's own signals and frames under one ceiling, never a fixed sleep.</summary>
public sealed class RenderSettlingTests
{
    private static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Early = TimeSpan.FromMilliseconds(50);

    [Fact]
    public void S15900_work_still_loading_is_waited_for()
    {
        var settle = new RenderSettling(Ceiling);
        Assert.Equal(SettleStep.AwaitWork, settle.Next(Early, pending: 2, same: null));
    }

    [Fact]
    public void S15900_the_first_look_is_never_settled()
    {
        var settle = new RenderSettling(Ceiling);
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, pending: 0, same: null));
    }

    [Fact]
    public void S15900_settled_after_the_quiet_frames_in_a_row()
    {
        var settle = new RenderSettling(Ceiling);
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, 0, null));
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, 0, true));
        Assert.Equal(SettleStep.Settled, settle.Next(Early, 0, true));
    }

    [Fact]
    public void S15900_a_change_starts_the_quiet_frames_again()
    {
        var settle = new RenderSettling(Ceiling);
        _ = settle.Next(Early, 0, null);
        _ = settle.Next(Early, 0, true);
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, 0, false));
        Assert.Equal(0, settle.SameInARow);
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, 0, true));
        Assert.Equal(SettleStep.Settled, settle.Next(Early, 0, true));
    }

    [Fact]
    public void S15900_work_that_starts_again_resets_the_quiet_frames()
    {
        var settle = new RenderSettling(Ceiling);
        _ = settle.Next(Early, 0, true);
        Assert.Equal(SettleStep.AwaitWork, settle.Next(Early, 1, null));
        Assert.Equal(0, settle.SameInARow);
        Assert.Equal(SettleStep.NextFrame, settle.Next(Early, 0, true));
    }

    [Fact]
    public void S15900_the_ceiling_ends_a_settle_still_loading()
    {
        var settle = new RenderSettling(Ceiling);
        Assert.Equal(SettleStep.CeilingHit, settle.Next(Ceiling, pending: 1, same: null));
    }

    [Fact]
    public void S15900_the_ceiling_ends_a_picture_still_changing()
    {
        var settle = new RenderSettling(Ceiling);
        Assert.Equal(SettleStep.CeilingHit, settle.Next(Ceiling + Early, pending: 0, same: false));
    }

    [Fact]
    public void S15900_a_picture_settled_at_the_ceiling_is_settled_not_late()
    {
        var settle = new RenderSettling(Ceiling, quietFrames: 1);
        Assert.Equal(SettleStep.Settled, settle.Next(Ceiling, pending: 0, same: true));
    }

    [Fact]
    public void S15900_the_ceiling_line_names_what_was_busy_loudly()
    {
        var loading = RenderSettling.CeilingLine("page-dark-general.png", Ceiling, ["an image", "an image", "WeatherView"]);
        Assert.Contains("NOT SETTLED after 10 s", loading, StringComparison.Ordinal);
        Assert.Contains("still loading an image, WeatherView", loading, StringComparison.Ordinal);

        var changing = RenderSettling.CeilingLine("menu.png", TimeSpan.FromSeconds(2.5), []);
        Assert.Contains("after 2.5 s", changing, StringComparison.Ordinal);
        Assert.Contains("the picture was still changing", changing, StringComparison.Ordinal);
    }

    [Fact]
    public void S15900_a_settle_needs_a_ceiling_and_a_quiet_frame()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSettling(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderSettling(Ceiling, quietFrames: 0));
    }

    [Fact]
    public void S15901_the_same_bytes_are_the_same_picture()
    {
        var a = Picture(4, 3);
        var b = Picture(4, 3);
        Assert.True(RenderSettling.SamePicture(a, b, 4, 4, []));
    }

    [Fact]
    public void S15901_one_pixel_changed_is_another_picture()
    {
        var a = Picture(4, 3);
        var b = Picture(4, 3);
        b[Offset(4, 2, 1) + 1]++;
        Assert.False(RenderSettling.SamePicture(a, b, 4, 4, []));
    }

    [Fact]
    public void S15901_another_size_is_another_picture()
    {
        Assert.False(RenderSettling.SamePicture(Picture(4, 3), Picture(4, 2), 4, 4, []));
        Assert.False(RenderSettling.SamePicture(Picture(4, 3), Picture(3, 4), 5, 4, []));
    }

    [Fact]
    public void S15901_a_change_inside_a_mask_is_not_a_change()
    {
        var a = Picture(6, 4);
        var b = Picture(6, 4);
        b[Offset(6, 2, 1)] ^= 0xFF;
        b[Offset(6, 3, 2) + 2] ^= 0xFF;
        Assert.True(RenderSettling.SamePicture(a, b, 6, 4, [new PixelBox(2, 1, 2, 2)]));
    }

    [Fact]
    public void S15901_a_change_beside_a_mask_still_counts()
    {
        var a = Picture(6, 4);
        var b = Picture(6, 4);
        b[Offset(6, 4, 1)] ^= 0xFF;
        Assert.False(RenderSettling.SamePicture(a, b, 6, 4, [new PixelBox(2, 1, 2, 2)]));

        var c = Picture(6, 4);
        c[Offset(6, 2, 3)] ^= 0xFF;
        Assert.False(RenderSettling.SamePicture(a, c, 6, 4, [new PixelBox(2, 1, 2, 2)]));
    }

    [Fact]
    public void S15901_masks_that_overlap_or_reach_past_the_edge_are_one_mask()
    {
        var a = Picture(6, 4);
        var b = Picture(6, 4);
        b[Offset(6, 0, 0)] ^= 0xFF;
        b[Offset(6, 5, 0)] ^= 0xFF;
        b[Offset(6, 3, 3)] ^= 0xFF;
        Assert.True(RenderSettling.SamePicture(a, b, 6, 4, [new PixelBox(-2, 0, 3, 1), new PixelBox(2, 0, 9, 4), new PixelBox(1, 3, 3, 1)]));
    }

    [Fact]
    public void S15900_the_ceiling_line_says_where_the_picture_kept_changing()
    {
        var line = RenderSettling.CeilingLine("tasks.png", Ceiling, [], "(10,20 4x4 DIPs: ProgressRing)");
        Assert.Contains("the picture was still changing at (10,20 4x4 DIPs: ProgressRing)", line, StringComparison.Ordinal);
        Assert.Contains("still loading an image", RenderSettling.CeilingLine("x", Ceiling, ["an image"], "(ignored)"), StringComparison.Ordinal);
    }

    [Fact]
    public void S15900_the_changed_box_holds_every_changed_pixel()
    {
        var a = Picture(6, 4);
        var b = Picture(6, 4);
        b[Offset(6, 1, 1) + 3] ^= 0xFF;
        b[Offset(6, 4, 3)] ^= 0xFF;
        Assert.Equal(new PixelBox(1, 1, 4, 3), RenderSettling.ChangedBox(a, b, 6, 4));
    }

    [Fact]
    public void S15900_no_change_no_box()
    {
        Assert.Null(RenderSettling.ChangedBox(Picture(6, 4), Picture(6, 4), 6, 4));
        Assert.Null(RenderSettling.ChangedBox(Picture(6, 4), Picture(6, 3), 6, 4));
    }

    private static byte[] Picture(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 7);
        }

        return pixels;
    }

    private static int Offset(int width, int x, int y) => ((y * width) + x) * 4;
}
