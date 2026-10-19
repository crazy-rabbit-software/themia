using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Folder.Tests;

/// <summary>[S-6412]/[S-6415] The folder widget's own part of the rubber band; how it combines and what it covers are <c>ItemSelectionTests</c>' ([S-6624]/[S-6625]).</summary>
public sealed class FolderLassoTests
{
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
