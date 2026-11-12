using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.SunTimes.Tests;

/// <summary>[S-11115]..[S-11117] The sun times: its rows, its pick, its sizing and its menu.</summary>
public sealed class SunTimesTests
{
    private static readonly WidgetDescriptor Kind = new SunTimesModule().Descriptor;

    /// <summary>Zurich at the summer solstice's midday (UTC+2).</summary>
    private static readonly DateTimeOffset Noon = new(2026, 6, 21, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S11115_a_new_card_shows_sunrise_sunset_and_the_daylight()
        => Assert.Equal([SunTimeKind.Sunrise, SunTimeKind.Sunset, SunTimeKind.Daylight], new SunTimesConfig().Times);

    [Fact]
    public void S11115_each_picked_row_is_one_row_in_order_and_the_next_of_rise_and_set_is_emphasised()
    {
        var rows = SunTimeRules.For([SunTimeKind.Dusk, SunTimeKind.Sunset, SunTimeKind.Sunrise], Noon, 47.37, 8.54);

        Assert.Equal([SunTimeKind.Dusk, SunTimeKind.Sunset, SunTimeKind.Sunrise], rows.Select(r => r.Kind));
        Assert.Equal([false, true, false], rows.Select(r => r.Next));
        Assert.All(rows, r => Assert.NotEqual("—", r.Value));
    }

    [Fact]
    public void S11115_inside_the_polar_circle_a_rise_or_set_that_does_not_happen_is_the_dash()
    {
        var rows = SunTimeRules.For([SunTimeKind.Sunrise, SunTimeKind.Sunset], Noon, 78.2, 15.6);
        Assert.Equal(["—", "—"], rows.Select(r => r.Value));
    }

    [Fact]
    public void S11115_the_pick_survives_the_state_file_and_a_copy_and_an_empty_one_is_repaired()
    {
        var config = new SunTimesConfig { Times = [SunTimeKind.Noon, SunTimeKind.Dawn] };
        var back = (SunTimesConfig)JsonSerializer.Deserialize(JsonSerializer.Serialize(config, StateJson.WidgetConfig), StateJson.WidgetConfig)!;
        Assert.Equal([SunTimeKind.Noon, SunTimeKind.Dawn], back.Times);
        Assert.Equal([SunTimeKind.Noon, SunTimeKind.Dawn], ((SunTimesConfig)config.Clone()).Times);

        var empty = new SunTimesConfig { Times = [] };
        Assert.NotEmpty(empty.Repair());
        Assert.Equal(SunTimeRules.Defaults, empty.Times);
    }

    [Fact]
    public void S11116_its_height_is_a_28_row_a_time_under_the_header_and_dragging_sets_the_rows()
    {
        var config = new SunTimesConfig();
        Assert.Equal(new SizeDip(240, 36 + (3 * 28) + 12), Kind.Sizing(config).Natural);
        Assert.Equal(36 + (6 * 28) + 12, Kind.Sizing(config).Max.Height);

        var taller = (SunTimesConfig)Kind.SizeSets!(config, new SizeDip(240, 36 + (5 * 28) + 12));
        Assert.Equal([SunTimeKind.Sunrise, SunTimeKind.Sunset, SunTimeKind.Daylight, SunTimeKind.Noon, SunTimeKind.Dawn], taller.Times);
    }

    [Fact]
    public void S11117_the_menu_is_the_rows_then_the_place()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new SunTimesModule().MenuLines(menu, new SunTimesConfig());

        Assert.Equal(SunTimesStr.SunTimes_Menu_Times.Text(), menu.Lines[0].Text);
        Assert.True(menu.Lines[1].IsSeparator);
        Assert.Contains(menu.Lines, l => l.Search is not null);
    }
}
