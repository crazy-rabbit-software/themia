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
    public void S4862_the_size_climbs_its_steps_the_count_comes_under_it_and_empty_where_the_controls_fit()
    {
        // The view's area: the card less its header (44 of a card's height).
        static RecycleBinPlan At(double width, double cardHeight) => RecycleBinLayout.Plan(new WidgetArea(width, cardHeight - 44));

        Assert.Equal(new RecycleBinPlan(FamilyType.Big28, Count: true, Button: false), At(120, 120));
        Assert.Equal(new RecycleBinPlan(FamilyType.Big28, Count: true, Button: false), At(200, 150));
        Assert.Equal(new RecycleBinPlan(FamilyType.Big40, Count: true, Button: true), At(320, 240));
        Assert.Equal(new RecycleBinPlan(FamilyType.Big28, Count: true, Button: true), At(480, 160));
        Assert.Equal(new RecycleBinPlan(FamilyType.Big68, Count: true, Button: true), At(800, 560));
        Assert.False(RecycleBinLayout.Plan(default).Button);
    }
}
