using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-3900] [S-3901] Which kinds are quick-setting toggles, their configs and their names, over every
/// widget of the build (the toggles, the power and Bluetooth widgets are module projects, which only
/// this host has). The rest of the toggles' tests are Core's QuickToggleTests.
/// </summary>
public sealed class QuickToggleHostTests
{
    private static IEnumerable<WidgetId> ToggleKinds
        => ModuleCatalog.Widgets.Select(w => w.Id).ToArray().Where(k => QuickToggleRules.ForKind(k) is not null);

    [Fact]
    public void S3900_every_toggle_has_its_own_kind_and_back()
    {
        Assert.Equal(Enum.GetValues<QuickToggle>().Order(), QuickToggleRules.All.Order());
        Assert.Equal(QuickToggleRules.All.Count, ToggleKinds.Count());

        foreach (var toggle in QuickToggleRules.All)
        {
            var kind = QuickToggleRules.KindOf(toggle);
            Assert.Equal(toggle, QuickToggleRules.ForKind(kind));
            Assert.Equal(toggle, ((QuickToggleConfig)WidgetConfig.CreateDefault(kind)).Toggle);
        }
    }

    [Fact]
    public void S3900_every_toggle_config_round_trips_through_the_state_file()
    {
        foreach (var kind in ToggleKinds)
        {
            var config = WidgetConfig.CreateDefault(kind);
            var json = System.Text.Json.JsonSerializer.Serialize(config, StateJson.WidgetConfig);
            var back = System.Text.Json.JsonSerializer.Deserialize(json, StateJson.WidgetConfig);
            Assert.NotNull(back);
            Assert.Equal(kind, back!.Kind);
            Assert.Equal(kind, back.Clone().Kind);
        }
    }

    [Fact]
    public void S3901_a_new_toggle_has_no_header_and_needs_no_setup()
    {
        foreach (var kind in ToggleKinds)
        {
            Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(kind));
            Assert.False(WidgetSetup.Needed(WidgetConfig.CreateDefault(kind), Themia.Sdk.FeatureSet.None));
        }
    }

    [Fact]
    public void S3901_names_and_picker_labels_are_set_in_every_language_and_the_picker_label_is_never_another_widgets_name()
    {
        foreach (var locale in Enum.GetValues<Locale>())
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            foreach (var toggle in QuickToggleRules.All)
            {
                var name = QuickToggleRules.Name(locale, toggle);
                var descriptor = ModuleCatalog.Of(QuickToggleRules.KindOf(toggle));
                var label = descriptor.Label.Text(locale);
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.False(string.IsNullOrWhiteSpace(descriptor.Description.Text(locale)));
                Assert.NotEqual(name, label);
                Assert.True(labels.Add(label), $"{locale}: {label} twice");
            }
        }
    }

    [Fact]
    public void S3900_a_power_or_meter_kind_is_not_a_toggle()
    {
        Assert.Null(QuickToggleRules.ForKind(new WidgetId("shutdown")));
        Assert.Null(QuickToggleRules.ForKind(new WidgetId("bluetooth")));
        Assert.Null(QuickToggleRules.ForKind(new WidgetId("volume")));
    }
}
