using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11751] [S-11753] The page of several: a shared setting's lines are the very lines one holder's module builds (the
/// real weather and sun place lines here), over a scratch copy, and every write they make is set on every holder.
/// </summary>
public sealed class SharedRowLinesTests
{
    private static readonly WidgetId Weather = new("weatherCondition");
    private static readonly WidgetId Sun = new("sun");
    private static readonly WidgetId Clock = new("clock");

    private static WidgetConfig Placed(WidgetId kind, string name, double latitude)
    {
        var config = WidgetConfig.CreateDefault(kind);
        var placed = (IPlaced)config;
        placed.UseDeviceLocation = false;
        placed.Latitude = latitude;
        placed.Longitude = 8.5;
        placed.PlaceName = name;
        return config;
    }

    private static WidgetMenuLines? Build(WidgetId kind, WidgetConfig config, Action save)
    {
        var menu = new WidgetMenuLines(save);
        ModuleRegistry.Widgets.Single(m => m.Descriptor.Id == kind).MenuLines(menu, config);
        return menu;
    }

    [Fact]
    public void S11751_the_first_holder_builds_the_lines_and_a_kind_without_the_setting_is_passed_over()
    {
        var lines = SharedRowLines.Of(SharedSettings.Place, false, [(Clock, WidgetConfig.CreateDefault(Clock)), (Weather, Placed(Weather, "Zurich", 47.4))], Build, _ => { });

        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.Equal(SharedSettings.Place.Key, l.Shares));
    }

    [Fact]
    public void S11751_a_write_on_the_scratch_copy_is_spread_to_every_holder_and_the_holders_are_not_touched()
    {
        var weather = Placed(Weather, "Zurich", 47.4);
        var sun = Placed(Sun, "Zurich", 47.4);
        var spread = new List<WidgetConfig>();

        var lines = SharedRowLines.Of(SharedSettings.Place, false, [(Weather, weather), (Sun, sun)], Build, spread.Add);

        // The place's line: its search, and Clear location under what it finds.
        var clear = lines.SelectMany(l => l.Search?.Fixed ?? []).First(c => c.Action is not null);
        clear.Action!();

        // The line's own save and the wiring after it both spread the one scratch copy (a pick of what it already holds
        // still makes the holders agree).
        Assert.NotEmpty(spread);
        var written = spread[0];
        Assert.All(spread, c => Assert.Same(written, c));
        Assert.NotSame(weather, written);
        Assert.False(((IPlaced)written).HasLocation);
        Assert.True(((IPlaced)weather).HasLocation);
        Assert.True(((IPlaced)sun).HasLocation);
    }

    [Fact]
    public void S11751_while_the_holders_disagree_the_line_that_differs_says_mixed()
    {
        var agreed = SharedRowLines.Of(SharedSettings.Place, false, [(Weather, Placed(Weather, "Zurich", 47.4)), (Sun, Placed(Sun, "Zurich", 47.4))], Build, _ => { });
        var mixed = SharedRowLines.Of(SharedSettings.Place, true, [(Weather, Placed(Weather, "Zurich", 47.4)), (Sun, Placed(Sun, "Bern", 46.9))], Build, _ => { });

        Assert.Equal(agreed.Count, mixed.Count);
        Assert.Contains(mixed.Zip(agreed), pair => pair.First.Value != pair.Second.Value || pair.First.Search?.Value != pair.Second.Search?.Value);
    }
}
