using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3900] [S-3901] The quick setting over every widget of the build (its module, the power and Bluetooth widgets are
/// module projects, which only this host has): its config, its setup and its names. The rest of the toggles' tests are
/// Core's QuickToggleTests and the module's own.
/// </summary>
public sealed class QuickToggleHostTests
{
    private static readonly WidgetId Kind = new("quickToggle");

    private static IEnumerable<QuickToggleConfig> Targets
        => QuickToggleRules.All.Select(t => new QuickToggleConfig { Target = t });

    [Fact]
    public void S3900_every_target_round_trips_through_the_state_file()
    {
        foreach (var config in Targets)
        {
            var json = System.Text.Json.JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
            var back = System.Text.Json.JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
            Assert.NotNull(back);
            Assert.Equal(Kind, back!.Kind);
            Assert.Equal(config.Target, ((QuickToggleConfig)back.Clone()).Target);
        }
    }

    [Fact]
    public void S3901_a_new_toggle_needs_no_setup()
    {
        foreach (var config in Targets)
        {
            Assert.False(WidgetSetup.Needed(config, Themia.Sdk.FeatureSet.None));
        }
    }

    [Fact]
    public void S3901_names_are_set_in_every_language()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            Assert.All(QuickToggleRules.All, toggle => Assert.False(string.IsNullOrWhiteSpace(QuickToggleRules.Name(locale, toggle))));
        }
    }

    [Fact]
    public void S3900_a_power_or_meter_kind_is_not_a_quick_setting()
    {
        Assert.Single(ModuleCatalog.Widgets, w => w.CreateDefault() is QuickToggleConfig);
        Assert.IsNotType<QuickToggleConfig>(WidgetConfig.CreateDefault(new WidgetId("shutdown")));
        Assert.IsNotType<QuickToggleConfig>(WidgetConfig.CreateDefault(new WidgetId("bluetooth")));
    }
}
