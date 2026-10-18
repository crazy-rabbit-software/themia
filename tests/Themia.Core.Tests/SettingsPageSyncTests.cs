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

}
