using Themia.Core.Model;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1720] / [S-1721] The open settings page catching up with changes made elsewhere.</summary>
public sealed class SettingsPageSyncTests
{
    [Fact]
    public void S1720_snap_to_grid_switched_on_the_desktop_rebuilds_the_open_app_page()
    {
        // The reported bug: the edit toolbar's snap switch changed the state, the Appearance page
        // kept showing the old value. The page shows the state it was built from; the change came
        // while the settings window was in the background.
        var state = AppState.CreateDefault();
        var shown = SettingsPageSync.Signature(state, SettingsScope.App, null);

        state.Design.GridSnap = !state.Design.GridSnap;
        var now = SettingsPageSync.Signature(state, SettingsScope.App, null);

        Assert.True(SettingsPageSync.NeedsRebuild(windowActive: false, shown, now));
    }

    [Fact]
    public void S1720_a_change_made_in_the_active_settings_window_is_never_rebuilt_under_the_user()
    {
        var state = AppState.CreateDefault();
        var shown = SettingsPageSync.Signature(state, SettingsScope.App, null);

        state.Design.WidgetGapPx = 20;
        var now = SettingsPageSync.Signature(state, SettingsScope.App, null);

        Assert.False(SettingsPageSync.NeedsRebuild(windowActive: true, shown, now));
    }

    [Fact]
    public void S1720_nothing_to_catch_up_when_the_page_already_shows_the_state_or_there_is_no_page()
    {
        var signature = SettingsPageSync.Signature(AppState.CreateDefault(), SettingsScope.App, null);

        Assert.False(SettingsPageSync.NeedsRebuild(windowActive: false, signature, signature));
        Assert.False(SettingsPageSync.NeedsRebuild(windowActive: false, shown: null, signature));
    }

    [Fact]
    public void S1721_the_app_pages_follow_the_design_and_preferences_not_where_widgets_sit()
    {
        var state = AppState.CreateDefault();
        var before = SettingsPageSync.Signature(state, SettingsScope.App, null);

        var widget = state.Views[0].Widgets[0];
        widget.X += 0.1;
        widget.W += 0.05;
        Assert.Equal(before, SettingsPageSync.Signature(state, SettingsScope.App, null));

        state.Preferences.GameModeSuspend = !state.Preferences.GameModeSuspend;
        Assert.NotEqual(before, SettingsPageSync.Signature(state, SettingsScope.App, null));
    }

    [Fact]
    public void S1721_a_widget_page_follows_its_own_widget_only()
    {
        var state = AppState.CreateDefault();
        var widgets = state.Views[0].Widgets;
        Assert.True(widgets.Count >= 2);

        var own = widgets[0];
        var other = widgets[1];
        var before = SettingsPageSync.Signature(state, SettingsScope.Widget, own.Id);

        other.Label = "renamed";
        own.X += 0.1;
        Assert.Equal(before, SettingsPageSync.Signature(state, SettingsScope.Widget, own.Id));

        own.Label = "mine";
        Assert.NotEqual(before, SettingsPageSync.Signature(state, SettingsScope.Widget, own.Id));
    }

    [Fact]
    public void S1721_the_displays_page_follows_the_connected_displays_only()
    {
        var state = AppState.CreateDefault();
        string[] connected = ["A"];
        var before = SettingsPageSync.Signature(state, SettingsScope.Displays, displays: connected);

        state.MonitorScales["B"] = 1.5;
        Assert.Equal(before, SettingsPageSync.Signature(state, SettingsScope.Displays, displays: connected));

        state.DisabledMonitors.Add("A");
        Assert.NotEqual(before, SettingsPageSync.Signature(state, SettingsScope.Displays, displays: connected));

        // A display plugged in is a new group on the page.
        Assert.NotEqual(
            SettingsPageSync.Signature(state, SettingsScope.Displays, displays: connected),
            SettingsPageSync.Signature(state, SettingsScope.Displays, displays: ["A", "B"]));
    }

    [Fact]
    public void S1721_the_views_page_follows_every_views_name_but_not_widgets_moving()
    {
        var state = AppState.CreateDefault();
        var view = state.Views[0];
        var before = SettingsPageSync.Signature(state, SettingsScope.Views);

        view.Widgets[0].Y += 0.1;
        Assert.Equal(before, SettingsPageSync.Signature(state, SettingsScope.Views));

        view.Name = "Work";
        Assert.NotEqual(before, SettingsPageSync.Signature(state, SettingsScope.Views));
    }
}
