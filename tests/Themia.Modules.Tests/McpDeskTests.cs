using Themia.Core.Geometry;
using Themia.Core.Mcp;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-9970] [S-9971] The desk as the assistants' server reads it, over real widgets.</summary>
public sealed class McpDeskTests
{
    private static AppState TwoViews()
    {
        var state = new AppState();
        var home = ViewState.Create("Home");
        home.Id = "home";
        var clock = WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.1, 0.2, 0.3, 0.4));
        clock.Id = "c";
        var named = WidgetInstance.Create(new WidgetId("clock"), new FracRect(0, 0, 0.1, 0.1));
        named.Id = "n";
        named.Label = "Tokyo";
        home.Widgets.AddRange([clock, named]);
        var work = ViewState.Create("Work");
        work.Id = "work";
        state.Views.AddRange([home, work]);
        state.ActiveViewId = "home";
        return state;
    }

    [Fact]
    public void S9971_a_widget_is_its_kind_its_label_or_its_kinds_name_and_its_fractions()
    {
        var desk = McpDesk.Of(TwoViews(), []);
        var home = desk.Views[0];
        Assert.True(home.Active);
        Assert.False(desk.Views[1].Active);

        var clock = home.Widgets[0];
        Assert.Equal("clock", clock.Kind);
        Assert.Equal(ModuleCatalog.Of(new WidgetId("clock")).Label.Text(), clock.Label);
        Assert.Equal((0.1, 0.2, 0.3, 0.4), (clock.X, clock.Y, clock.W, clock.H));
        Assert.Equal("Tokyo", home.Widgets[1].Label);
    }

    [Fact]
    public void S9970_displays_are_numbered_show_their_view_or_the_active_one_and_nothing_is_repaired()
    {
        var state = TwoViews();
        state.MonitorViews["display-A"] = "work";
        state.MonitorViews["display-B"] = "deleted";
        state.DisabledMonitors.Add("display-C");

        var desk = McpDesk.Of(state,
        [
            new McpScreen("display-A", "Dell", 3840, 2160, Primary: true),
            new McpScreen("display-B", "LG", 1920, 1080, Primary: false),
            new McpScreen("display-C", "Laptop", 2560, 1600, Primary: false),
        ]);

        Assert.Equal([1, 2, 3], desk.Displays.Select(d => d.Number));
        Assert.Equal(["work", "home", "home"], desk.Displays.Select(d => d.ViewId));
        Assert.Equal([true, true, false], desk.Displays.Select(d => d.Enabled));

        // Read only: the dangling assignment is still there for the app's own repair.
        Assert.Equal("deleted", state.MonitorViews["display-B"]);
    }
}
