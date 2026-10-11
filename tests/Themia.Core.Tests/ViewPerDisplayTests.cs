using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-18]/[S-19]/[S-1705] Which view each display shows, and how a display is given one.</summary>
public class ViewPerDisplayTests
{
    private static readonly string[] TwoDisplays = ["\\\\.\\DISPLAY1", "\\\\.\\DISPLAY2"];

    /// <summary>Two displays on two different views, as the user would set them up.</summary>
    private static AppState TwoViewsOnTwoDisplays()
    {
        var state = AppState.CreateDefault();
        var second = state.Views[1];
        state.MonitorViews[TwoDisplays[0]] = state.Views[0].Id;
        state.MonitorViews[TwoDisplays[1]] = second.Id;
        return state;
    }

    [Fact]
    public void S1705_displays_share_the_one_view_rather_than_get_new_ones()
    {
        var state = AppState.CreateDefault();
        var original = state.ActiveView();
        var views = state.Views.Count;

        DisplayPlan.AssignViews(state, TwoDisplays);

        Assert.Equal(views, state.Views.Count);
        Assert.Equal(original.Id, state.ViewForMonitor(TwoDisplays[0]).Id);
        Assert.Equal(original.Id, state.ViewForMonitor(TwoDisplays[1]).Id);
    }

    [Fact]
    public void S1705_the_seeded_widgets_are_not_duplicated_or_lost()
    {
        var state = AppState.CreateDefault();
        var seeded = state.Views.Sum(v => v.Widgets.Count);

        DisplayPlan.AssignViews(state, TwoDisplays);

        Assert.Equal(seeded, state.Views.Sum(v => v.Widgets.Count));
    }

    [Fact]
    public void S1705_running_it_again_changes_nothing()
    {
        var state = AppState.CreateDefault();
        DisplayPlan.AssignViews(state, TwoDisplays);
        var before = state.MonitorViews.ToDictionary(kv => kv.Key, kv => kv.Value);

        var assigned = DisplayPlan.AssignViews(state, TwoDisplays);

        Assert.Equal(0, assigned);
        Assert.Equal(before, state.MonitorViews);
    }

    [Fact]
    public void S1705_an_existing_assignment_is_never_taken_from_its_display()
    {
        var state = AppState.CreateDefault();
        var chosen = ViewState.Create("Work");
        state.Views.Add(chosen);
        state.MonitorViews[TwoDisplays[1]] = chosen.Id;

        DisplayPlan.AssignViews(state, TwoDisplays);

        Assert.Equal(chosen.Id, state.ViewForMonitor(TwoDisplays[1]).Id);
        Assert.Equal(state.Views[0].Id, state.ViewForMonitor(TwoDisplays[0]).Id);
    }

    [Fact]
    public void S1705_adding_a_third_display_leaves_the_first_two_alone()
    {
        var state = TwoViewsOnTwoDisplays();
        var before = state.MonitorViews.ToDictionary(kv => kv.Key, kv => kv.Value);

        string[] three = [.. TwoDisplays, "\\\\.\\DISPLAY3"];
        var assigned = DisplayPlan.AssignViews(state, three);

        Assert.Equal(1, assigned);
        Assert.Equal(2, state.Views.Count);
        foreach (var (display, viewId) in before)
        {
            Assert.Equal(viewId, state.MonitorViews[display]);
        }
    }

    [Fact]
    public void S1705_no_displays_is_a_no_op_rather_than_a_reset()
    {
        var state = AppState.CreateDefault();

        var assigned = DisplayPlan.AssignViews(state, []);

        Assert.Equal(0, assigned);
        Assert.Empty(state.MonitorViews);
    }

    [Fact]
    public void S1705_the_result_still_passes_validation()
    {
        var state = AppState.CreateDefault();
        DisplayPlan.AssignViews(state, TwoDisplays);

        Assert.Empty(Themia.Core.Storage.AppStateStore.Validate(state));
    }

    /// <summary>
    /// The bug: the View menu switched the active view, and every display already had an
    /// assignment of its own, which wins. Pinned here so the reason the fix exists stays visible.
    /// </summary>
    [Fact]
    public void S19_SwitchingOnlyTheActiveViewChangesNoAssignedDisplay()
    {
        var state = TwoViewsOnTwoDisplays();
        var before = state.ViewForMonitor(TwoDisplays[0]).Id;
        var other = state.ViewForMonitor(TwoDisplays[1]).Id;

        state.ActiveViewId = other;

        Assert.Equal(before, state.ViewForMonitor(TwoDisplays[0]).Id);
    }

    [Fact]
    public void S19_ShowingAViewOnADisplayPutsItThere()
    {
        var state = TwoViewsOnTwoDisplays();
        var other = state.ViewForMonitor(TwoDisplays[1]).Id;

        Assert.True(state.ShowViewOn(TwoDisplays[0], other));

        Assert.Equal(other, state.ViewForMonitor(TwoDisplays[0]).Id);
    }

    [Fact]
    public void S19_ShowingAViewOnOneDisplayLeavesTheOtherAlone()
    {
        var state = TwoViewsOnTwoDisplays();
        var second = state.ViewForMonitor(TwoDisplays[1]).Id;
        var third = ViewState.Create("Third");
        state.Views.Add(third);

        state.ShowViewOn(TwoDisplays[0], third.Id);

        Assert.Equal(second, state.ViewForMonitor(TwoDisplays[1]).Id);
    }

    /// <summary>A display plugged in later, with no assignment yet, starts on the last view chosen.</summary>
    [Fact]
    public void S19_AnUnassignedDisplayFollowsTheLastViewShown()
    {
        var state = TwoViewsOnTwoDisplays();
        var other = state.ViewForMonitor(TwoDisplays[1]).Id;

        state.ShowViewOn(TwoDisplays[0], other);
        DisplayPlan.AssignViews(state, ["\\\\.\\DISPLAY3"]);

        Assert.Equal(other, state.ViewForMonitor("\\\\.\\DISPLAY3").Id);
    }

    [Fact]
    public void S19_AViewThatDoesNotExistChangesNothing()
    {
        var state = TwoViewsOnTwoDisplays();
        var before = state.ViewForMonitor(TwoDisplays[0]).Id;
        var activeBefore = state.ActiveViewId;

        Assert.False(state.ShowViewOn(TwoDisplays[0], "no-such-view"));

        Assert.Equal(before, state.ViewForMonitor(TwoDisplays[0]).Id);
        Assert.Equal(activeBefore, state.ActiveViewId);
    }
}
