using Themia.Core.Geometry;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

public class WidgetTransferTests
{
    private static readonly ScreenMetrics Screen = new(1920, 1080);

    private static (AppState State, ViewState Left, ViewState Right, WidgetInstance Widget) TwoViews()
    {
        // One clock, not the first-run desktop ([S-893]): the counts below are about that one widget.
        var state = AppState.CreateDefault();
        var left = state.ActiveView();
        left.Widgets.RemoveRange(1, left.Widgets.Count - 1);
        var right = ViewState.Create("Second");
        state.Views.Add(right);
        return (state, left, right, left.Widgets[0]);
    }

    // ---- hand-off decision ------------------------------------------------------------------

    [Fact]
    public void A_drop_on_the_same_display_never_hands_off()
    {
        Assert.False(WidgetTransfer.ShouldHandOff(droppedOnThisDisplay: true, "view-a", "view-b"));
    }

    [Fact]
    public void A_drop_on_a_display_showing_the_same_view_does_not_hand_off()
    {
        // Both displays already render that widget, so moving it would be a no-op that rewrote its
        // position against the wrong screen.
        Assert.False(WidgetTransfer.ShouldHandOff(droppedOnThisDisplay: false, "view-a", "view-a"));
    }

    [Fact]
    public void A_drop_outside_every_display_does_not_hand_off()
    {
        Assert.False(WidgetTransfer.ShouldHandOff(droppedOnThisDisplay: false, "view-a", null));
    }

    [Fact]
    public void A_drop_on_a_display_showing_another_view_hands_off()
    {
        Assert.True(WidgetTransfer.ShouldHandOff(droppedOnThisDisplay: false, "view-a", "view-b"));
    }

    // ---- moving between views: a Move placement ---------------------------------------------

    private static PastePlan MovePlan(ViewState from, string id, double x = 0.5, double y = 0.5)
        => PlacementPayload.FromView(from, [id], PlacementMode.Move)!.Plan(x, y, Screen, snapEnabled: false);

    private static PastePlan CopyPlan(ViewState from, string id, double x = 0.5, double y = 0.5)
        => PlacementPayload.FromView(from, [id], PlacementMode.Copy)!.Plan(x, y, Screen, snapEnabled: false);

    [Fact]
    public void S1590_moving_a_widget_transfers_it_without_duplicating_it()
    {
        var (state, left, right, widget) = TwoViews();

        state.ApplyPlacement(MovePlan(left, widget.Id), right.Id);

        Assert.Empty(left.Widgets);
        Assert.Single(right.Widgets);
        Assert.Same(widget, right.Widgets[0]);
    }

    [Fact]
    public void S1590_a_moved_widget_lands_as_itself_with_the_config_it_has_now()
    {
        var (state, left, right, widget) = TwoViews();
        var plan = MovePlan(left, widget.Id);

        // Changed after the snapshot was taken: the live widget is what lands.
        ((ClockConfig)widget.Config).ShowSeconds = true;
        widget.Label = "Bedside";
        state.ApplyPlacement(plan, right.Id);

        var moved = right.Widgets[0];
        Assert.Equal(widget.Id, moved.Id);
        Assert.Equal("Bedside", moved.Label);
        Assert.True(((ClockConfig)moved.Config).ShowSeconds);
    }

    [Fact]
    public void S1590_a_moved_widget_lands_at_the_planned_rect()
    {
        var (state, left, right, widget) = TwoViews();
        var plan = MovePlan(left, widget.Id, 0.3, 0.4);

        state.ApplyPlacement(plan, right.Id);

        Assert.Equal(plan.Added[0].Rect, right.Widgets[0].Rect);
    }

    [Fact]
    public void S75_moving_within_one_view_keeps_exactly_one_widget()
    {
        // The remove and the add both target the same list; doing them in the wrong order would
        // either lose the widget or leave two.
        var (state, left, _, widget) = TwoViews();

        state.ApplyPlacement(MovePlan(left, widget.Id, 0.7, 0.7), left.Id);

        Assert.Same(widget, Assert.Single(left.Widgets));
    }

    [Fact]
    public void S1590_a_move_whose_original_was_handed_elsewhere_meanwhile_still_moves_it_once()
    {
        var (state, left, right, widget) = TwoViews();
        var plan = MovePlan(left, widget.Id);

        // The widget went to the other view by some other route before this move landed.
        left.Widgets.Remove(widget);
        right.Widgets.Add(widget);
        state.ApplyPlacement(plan, left.Id);

        Assert.Empty(right.Widgets);
        Assert.Same(widget, Assert.Single(left.Widgets));
    }

    // ---- paste across views -----------------------------------------------------------------

    [Fact]
    public void Pasting_a_copy_into_another_view_leaves_the_original_alone()
    {
        var (state, left, right, widget) = TwoViews();

        state.ApplyPlacement(CopyPlan(left, widget.Id), right.Id);

        Assert.Single(left.Widgets);
        Assert.Single(right.Widgets);
        Assert.NotEqual(left.Widgets[0].Id, right.Widgets[0].Id);
    }

    [Fact]
    public void Pasting_into_a_view_that_was_deleted_falls_back_to_the_active_view()
    {
        var (state, left, right, widget) = TwoViews();
        var plan = CopyPlan(left, widget.Id);
        state.Views.Remove(right);

        state.ApplyPlacement(plan, right.Id);

        Assert.Equal(2, state.ActiveView().Widgets.Count);
    }

    [Fact]
    public void S72_a_cut_whose_source_view_vanished_still_pastes()
    {
        var (state, left, right, widget) = TwoViews();
        var plan = MovePlan(left, widget.Id);

        // The source view is gone by the time the paste happens.
        state.Views.Remove(left);
        state.ActiveViewId = right.Id;
        state.ApplyPlacement(plan, right.Id);

        Assert.Equal(widget.Id, Assert.Single(right.Widgets).Id);
    }

    [Fact]
    public void A_transferred_state_still_passes_validation()
    {
        var (state, left, right, widget) = TwoViews();
        state.ApplyPlacement(MovePlan(left, widget.Id), right.Id);

        var repairs = Themia.Core.Storage.AppStateStore.Validate(state);

        Assert.Empty(repairs);
    }
}
