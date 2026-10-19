using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-6620]..[S-6629] Item selection, the desktop's: the one rule set (<see cref="ItemSelection"/>),
/// the rubber band (<see cref="SelectionBand"/>) and the look (<see cref="SelectionLook"/>) every
/// item-selecting widget (the folder, the shortcuts) draws through.
/// </summary>
public sealed class ItemSelectionTests
{
    private static ItemSelection Selection(int count, StringComparer? comparer = null)
    {
        var selection = new ItemSelection(comparer);
        selection.SetOrder([.. Enumerable.Range(0, count).Select(Id)]);
        return selection;
    }

    private static string Id(int i) => $@"C:\Root\{i}";

    // ---- clicks ([S-6620]) ----------------------------------------------------------------------

    [Fact]
    public void S6620_click_ctrl_and_shift_select_as_the_desktop()
    {
        var s = Selection(7);

        s.Click(1, ctrl: false, shift: false);
        Assert.Equal([Id(1)], s.Selected);

        s.Click(4, ctrl: false, shift: true);
        Assert.Equal([Id(1), Id(2), Id(3), Id(4)], s.Selected);

        s.Click(2, ctrl: true, shift: false);
        Assert.Equal([Id(1), Id(3), Id(4)], s.Selected);

        // Ctrl+Shift adds the range from the anchor (the Ctrl+clicked item) to what is selected.
        s.Click(6, ctrl: true, shift: true);
        Assert.Equal([Id(1), Id(2), Id(3), Id(4), Id(5), Id(6)], s.Selected);

        s.Click(0, ctrl: false, shift: false);
        Assert.Equal([Id(0)], s.Selected);
        Assert.Equal(Id(0), s.Focus);
    }

    [Fact]
    public void S6620_shift_from_no_anchor_selects_just_the_item()
    {
        var s = Selection(4);
        s.Click(2, ctrl: false, shift: true);

        Assert.Equal([Id(2)], s.Selected);
        Assert.Equal(Id(2), s.Anchor);
    }

    [Fact]
    public void S6620_clear_drops_selection_focus_and_cue_and_says_whether_there_was_any()
    {
        var s = Selection(3);
        s.Move(SelectionMove.Down, 1, 5);

        Assert.True(s.Clear());
        Assert.True(s.IsEmpty);
        Assert.Null(s.Focus);
        Assert.False(s.Cues);
        Assert.False(s.Clear());
    }

    [Fact]
    public void S6620_a_redraw_keeps_the_selection_of_items_still_there_by_id()
    {
        var s = Selection(4, StringComparer.OrdinalIgnoreCase);
        s.Click(1, false, false);
        s.Click(3, ctrl: true, shift: false);

        s.SetOrder([Id(0), Id(1).ToUpperInvariant(), Id(2)]);

        Assert.Equal([Id(1).ToUpperInvariant()], s.Selected);
        Assert.Null(s.Focus);
    }

    [Fact]
    public void S6620_out_of_range_clicks_change_nothing()
    {
        var s = Selection(2);
        s.Click(0, false, false);

        s.Click(5, false, false);
        s.Click(-1, true, true);

        Assert.Equal([Id(0)], s.Selected);
    }

    // ---- a press on the selection ([S-6621]) ---------------------------------------------------

    [Fact]
    public void S6621_a_plain_press_on_a_selected_item_keeps_the_selection_until_the_release()
    {
        var s = Selection(5);
        s.Click(0, false, false);
        s.Click(2, ctrl: false, shift: true);

        s.Press(1, ctrl: false, shift: false);
        Assert.Equal(3, s.Count);
        Assert.Equal(Id(1), s.Focus);

        s.Release(1);
        Assert.Equal([Id(1)], s.Selected);
    }

    [Fact]
    public void S6621_a_drag_from_the_selection_carries_all_of_it()
    {
        var s = Selection(5);
        s.Click(0, false, false);
        s.Click(2, ctrl: false, shift: true);

        s.Press(1, ctrl: false, shift: false);
        s.CancelPress();
        s.Release(1);

        Assert.Equal([Id(0), Id(1), Id(2)], s.Selected);
    }

    [Fact]
    public void S6621_a_press_on_an_unselected_item_or_with_a_modifier_acts_at_once()
    {
        var s = Selection(5);
        s.Click(0, false, false);

        s.Press(3, ctrl: false, shift: false);
        Assert.Equal([Id(3)], s.Selected);

        s.Press(3, ctrl: true, shift: false);
        Assert.True(s.IsEmpty);

        // A release on another item than the press does not narrow.
        s.Click(1, false, false);
        s.Click(2, ctrl: true, shift: false);
        s.Press(1, false, false);
        s.Release(2);
        Assert.Equal([Id(1), Id(2)], s.Selected);
    }

    // ---- the right press ([S-6622]) -------------------------------------------------------------

    [Fact]
    public void S6622_right_click_keeps_a_selection_it_lands_in_and_replaces_one_it_misses()
    {
        var s = Selection(5);
        s.Click(0, false, false);
        s.Click(2, false, true);

        s.Context(1);
        Assert.Equal(3, s.Count);
        Assert.Equal(Id(1), s.Focus);

        s.Context(4);
        Assert.Equal([Id(4)], s.Selected);
    }

    // ---- keys ([S-6623]) ------------------------------------------------------------------------

    [Fact]
    public void S6623_grid_keys_jump_by_columns_and_clamp_at_the_edges()
    {
        var s = Selection(10);

        Assert.True(s.Move(SelectionMove.Down, columns: 4, pageRows: 2));
        Assert.Equal(0, s.FocusIndex); // the first key lands on the first item

        s.Move(SelectionMove.Down, 4, 2);
        Assert.Equal(4, s.FocusIndex);

        s.Move(SelectionMove.Right, 4, 2);
        Assert.Equal(5, s.FocusIndex);

        // Down from the second row to a third row that only reaches index 9: the last item.
        s.Move(SelectionMove.Down, 4, 2);
        Assert.Equal(9, s.FocusIndex);

        s.Move(SelectionMove.Down, 4, 2);
        Assert.Equal(9, s.FocusIndex);

        s.Move(SelectionMove.PageUp, 4, 2);
        Assert.Equal(1, s.FocusIndex);

        s.Move(SelectionMove.Up, 4, 2);
        Assert.Equal(1, s.FocusIndex);
        Assert.Equal([Id(1)], s.Selected);
    }

    [Fact]
    public void S6623_end_with_nothing_focused_lands_on_the_last_item()
    {
        var s = Selection(4);
        s.Move(SelectionMove.End, 1, 5);

        Assert.Equal(3, s.FocusIndex);
        Assert.False(Selection(0).Move(SelectionMove.Down, 1, 5));
    }

    [Fact]
    public void S6623_left_and_right_do_nothing_in_a_one_column_list()
    {
        var s = Selection(3);
        s.Click(1, false, false);

        s.Move(SelectionMove.Right, columns: 1, pageRows: 5);

        Assert.Equal(1, s.FocusIndex);
    }

    [Fact]
    public void S6623_shift_keys_extend_from_the_anchor()
    {
        var s = Selection(6);
        s.Click(2, false, false);

        s.Move(SelectionMove.Down, 1, 5, extend: true);
        s.Move(SelectionMove.Down, 1, 5, extend: true);
        Assert.Equal([Id(2), Id(3), Id(4)], s.Selected);

        s.Move(SelectionMove.Home, 1, 5, extend: true);
        Assert.Equal([Id(0), Id(1), Id(2)], s.Selected);
    }

    [Fact]
    public void S6623_ctrl_arrows_move_the_focus_and_ctrl_space_toggles_it_in()
    {
        var s = Selection(6);
        s.Click(0, false, false);

        Assert.True(s.Key(WidgetKey.FocusDown, shift: false, columns: 1, pageRows: 5));
        Assert.True(s.Key(WidgetKey.FocusDown, shift: false, columns: 1, pageRows: 5));
        Assert.Equal(2, s.FocusIndex);
        Assert.Equal([Id(0)], s.Selected);

        Assert.True(s.Key(WidgetKey.ToggleSelection, shift: false, columns: 1, pageRows: 5));
        Assert.Equal([Id(0), Id(2)], s.Selected);

        // Space toggles the focused item the same way.
        Assert.True(s.Key(WidgetKey.Space, shift: false, columns: 1, pageRows: 5));
        Assert.Equal([Id(0)], s.Selected);
    }

    [Fact]
    public void S6623_the_key_map_extends_with_shift_held_and_leaves_the_widgets_own_keys()
    {
        var s = Selection(6);
        s.Click(1, false, false);

        Assert.True(s.Key(WidgetKey.Down, shift: true, columns: 1, pageRows: 5));
        Assert.Equal([Id(1), Id(2)], s.Selected);

        Assert.True(s.Key(WidgetKey.SelectAll, shift: false, columns: 1, pageRows: 5));
        Assert.Equal(6, s.Count);

        foreach (var own in new[] { WidgetKey.Enter, WidgetKey.Delete, WidgetKey.Rename, WidgetKey.Escape, WidgetKey.Menu, WidgetKey.Deactivate })
        {
            Assert.False(s.Key(own, shift: false, columns: 1, pageRows: 5));
        }

        Assert.False(new ItemSelection().Key(WidgetKey.Space, false, 1, 5)); // nothing focused
        Assert.False(new ItemSelection().Key(WidgetKey.SelectAll, false, 1, 5)); // nothing shown
    }

    [Fact]
    public void S6623_type_ahead_selects_the_match_alone_with_the_focus_shown()
    {
        var s = Selection(4);
        s.Click(0, false, false);
        s.Click(1, ctrl: true, shift: false);

        s.Type(3);

        Assert.Equal([Id(3)], s.Selected);
        Assert.True(s.Cues);
    }

    [Theory]
    [InlineData(0x26, WidgetKey.FocusUp)]
    [InlineData(0x28, WidgetKey.FocusDown)]
    [InlineData(0x25, WidgetKey.FocusLeft)]
    [InlineData(0x27, WidgetKey.FocusRight)]
    [InlineData(0x20, WidgetKey.ToggleSelection)]
    public void S6623_ctrl_arrows_and_ctrl_space_reach_a_widget(int vk, WidgetKey expected)
        => Assert.Equal(expected, WidgetKeys.FromVirtualKey(vk, shift: false, ctrl: true, alt: false));

    [Fact]
    public void S6623_ctrl_shift_and_ctrl_alt_arrows_still_do_not()
    {
        Assert.Null(WidgetKeys.FromVirtualKey(0x28, shift: true, ctrl: true, alt: false));
        Assert.Null(WidgetKeys.FromVirtualKey(0x28, shift: false, ctrl: true, alt: true));
    }

    // ---- the band: what it covers ([S-6624]) ----------------------------------------------------

    [Fact]
    public void S6624_a_band_over_rows_covers_every_row_it_touches()
    {
        // Rows 32 tall: 10..70 touches rows 0, 1 and 2.
        Assert.Equal([0, 1, 2], SelectionBand.GridHits(new BandRect(400, 70, 420, 10), 10, 1, 600, 32));
    }

    [Fact]
    public void S6624_rows_not_built_yet_are_covered_too()
        => Assert.Equal([.. Enumerable.Range(30, 11)], SelectionBand.GridHits(new BandRect(5, 30 * 32 + 1, 50, 40 * 32 + 2), 1000, 1, 600, 32));

    [Fact]
    public void S6624_a_band_over_tiles_covers_only_the_cells_it_overlaps()
    {
        // 4 columns of 100 × 120: a band from (150, 50) to (250, 130) touches cells 1, 2 (row 0) and 5, 6 (row 1).
        Assert.Equal([1, 2, 5, 6], SelectionBand.GridHits(new BandRect(150, 50, 250, 130), 10, 4, 100, 120));
    }

    [Fact]
    public void S6624_the_last_partial_row_and_an_empty_list_are_safe()
    {
        Assert.Equal([8, 9], SelectionBand.GridHits(new BandRect(0, 250, 400, 500), 10, 4, 100, 120));
        Assert.Empty(SelectionBand.GridHits(new BandRect(0, 0, 100, 100), 0, 1, 100, 32));
        Assert.Empty(SelectionBand.GridHits(new BandRect(0, 0, 100, 100), 5, 1, 100, 0));
    }

    [Fact]
    public void S6624_boxes_count_when_overlapped_at_all_and_an_empty_box_never()
    {
        ItemBox[] boxes = [new(0, 0, 80, 60), new(90, 0, 80, 60), new(0, 70, 80, 60), new(200, 200, 0, 0)];

        Assert.Equal([0, 1], SelectionBand.Hits(new BandRect(70, 10, 95, 20), boxes));
        Assert.Empty(SelectionBand.Hits(new BandRect(81, 61, 89, 69), boxes)); // the gaps between them
        Assert.Empty(SelectionBand.Hits(new BandRect(150, 150, 260, 260), boxes[3..]));
    }

    [Fact]
    public void S6624_the_band_is_kept_inside_the_items_area()
        => Assert.Equal(new BandRect(10, 20, 0, 500), SelectionBand.Clamp(new BandRect(10, 20, -40, 900), 300, 500));

    [Fact]
    public void S6624_a_press_is_a_click_inside_the_dead_zone()
    {
        Assert.False(SelectionBand.PastThreshold(3, 3));
        Assert.True(SelectionBand.PastThreshold(5, 0));
    }

    // ---- the band: how it combines ([S-6625]) -------------------------------------------------

    [Theory]
    [InlineData(false, false, BandMode.Replace)]
    [InlineData(false, true, BandMode.Add)]
    [InlineData(true, false, BandMode.Toggle)]
    [InlineData(true, true, BandMode.Toggle)]
    public void S6625_modifiers_pick_the_mode(bool ctrl, bool shift, BandMode mode)
        => Assert.Equal(mode, SelectionBand.Mode(ctrl, shift));

    [Fact]
    public void S6625_a_plain_band_replaces_the_selection_live()
    {
        var s = Selection(6);
        s.Click(0, false, false);

        s.BeginBand(BandMode.Replace);
        s.Band([2, 3, 4]);
        Assert.Equal([Id(2), Id(3), Id(4)], s.Selected);

        // Shrinking the band gives items back as it goes.
        s.Band([2]);
        Assert.Equal([Id(2)], s.Selected);
        s.EndBand();
        Assert.Equal([Id(2)], s.Selected);
    }

    [Fact]
    public void S6625_shift_adds_the_band_to_the_earlier_selection()
    {
        var s = Selection(6);
        s.Click(0, false, false);
        s.Click(2, ctrl: true, shift: false);

        s.BeginBand(BandMode.Add);
        s.Band([1, 2]);

        Assert.Equal([Id(0), Id(1), Id(2)], s.Selected);
    }

    [Fact]
    public void S6625_ctrl_toggles_each_covered_item_against_the_earlier_selection()
    {
        var s = Selection(6);
        s.Click(0, false, false);
        s.Click(2, ctrl: true, shift: false);

        s.BeginBand(BandMode.Toggle);
        s.Band([1, 2]);
        Assert.Equal([Id(0), Id(1)], s.Selected);

        // Moving the band off an item puts it back the way it was.
        s.Band([1]);
        Assert.Equal([Id(0), Id(1), Id(2)], s.Selected);
        Assert.Equal(Id(1), s.Focus);
    }

    [Fact]
    public void S6625_a_band_hides_the_focus_cue()
    {
        var s = Selection(3);
        s.Move(SelectionMove.Down, 1, 5);

        s.BeginBand(BandMode.Replace);

        Assert.False(s.Cues);
    }

    // ---- going elsewhere ([S-6626]) -------------------------------------------------------------

    [Fact]
    public void S6626_deactivating_keeps_the_selection_and_drops_the_cue_and_a_pending_press()
    {
        var s = Selection(4);
        s.Click(0, false, false);
        s.Move(SelectionMove.Down, 1, 5, extend: true);
        s.Press(1, ctrl: false, shift: false);

        s.Deactivate();
        s.Release(1);

        Assert.Equal([Id(0), Id(1)], s.Selected);
        Assert.False(s.Cues);
    }

    // ---- the look ([S-6627]) --------------------------------------------------------------------

    [Fact]
    public void S6627_rest_draws_nothing_and_hover_is_a_faint_highlight()
    {
        Assert.Equal(new SelectionInk(SelectionTone.None, 0, 0), SelectionLook.Ink(new SelectionCue(false, false)));
        Assert.Equal(new SelectionInk(SelectionTone.Highlight, SelectionLook.HoverFill, SelectionLook.HoverStroke), SelectionLook.Ink(new SelectionCue(false, true)));
    }

    [Fact]
    public void S6627_a_selection_is_stronger_than_a_hover_and_stronger_still_under_the_pointer()
    {
        var selected = SelectionLook.Ink(new SelectionCue(true, false));
        var both = SelectionLook.Ink(new SelectionCue(true, true));
        var hover = SelectionLook.Ink(new SelectionCue(false, true));

        Assert.Equal(SelectionTone.Highlight, selected.Tone);
        Assert.True(selected.Fill > hover.Fill && selected.Stroke > hover.Stroke);
        Assert.True(both.Fill > selected.Fill && both.Stroke > selected.Stroke);
        Assert.InRange(selected.Fill, 0.3, 0.4);
        Assert.InRange(selected.Stroke, 0.6, 0.7);
        Assert.InRange(hover.Fill, 0.1, 0.15);
        Assert.InRange(hover.Stroke, 0.3, 0.4);
    }

    [Fact]
    public void S6627_a_selection_without_the_keyboard_is_neutral_grey()
    {
        var inactive = SelectionLook.Ink(new SelectionCue(true, false, Active: false));

        Assert.Equal(SelectionTone.Neutral, inactive.Tone);
        Assert.Equal(SelectionLook.NeutralGrey, SelectionLook.Colour(inactive.Tone, new RgbColor(1, 2, 3)));
        Assert.Equal(new RgbColor(1, 2, 3), SelectionLook.Colour(SelectionTone.Highlight, new RgbColor(1, 2, 3)));

        // A hover over it lifts it, still grey; a hover over an unselected item in the same widget is the highlight's.
        Assert.Equal(SelectionTone.Neutral, SelectionLook.Ink(new SelectionCue(true, true, Active: false)).Tone);
        Assert.Equal(SelectionTone.Highlight, SelectionLook.Ink(new SelectionCue(false, true, Active: false)).Tone);
    }

    [Fact]
    public void S6627_a_drop_target_reads_as_the_selection_under_the_pointer()
        => Assert.Equal(SelectionLook.Ink(new SelectionCue(true, true)), SelectionLook.Ink(new SelectionCue(false, false, DropTarget: true)));

    [Fact]
    public void S6627_the_band_is_a_translucent_fill_under_a_solid_border()
    {
        Assert.Equal(SelectionTone.Highlight, SelectionLook.Band.Tone);
        Assert.InRange(SelectionLook.Band.Fill, 0.25, 0.3);
        Assert.Equal(1.0, SelectionLook.Band.Stroke);
    }

    // ---- the focus cue ([S-6628]) ---------------------------------------------------------------

    [Fact]
    public void S6628_the_focus_shows_as_a_border_only_after_keyboard_use_and_with_the_keyboard()
    {
        var focusShown = SelectionLook.Ink(new SelectionCue(false, false, Focused: true, Cues: true));
        Assert.Equal(new SelectionInk(SelectionTone.Highlight, 0, SelectionLook.FocusStroke), focusShown);

        Assert.Equal(SelectionTone.None, SelectionLook.Ink(new SelectionCue(false, false, Focused: true, Cues: false)).Tone);
        Assert.Equal(SelectionTone.None, SelectionLook.Ink(new SelectionCue(false, false, Focused: true, Cues: true, Active: false)).Tone);
    }

    [Fact]
    public void S6628_keys_show_the_cue_and_a_press_hides_it()
    {
        var s = Selection(4);

        s.Move(SelectionMove.Down, 1, 5);
        Assert.True(s.Cues);

        s.Press(2, ctrl: false, shift: false);
        Assert.False(s.Cues);

        s.Key(WidgetKey.Space, false, 1, 5);
        Assert.True(s.Cues);

        s.Context(0);
        Assert.False(s.Cues);
    }

    // ---- the full name ([S-6629]) ---------------------------------------------------------------

    [Theory]
    [InlineData(true, true, true, true, false, true)]
    [InlineData(false, true, true, true, false, false)] // not selected
    [InlineData(true, false, true, true, false, false)] // selected, but not the focused one
    [InlineData(true, true, false, true, false, false)] // without the keyboard
    [InlineData(true, true, true, false, false, false)] // the name fits
    [InlineData(true, true, true, true, true, false)] // being renamed
    public void S6629_only_the_focused_selected_cut_short_name_shows_in_full(bool selected, bool focused, bool active, bool trimmed, bool renaming, bool shows)
        => Assert.Equal(shows, SelectionLook.ShowsFullName(selected, focused, active, trimmed, renaming));
}
