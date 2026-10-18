using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.RecycleBin.Tests;

/// <summary>
/// [S-4860]..[S-4862] The Recycle Bin widget: its config, its count and size, its ladder. Moved
/// with the widget from Core's DeskInfoWidgetsTests, unchanged in what they assert.
/// </summary>
public sealed class RecycleBinTests
{
    private static WidgetArea Inset(double width, double height) => new WidgetArea(width, height).Deflate(16, 16);

    [Fact]
    public void S4860_its_config_round_trips_and_it_never_asks_to_be_set_up()
    {
        var kind = new WidgetId("recycleBin");
        var config = WidgetConfig.CreateDefault(kind);
        Assert.IsType<RecycleBinConfig>(config);
        Assert.Equal(kind, config.Kind);

        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
        Assert.IsType<RecycleBinConfig>(back);
        Assert.Empty(back!.Repair());

        Assert.False(WidgetSetup.Needed(new RecycleBinConfig(), FeatureSet.None));
    }

    [Fact]
    public void S4861_says_how_many_items_or_that_it_is_empty()
    {
        Assert.Null(RecycleBinLayout.CountText(Locale.En, null));
        Assert.Equal("No items", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(0, 0)));
        Assert.Equal("1 item", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(1, 10)));
        Assert.Equal("37 items", RecycleBinLayout.CountText(Locale.En, new RecycleBinState(37, 10)));
        Assert.Equal("–", RecycleBinLayout.SizeText(Locale.En, null));
    }

    [Fact]
    public void S4862_the_size_always_shows_and_the_button_comes_with_room()
    {
        var xs = RecycleBinLayout.Plan(Inset(120, 76));
        Assert.False(xs.Button);

        var medium = RecycleBinLayout.Plan(Inset(200, 200));
        Assert.True(medium.Count);
        Assert.True(medium.Button);

        var strip = RecycleBinLayout.Plan(Inset(480, 116));
        Assert.True(strip.Side);
        Assert.True(strip.Button);
    }
}
