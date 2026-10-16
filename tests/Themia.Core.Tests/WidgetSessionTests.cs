using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-813]..[S-816] The interaction session: selections and popups end on a click anywhere else.</summary>
public sealed class WidgetSessionTests
{
    private sealed class W(string name)
    {
        public override string ToString() => name;
    }

    private static readonly W Drives = new("removable-drives");
    private static readonly W Mail = new("mail");

    [Fact]
    public void S813_the_first_press_opens_the_session_and_starts_the_watch()
    {
        var session = new WidgetSession<W>();

        var change = session.Press(Drives, PressTarget.Item, extending: false, typing: false);

        Assert.True(change.Arm);
        Assert.True(session.Open);
        Assert.Same(Drives, session.Active);
        Assert.Null(change.Left);
        Assert.Null(change.Reset);
    }

    [Fact]
    public void S813_a_second_press_does_not_start_the_watch_again()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        Assert.False(session.Press(Drives, PressTarget.Item, false, false).Arm);
    }

    [Fact]
    public void S814_a_press_on_the_active_widgets_background_clears_its_selection()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Press(Drives, PressTarget.Background, false, false);

        Assert.Same(Drives, change.Reset);
        Assert.Null(change.Left);
        Assert.Same(Drives, session.Active);
    }

    [Theory]
    [InlineData(PressTarget.Item)]
    [InlineData(PressTarget.Control)]
    public void S814_a_press_on_an_item_or_a_control_is_the_widgets_to_decide(PressTarget target)
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        Assert.Null(session.Press(Drives, target, false, false).Reset);
    }

    [Fact]
    public void S814_a_ctrl_or_shift_press_on_the_background_extends_and_keeps_the_selection()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        Assert.Null(session.Press(Drives, PressTarget.Background, extending: true, typing: false).Reset);
    }

    [Fact]
    public void S814_a_press_on_the_background_while_typing_in_the_widget_keeps_everything()
    {
        var session = new WidgetSession<W>();
        session.Press(Mail, PressTarget.Control, false, false);

        Assert.Null(session.Press(Mail, PressTarget.Background, extending: false, typing: true).Reset);
    }

    [Fact]
    public void S814_a_press_on_another_widget_leaves_the_first()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Press(Mail, PressTarget.Item, false, false);

        Assert.Same(Drives, change.Left);
        Assert.Same(Mail, session.Active);
        Assert.Equal(PopupDismissal.OtherSurfaces, change.Popups);
        Assert.False(change.Disarm);
    }

    [Fact]
    public void S814_a_press_on_the_chrome_leaves_the_widget_but_keeps_watching()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Press(null, PressTarget.Control, false, false);

        Assert.Same(Drives, change.Left);
        Assert.Null(session.Active);
        Assert.True(session.Open);
    }

    [Fact]
    public void S815_a_press_outside_every_surface_ends_the_session_and_closes_every_popup()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Outside();

        Assert.Same(Drives, change.Left);
        Assert.True(change.Disarm);
        Assert.Equal(PopupDismissal.Everywhere, change.Popups);
        Assert.False(session.Open);
        Assert.Null(session.Active);
    }

    [Fact]
    public void S816_a_popup_opened_from_the_chrome_closes_on_a_press_outside_too()
    {
        var session = new WidgetSession<W>();
        session.Press(null, PressTarget.Control, false, false);

        var change = session.Outside();

        Assert.True(change.Disarm);
        Assert.Equal(PopupDismissal.Everywhere, change.Popups);
    }

    [Fact]
    public void S815_outside_with_no_session_changes_nothing()
    {
        var session = new WidgetSession<W>();

        Assert.True(session.Outside().IsNone);
        Assert.True(session.Finish().IsNone);
    }

    [Fact]
    public void S815_escape_ends_the_session_without_touching_popups()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Finish();

        Assert.Same(Drives, change.Left);
        Assert.True(change.Disarm);
        Assert.Equal(PopupDismissal.None, change.Popups);
    }

    [Fact]
    public void S815_a_widget_that_went_away_is_not_cleared_later()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        session.Remove(Drives);

        Assert.Null(session.Outside().Left);
    }

    [Fact]
    public void S815_removing_another_widget_keeps_the_active_one()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);
        session.Remove(Mail);

        Assert.Same(Drives, session.Active);
    }

    [Theory]
    [InlineData(true, false, false, PressTarget.Item)]
    [InlineData(true, true, true, PressTarget.Item)]
    [InlineData(false, true, false, PressTarget.Control)]
    [InlineData(false, false, true, PressTarget.Control)]
    [InlineData(false, false, false, PressTarget.Background)]
    public void S814_classify_prefers_the_item_then_a_control_or_a_taken_press(bool onItem, bool onControl, bool handled, PressTarget expected)
        => Assert.Equal(expected, SessionInput.Classify(onItem, onControl, handled));

    [Theory]
    [InlineData(0x0201u, true)] // left
    [InlineData(0x0204u, true)] // right
    [InlineData(0x0207u, true)] // middle
    [InlineData(0x020Bu, true)] // x
    [InlineData(0x0200u, false)] // move
    [InlineData(0x0202u, false)] // left up
    [InlineData(0x020Au, false)] // wheel
    public void S815_only_a_button_going_down_counts_as_a_press(uint message, bool expected)
        => Assert.Equal(expected, SessionInput.IsButtonDown(message));

    [Fact]
    public void S815_a_press_on_another_process_is_outside_and_on_our_own_is_not()
    {
        Assert.True(SessionInput.IsOutside(pressedProcess: 4321, ownProcess: 1234));
        Assert.True(SessionInput.IsOutside(pressedProcess: 0, ownProcess: 1234));
        Assert.False(SessionInput.IsOutside(pressedProcess: 1234, ownProcess: 1234));
    }

    [Fact]
    public void S815_losing_the_keyboard_to_our_own_window_clears_the_widget_but_keeps_watching()
    {
        var session = new WidgetSession<W>();
        session.Press(Drives, PressTarget.Item, false, false);

        var change = session.Leave();

        Assert.Same(Drives, change.Left);
        Assert.False(change.Disarm);
        Assert.True(session.Open);
        Assert.True(session.Leave().IsNone);
    }

    [Theory]
    [InlineData("Notepad", true)]
    [InlineData("Progman", true)]
    [InlineData(null, true)]
    [InlineData("Windows.UI.Core.CoreWindow", false)]
    public void S815_the_foreground_leaving_start_after_our_click_is_not_the_user_leaving(string? previous, bool ends)
        => Assert.Equal(ends, SessionInput.ForegroundEndsSession(previous));
}
