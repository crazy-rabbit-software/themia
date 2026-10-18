using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-4400] [S-4421] [S-4442] The brightness and focus widgets' configs under the root's rules (the screens choice's wire and repair; a hand-edited focus length pulled into range; none of Windows' panels asks to be set up): the configs are their modules', so they run here. Moved from Core's WindowsPanelsTests, unchanged in what they assert.</summary>
public sealed class WindowsPanelsHostTests
{

    [Fact]
    public void S4442_the_screens_choice_is_written_as_a_word_and_read_back()
    {
        var config = new BrightnessConfig { Screens = BrightnessScreens.ThisDisplay };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);

        Assert.Contains("\"ThisDisplay\"", json, StringComparison.Ordinal);
        var back = Assert.IsType<BrightnessConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(BrightnessScreens.ThisDisplay, back.Screens);
        Assert.Equal(BrightnessScreens.ThisDisplay, ((BrightnessConfig)back.Clone()).Screens);
    }

    [Fact]
    public void S4442_an_unknown_screens_number_is_repaired_to_every_display()
    {
        var config = new BrightnessConfig { Screens = (BrightnessScreens)7 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(BrightnessScreens.All, config.Screens);
    }

    [Fact]
    public void S4400_none_of_the_three_asks_to_be_set_up()
    {
        foreach (var kind in new[] { new WidgetId("notifications"), new WidgetId("focusSession"), new WidgetId("brightness") })
        {
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), Themia.Sdk.FeatureSet.None), kind.ToString());
        }
    }

    [Theory]
    [InlineData("notifications")]
    [InlineData("focusSession")]
    [InlineData("brightness")]
    public void S4400_each_config_round_trips_through_the_state_file(string kindId)
    {
        var kind = new WidgetId(kindId);
        var config = WidgetConfig.CreateDefault(kind);
        var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
        var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig);

        Assert.NotNull(back);
        Assert.Equal(kind, back!.Kind);
        Assert.Equal(kind, back.Clone().Kind);
    }

    [Fact]
    public void S4421_a_hand_edited_length_is_pulled_into_range_and_said_so()
    {
        var config = new FocusSessionConfig { Minutes = 9000 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal(FocusSessionRules.MaxMinutes, config.Minutes);

        var fine = new FocusSessionConfig { Minutes = 45 };
        Assert.Empty(fine.Repair());
        Assert.Equal(45, fine.Minutes);
    }
}
