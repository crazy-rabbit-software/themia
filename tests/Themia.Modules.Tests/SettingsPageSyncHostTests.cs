using Themia.Core.Model;
using Themia.Core.Settings;
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
}
