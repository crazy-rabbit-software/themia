using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Tests;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1721] The open settings page catching up with a change, over the first-run desktop: it needs two real widgets, which are their modules', so it runs here. Moved from Core's SettingsPageSyncTests, unchanged in what it asserts.</summary>
public sealed class SettingsPageSyncHostTests
{
    [Fact]
    public void S1721_the_app_pages_follow_the_design_and_preferences_not_where_widgets_sit()
    {
        var state = AppState.CreateDefault();
        var before = SettingsPageSync.Signature(state);

        var widget = state.Views[0].Widgets[0];
        widget.X += 0.1;
        widget.W += 0.05;
        Assert.Equal(before, SettingsPageSync.Signature(state));

        state.Preferences.GameModeSuspend = !state.Preferences.GameModeSuspend;
        Assert.NotEqual(before, SettingsPageSync.Signature(state));
    }
}
