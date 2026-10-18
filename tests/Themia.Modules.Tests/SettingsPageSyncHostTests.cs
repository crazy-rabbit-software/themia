using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1721] The open settings page catching up with a change, over the first-run desktop: it needs two real widgets, which are their modules', so it runs here. Moved from Core's SettingsPageSyncTests, unchanged in what it asserts.</summary>
public sealed class SettingsPageSyncHostTests
{

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
}
