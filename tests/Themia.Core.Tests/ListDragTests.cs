using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3732] [S-3747] A press on a menu list's line, the drag it becomes and how it ends.</summary>
public sealed class ListDragTests
{
    [Fact]
    public void S3732_a_press_that_travels_less_than_the_threshold_is_no_drag()
    {
        var drag = new ListDrag(100);
        Assert.False(drag.Move(103, buttonDown: true));
        Assert.False(drag.Active);

        // Its release is a click: nothing to drop.
        Assert.Equal(ListDragEnd.None, drag.Release());
    }

    [Fact]
    public void S3732_a_move_with_the_button_up_starts_nothing()
    {
        var drag = new ListDrag(100);
        Assert.False(drag.Move(160, buttonDown: false));
        Assert.False(drag.Active);
    }

    [Fact]
    public void S3732_the_release_drops_in_the_aimed_gap()
    {
        var drag = new ListDrag(100);
        Assert.True(drag.Move(96, buttonDown: true));
        Assert.True(drag.Aim(3));
        Assert.False(drag.Aim(3));
        Assert.Equal(ListDragEnd.Drop, drag.Release());
        Assert.Equal(3, drag.Gap);
    }

    [Fact]
    public void S3747_the_capture_the_release_gives_up_is_the_drop()
    {
        // The regression: the line's button released its capture while it handled the release,
        // before the release reached the menu, and the menu took the capture lost for a cancel.
        var drag = new ListDrag(100);
        drag.Move(140, buttonDown: true);
        drag.Aim(2);

        Assert.Equal(ListDragEnd.Drop, drag.CaptureLost(buttonDown: false));

        // The release that follows finds the drag ended: it moves nothing a second time.
        Assert.Equal(ListDragEnd.None, drag.Release());
    }

    [Fact]
    public void S3747_a_capture_taken_away_with_the_button_down_cancels()
    {
        var drag = new ListDrag(100);
        drag.Move(140, buttonDown: true);
        drag.Aim(2);

        Assert.Equal(ListDragEnd.Cancel, drag.CaptureLost(buttonDown: true));
        Assert.Equal(ListDragEnd.None, drag.Release());
    }

    [Fact]
    public void S3732_a_drag_never_aimed_moves_nothing()
    {
        var drag = new ListDrag(100);
        drag.Move(140, buttonDown: true);
        Assert.Equal(ListDragEnd.Cancel, drag.Release());
    }

    [Fact]
    public void S3732_a_cancelled_drag_ends_once()
    {
        var drag = new ListDrag(100);
        drag.Move(140, buttonDown: true);
        drag.Aim(0);
        Assert.Equal(ListDragEnd.Cancel, drag.Cancel());
        Assert.Equal(ListDragEnd.None, drag.CaptureLost(buttonDown: false));
        Assert.False(drag.Move(200, buttonDown: true));
    }
}
