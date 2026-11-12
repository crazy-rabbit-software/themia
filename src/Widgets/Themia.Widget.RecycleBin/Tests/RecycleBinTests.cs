using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RecycleBin.Tests;

/// <summary>
/// [S-4860]..[S-4862] The Recycle Bin widget: its config, its count and size, its ladder. Moved
/// with the widget from Core's DeskInfoWidgetsTests, unchanged in what they assert.
/// </summary>
public sealed class RecycleBinTests
{
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
    public void S11201_one_fixed_size_holds_the_header_the_reading_its_count_and_empty()
    {
        var sizing = ModuleCatalog.Of(new WidgetId("recycleBin")).SizingOf();

        Assert.False(sizing.IsLegacy);
        Assert.True(sizing.FixedWidth && sizing.FixedHeight);
        Assert.Equal(new SizeDip(180, 36 + 36 + 16 + 8 + 28 + 12), sizing.Natural);
        Assert.Empty(sizing.Problems());
    }
}
