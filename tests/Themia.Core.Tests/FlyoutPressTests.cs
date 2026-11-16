using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2346] The open flyout is never drawn again under a press held on it, nor inside the event that asked.</summary>
public sealed class FlyoutPressTests
{
    [Fact]
    public void S2346_with_no_press_held_one_drawing_is_posted_and_drawn_on_its_turn()
    {
        var press = new FlyoutPress();
        Assert.True(press.Redraw());
        Assert.True(press.Posted);
        Assert.False(press.Release());
        Assert.True(press.Run());
        Assert.False(press.Posted);
    }

    [Fact]
    public void S2346_asks_while_a_drawing_is_posted_are_that_drawing()
    {
        var press = new FlyoutPress();
        Assert.True(press.Redraw());
        Assert.False(press.Redraw());
        Assert.False(press.Redraw());
        Assert.True(press.Run());
        Assert.False(press.Run());
        Assert.True(press.Redraw());
    }

    [Fact]
    public void S2346_a_change_while_a_row_is_held_waits_and_is_drawn_once_after_the_press_lets_go()
    {
        // The regression: a widget's config written while a section's row was pressed drew the page again, the
        // row was replaced, and the release clicked nothing.
        var press = new FlyoutPress();
        press.Press();
        Assert.False(press.Redraw());
        Assert.False(press.Redraw());
        Assert.True(press.Pending);
        Assert.False(press.Posted);

        Assert.True(press.Release());
        Assert.False(press.Held);
        Assert.False(press.Release());
        Assert.True(press.Run());
        Assert.False(press.Run());
    }

    [Fact]
    public void S2346_the_drawing_a_release_owes_is_posted_never_drawn_inside_the_release()
    {
        // The regression (owner's log, 2026-11-15 18:01): the release drew the page at once, from inside the pointer
        // event XAML was still handing to the pressed row; the panel's Clear threw out of the event. A release posts:
        // the event it lets go in ends first, its own and the surface's (a press dragged off the page), however many.
        var press = new FlyoutPress();
        press.Press();
        Assert.False(press.Redraw());

        Assert.True(press.Release());
        Assert.True(press.Posted);
        Assert.False(press.Release()); // the capture lost after the release
        Assert.False(press.Release()); // the surface's own release
        Assert.False(press.Redraw()); // a change made by the click itself: the posted drawing has it

        Assert.True(press.Run());
    }

    [Fact]
    public void S2346_a_press_landing_before_the_posted_drawing_runs_holds_it_again()
    {
        var press = new FlyoutPress();
        Assert.True(press.Redraw());
        press.Press();

        Assert.False(press.Run());
        Assert.True(press.Pending);

        Assert.True(press.Release());
        Assert.True(press.Run());
    }

    [Fact]
    public void S2346_a_press_that_let_go_with_nothing_asked_owes_nothing()
    {
        var press = new FlyoutPress();
        press.Press();
        Assert.False(press.Release());
        Assert.False(press.Run());
    }

    [Fact]
    public void S2346_a_closed_flyout_holds_owes_and_posts_nothing()
    {
        var press = new FlyoutPress();
        press.Press();
        Assert.False(press.Redraw());
        press.Reset();
        Assert.False(press.Held);
        Assert.False(press.Release());
        Assert.True(press.Redraw());
        press.Reset();
        Assert.False(press.Run());
        Assert.True(press.Redraw());
    }
}
