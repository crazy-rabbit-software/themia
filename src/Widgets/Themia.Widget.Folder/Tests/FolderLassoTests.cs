using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-6412]..[S-6415] The folder widget's rubber band. Moved with the widget from Core, unchanged.</summary>
public sealed class FolderLassoTests
{
    private static FolderSelection Selection(int count)
    {
        var selection = new FolderSelection();
        selection.SetOrder([.. Enumerable.Range(0, count).Select(i => $@"C:\Root\{i}")]);
        return selection;
    }

    private static string P(int i) => $@"C:\Root\{i}";

    // ---- where a band starts ([S-6412]) ----------------------------------------------------------

    [Fact]
    public void S6412_a_press_on_an_items_icon_or_name_is_an_item_press()
        => Assert.Equal(FolderPress.Item, FolderLasso.Classify(onItem: true, onIconOrName: true));

    [Fact]
    public void S6412_a_press_beside_the_name_or_on_the_background_can_start_a_band()
    {
        Assert.Equal(FolderPress.ItemBlank, FolderLasso.Classify(onItem: true, onIconOrName: false));
        Assert.Equal(FolderPress.Background, FolderLasso.Classify(onItem: false, onIconOrName: false));
    }

    [Fact]
    public void S6412_a_drag_from_a_rows_empty_part_draws_a_band_not_a_file_drag()
    {
        // The bug: every press inside a row was an item press, and rows fill the list's width, so a
        // full folder had nowhere a band could start.
        var g = new FolderGesture();
        g.BeginPress(300, 10, FolderPress.ItemBlank, [@"C:\a"]);

        Assert.Equal(GestureStep.Band, g.Move(300, 60));
        Assert.Equal(new BandRect(300, 10, 300, 60), g.BandRect);
        Assert.Equal((false, true), g.Release());
    }

    [Fact]
    public void S6412_a_click_on_a_rows_empty_part_is_still_a_click()
    {
        var g = new FolderGesture();
        g.BeginPress(300, 10, FolderPress.ItemBlank);

        Assert.Equal(GestureStep.Idle, g.Move(302, 12));
        Assert.Equal((false, false), g.Release());
    }

    // ---- how it combines ([S-6413]) ------------------------------------------------------------

    [Theory]
    [InlineData(false, false, BandMode.Replace)]
    [InlineData(false, true, BandMode.Add)]
    [InlineData(true, false, BandMode.Toggle)]
    [InlineData(true, true, BandMode.Toggle)]
    public void S6413_modifiers_pick_the_mode(bool ctrl, bool shift, BandMode mode)
        => Assert.Equal(mode, FolderLasso.Mode(ctrl, shift));

    [Fact]
    public void S6413_a_plain_band_replaces_the_selection_live()
    {
        var s = Selection(6);
        s.Click(0, false, false);

        s.Band([2, 3, 4], [P(0)], BandMode.Replace);
        Assert.Equal([P(2), P(3), P(4)], s.Selected);

        // Shrinking the band gives items back as it goes.
        s.Band([2], [P(0)], BandMode.Replace);
        Assert.Equal([P(2)], s.Selected);
    }

    [Fact]
    public void S6413_shift_adds_the_band_to_the_earlier_selection()
    {
        var s = Selection(6);
        s.Band([1, 2], [P(0), P(2)], BandMode.Add);

        Assert.Equal([P(0), P(1), P(2)], s.Selected);
    }

    [Fact]
    public void S6413_ctrl_toggles_each_covered_item_against_the_earlier_selection()
    {
        var s = Selection(6);
        s.Band([1, 2], [P(0), P(2)], BandMode.Toggle);
        Assert.Equal([P(0), P(1)], s.Selected);

        // Moving the band off an item puts it back the way it was.
        s.Band([1], [P(0), P(2)], BandMode.Toggle);
        Assert.Equal([P(0), P(1), P(2)], s.Selected);
        Assert.Equal(P(1), s.Focus);
    }

    // ---- what it covers ([S-6414]) -------------------------------------------------------------

    [Fact]
    public void S6414_a_band_over_rows_covers_every_row_it_touches()
    {
        // Rows 32 tall: 10..70 touches rows 0, 1 and 2.
        Assert.Equal([0, 1, 2], FolderLasso.Hits(new BandRect(400, 70, 420, 10), 10, 1, 600, 32));
    }

    [Fact]
    public void S6414_rows_not_built_yet_are_covered_too()
        => Assert.Equal([.. Enumerable.Range(30, 11)], FolderLasso.Hits(new BandRect(5, 30 * 32 + 1, 50, 40 * 32 + 2), 1000, 1, 600, 32));

    [Fact]
    public void S6414_a_band_over_tiles_covers_only_the_cells_it_overlaps()
    {
        // 4 columns of 100 × 120: a band from (150, 50) to (250, 130) touches cells 1, 2 (row 0) and 5, 6 (row 1).
        Assert.Equal([1, 2, 5, 6], FolderLasso.Hits(new BandRect(150, 50, 250, 130), 10, 4, 100, 120));
    }

    [Fact]
    public void S6414_the_last_partial_row_and_an_empty_list_are_safe()
    {
        Assert.Equal([8, 9], FolderLasso.Hits(new BandRect(0, 250, 400, 500), 10, 4, 100, 120));
        Assert.Empty(FolderLasso.Hits(new BandRect(0, 0, 100, 100), 0, 1, 100, 32));
        Assert.Empty(FolderLasso.Hits(new BandRect(0, 0, 100, 100), 5, 1, 100, 0));
    }

    [Fact]
    public void S6414_the_band_is_kept_inside_the_content()
    {
        var band = FolderLasso.Clamp(new BandRect(10, 20, -40, 900), 300, 500);

        Assert.Equal(new BandRect(10, 20, 0, 500), band);
    }

    // ---- auto-scroll ([S-6415]) ----------------------------------------------------------------

    [Fact]
    public void S6415_the_middle_of_the_list_never_scrolls()
    {
        Assert.Equal(0, FolderLasso.ScrollVelocity(200, 400));
        Assert.Equal(0, FolderLasso.ScrollVelocity(FolderLasso.EdgeZone, 400));
    }

    [Fact]
    public void S6415_past_an_edge_it_scrolls_that_way_faster_the_further_out_capped()
    {
        var near = FolderLasso.ScrollVelocity(400 - 10, 400);
        var past = FolderLasso.ScrollVelocity(400 + 40, 400);
        var far = FolderLasso.ScrollVelocity(400 + 5000, 400);

        Assert.True(near > 0 && past > near);
        Assert.Equal(FolderLasso.MaxSpeed, far);
        Assert.True(FolderLasso.ScrollVelocity(-30, 400) < FolderLasso.ScrollVelocity(5, 400));
        Assert.True(FolderLasso.ScrollVelocity(5, 400) < 0);
    }

    [Fact]
    public void S6415_a_short_list_keeps_a_middle_that_does_not_scroll()
        => Assert.Equal(0, FolderLasso.ScrollVelocity(20, 40));

    [Fact]
    public void S6415_a_step_stays_in_range_and_a_stalled_frame_moves_at_most_a_tenth_of_a_second()
    {
        Assert.Equal(0, FolderLasso.Step(10, -1000, 0.05, 500));
        Assert.Equal(500, FolderLasso.Step(490, 1000, 0.05, 500));
        Assert.Equal(100 + (1000 * FolderLasso.MaxStepSeconds), FolderLasso.Step(100, 1000, 3, 500));
        Assert.Equal(100, FolderLasso.Step(100, 1000, double.NaN, 500));
    }
}
