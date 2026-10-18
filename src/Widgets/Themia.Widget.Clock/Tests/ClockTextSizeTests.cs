using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>[S-1476] [S-1477] The clock's label and date size (issue #170).</summary>
public class ClockTextSizeTests
{
    [Fact]
    public void S1476_default_is_the_ladder_the_clock_always_had()
    {
        foreach (var (width, height) in new[] { (248.0, 104.0), (448.0, 128.0), (528.0, 344.0), (88.0, 88.0) })
        {
            var before = FluentType.Fit(width, height * 0.22, 23, FluentType.Caption, FluentType.BodyLarge);
            Assert.Equal(before.FontSize, ClockText.Fit(ClockTextSize.Default, width, height, 0.22, 23, FluentType.Caption, FluentType.BodyLarge).FontSize);
        }
    }

    [Fact]
    public void S1476_large_sets_the_label_and_date_larger_where_there_is_room()
    {
        // A panel with room: the default stops at BodyLarge, Large goes past it.
        var standard = ClockText.Fit(ClockTextSize.Default, 528, 344, 0.22, 10, FluentType.Caption, FluentType.BodyLarge);
        var large = ClockText.Fit(ClockTextSize.Large, 528, 344, 0.22, 10, FluentType.Caption, FluentType.BodyLarge);
        Assert.Equal(FluentType.BodyLarge.FontSize, standard.FontSize);
        Assert.True(large.FontSize > standard.FontSize, $"{large.FontSize} should be larger than {standard.FontSize}");

        // Never the semibold of a title: a larger date is still support ([S-1487]).
        Assert.False(large.SemiBold);

        // And never under Body, even on a small tile, where the default may drop to Caption.
        var smallTile = ClockText.Fit(ClockTextSize.Large, 88, 60, 0.22, 10, FluentType.Caption, FluentType.BodyLarge);
        Assert.True(smallTile.FontSize >= FluentType.Body.FontSize);
    }

    [Fact]
    public void S1476_small_never_grows_past_one_step_under_the_default()
    {
        foreach (var (width, height) in new[] { (248.0, 104.0), (528.0, 344.0), (768.0, 504.0) })
        {
            var small = ClockText.Fit(ClockTextSize.Small, width, height, 0.22, 10, FluentType.Caption, FluentType.BodyLarge);
            Assert.True(small.FontSize <= FluentType.Body.FontSize);
        }
    }

    [Fact]
    public void S1476_the_size_round_trips_and_a_bad_one_is_repaired()
    {
        var config = new ClockConfig { TextSize = ClockTextSize.Large };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        Assert.Contains("\"Large\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<ClockConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(ClockTextSize.Large, back.TextSize);
        Assert.Equal(ClockTextSize.Large, Assert.IsType<ClockConfig>(back.Clone()).TextSize);

        var bad = new ClockConfig { TextSize = (ClockTextSize)42 };
        Assert.Single(bad.Repair());
        Assert.Equal(ClockTextSize.Default, bad.TextSize);
    }

    [Fact]
    public void S1477_a_shift_walks_the_ramp_and_stops_at_its_ends()
    {
        Assert.Equal(FluentType.Body.FontSize, ClockText.Shift(FluentType.Caption, 1).FontSize);
        Assert.Equal(FluentType.Title.FontSize, ClockText.Shift(FluentType.BodyLarge, 2).FontSize);
        Assert.Equal(FluentType.Caption.FontSize, ClockText.Shift(FluentType.Caption, -3).FontSize);
        Assert.Equal(FluentType.Display.FontSize, ClockText.Shift(FluentType.Title, 9).FontSize);
        Assert.Equal(FluentType.Body.FontSize, ClockText.Floor(ClockTextSize.Large, FluentType.Caption).FontSize);
        Assert.Equal(FluentType.Caption.FontSize, ClockText.Floor(ClockTextSize.Small, FluentType.Caption).FontSize);
    }
}
