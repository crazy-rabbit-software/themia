using Themia.Core.Shell;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13060] The first press on a widget, dragged, moves it whatever is under the pointer (owner, 2026-11-19: "sometimes when
/// i hover over a widget an try to drag it it does not work on the first try"); a press let go without moving stays the
/// part's click; a slider's track waits for the release.
/// </summary>
public sealed class FirstDragTests
{
    private static bool Moves(PressPart part, bool handled, bool pinned = false, bool editMode = false, bool preview = false, bool ownsDrags = false)
        => CardGestures.MovesLocked(editMode, preview, leftButton: true, ownsDrags, pinned, part, handled);

    [Theory]
    [InlineData(PressPart.Item, false)]     // a list row: rows click on the tap
    [InlineData(PressPart.Ground, false)]   // an empty spot, a chart, a text
    [InlineData(PressPart.Control, true)]   // a button took the press, focused and captured
    [InlineData(PressPart.Control, false)]  // a disabled button leaves it
    [InlineData(PressPart.Track, true)]     // a slider's track waits for its release
    [InlineData(PressPart.Text, true)]      // a text box at rest, focused by this press
    public void S13060_a_press_on_any_part_of_a_widget_dragged_moves_the_card_on_the_first_press(PressPart part, bool handled)
        => Assert.True(Moves(part, handled));

    [Theory]
    [InlineData(PressPart.Thumb, true)]
    [InlineData(PressPart.Typing, true)]
    public void S13060_a_thumb_or_a_text_being_typed_in_keeps_its_drag(PressPart part, bool handled)
        => Assert.False(Moves(part, handled));

    [Theory]
    [InlineData(PressPart.Ground)]  // a fence's body: the desktop's band
    [InlineData(PressPart.Item)]    // a fence's or a folder's file: the shell's drag
    public void S13060_a_part_that_takes_the_press_for_a_drag_of_its_own_keeps_it(PressPart part)
        => Assert.False(Moves(part, handled: true));

    [Fact]
    public void S13060_a_button_no_longer_needs_to_be_the_whole_widget_to_move_it()
    {
        // The regression: only a widget that is one button (IWidgetButton) armed the move on a handled press; a button
        // inside any other widget (a stopwatch's, the music's, a list's tool line) took the press and the drag did nothing.
        Assert.True(PressGesture.CardMayMove(PressPart.Control, handled: true));
        Assert.True(PressGesture.CardMayMove(PressPart.Track, handled: true));
        Assert.True(PressGesture.CardMayMove(PressPart.Text, handled: true));
    }

    [Fact]
    public void S13060_lock_widgets_edit_mode_a_preview_and_a_view_that_drags_its_own_content_still_win()
    {
        foreach (var part in Enum.GetValues<PressPart>())
        {
            foreach (var handled in new[] { false, true })
            {
                Assert.False(Moves(part, handled, pinned: true));
                Assert.False(Moves(part, handled, editMode: true));
                Assert.False(Moves(part, handled, preview: true));
                Assert.False(Moves(part, handled, ownsDrags: true));
                Assert.False(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: false, ownsDrags: false, pinned: false, part, handled));
            }
        }
    }

    [Fact]
    public void S13060_the_first_press_after_another_app_never_takes_the_foreground_while_it_is_held()
    {
        // The lease the press asks for waits for the release: activating the surface mid-press takes the pointer from the move.
        Assert.Equal(ForegroundStep.AfterPress, PressForeground.When(isForeground: false, mayTake: true, pressHeld: true, shellDrag: false));
    }

    [Fact]
    public void S13060_one_drag_threshold_for_the_card_and_the_parts_that_wait_for_the_release()
    {
        Assert.Equal(PressGesture.DragThreshold, CardGestures.DragThreshold);
        Assert.False(CardGestures.Travelled(PressGesture.DragThreshold - 0.5, 0));
        Assert.True(CardGestures.Travelled(0, -PressGesture.DragThreshold));

        var track = new TrackPress();
        track.Press(10, 10);
        Assert.Equal(TrackStep.Wait, track.Move(10 + PressGesture.DragThreshold - 0.5, 10));
        Assert.Equal(TrackStep.Wait, track.Move(10 + PressGesture.DragThreshold, 10));
        Assert.Equal(TrackStep.Follow, track.Move(10 + PressGesture.DragThreshold + 1, 10));
    }

    [Fact]
    public void S13060_a_click_on_a_track_jumps_on_the_release_where_it_was_pressed()
    {
        var track = new TrackPress();
        track.Press(40, 6);
        Assert.True(track.Pending);
        Assert.Equal(TrackStep.Wait, track.Move(41, 6));
        Assert.Equal(TrackStep.Click, track.Release());
        Assert.Equal((40d, 6d), track.At);
        Assert.False(track.Pending);
    }

    [Fact]
    public void S13060_a_track_press_the_card_took_for_its_move_does_nothing()
    {
        var track = new TrackPress();
        track.Press(40, 6);
        Assert.Equal(TrackStep.Wait, track.Move(48, 6));   // the card hears this move after the track and takes the pointer
        track.Lost();
        Assert.False(track.Pending);
        Assert.Equal(TrackStep.Wait, track.Release());
        Assert.Equal(TrackStep.Wait, track.Move(80, 6));
    }

    [Fact]
    public void S13060_a_track_press_nothing_else_took_follows_the_pointer_as_a_slider_does()
    {
        // Lock widgets on, or a preview: the card does not move, so the slider's track drag is the slider's as ever.
        var track = new TrackPress();
        track.Press(40, 6);
        Assert.Equal(TrackStep.Wait, track.Move(48, 6));
        Assert.Equal(TrackStep.Follow, track.Move(52, 6));
        Assert.True(track.Following);
        Assert.Equal(TrackStep.Follow, track.Move(51, 6));
        Assert.Equal(TrackStep.Follow, track.Release());
        Assert.False(track.Following);
    }

    [Fact]
    public void S13060_a_track_with_no_press_does_nothing()
    {
        var track = new TrackPress();
        Assert.Equal(TrackStep.Wait, track.Move(100, 100));
        Assert.Equal(TrackStep.Wait, track.Release());
    }

    [Theory]
    [InlineData(10, 0)]      // the thumb's middle at the start
    [InlineData(0, 0)]       // before it: clamped
    [InlineData(100, 50)]
    [InlineData(190, 100)]   // the thumb's middle at the end
    [InlineData(200, 100)]
    [InlineData(55.5, 25)]   // 45.5 / 180 = 25.28 %: the nearest whole step
    public void S13060_a_click_on_a_slider_track_lands_where_winui_puts_the_thumb(double offset, double value)
        => Assert.Equal(value, SliderTrack.ValueAt(offset, length: 200, thumb: 20, minimum: 0, maximum: 100, step: 1));

    [Fact]
    public void S13060_a_slider_track_click_keeps_its_steps_its_direction_and_its_ends()
    {
        Assert.Equal(30, SliderTrack.ValueAt(73, length: 200, thumb: 20, minimum: 0, maximum: 100, step: 10));
        Assert.Equal(100, SliderTrack.ValueAt(10, length: 200, thumb: 20, minimum: 0, maximum: 100, step: 1, reversed: true));
        Assert.Equal(5, SliderTrack.ValueAt(double.NaN, length: 200, thumb: 20, minimum: 5, maximum: 100, step: 1));
        Assert.Equal(5, SliderTrack.ValueAt(50, length: 200, thumb: 20, minimum: 5, maximum: 5, step: 1));
        Assert.Equal(0, SliderTrack.ValueAt(10, length: 10, thumb: 20, minimum: 0, maximum: 100, step: 1));
    }
}
