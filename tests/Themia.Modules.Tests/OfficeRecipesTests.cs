using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11520] The screen-time and office family's ready-made groups: each atom's box is a size its kind
/// takes (so placing it moves nothing), [S-12485] the first is the group's Header widget, and every atom
/// of a group is as wide as the others (one column, one panel).
/// </summary>
public sealed class OfficeRecipesTests
{
    /// <summary>The family's recipes.</summary>
    public static TheoryData<string> Recipes() => ["screenTime"];

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11520_every_atom_s_box_is_a_size_its_kind_takes(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        foreach (var part in recipe.Widgets)
        {
            var kind = new WidgetId(part.Kind);
            var config = WidgetConfig.CreateDefault(kind);
            if (part.Fragment is { } fragment)
            {
                config = StateJson.Merged(config, fragment)!;
            }

            var sizing = SizeFit.Of(config);
            var box = new SizeDip(part.Box.Width, part.Box.Height);
            Assert.True(sizing.Fit(box) == box, $"{id}: {part.Kind} at {box} is fitted to {sizing.Fit(box)}");
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S12485_the_first_atom_is_the_group_s_header_widget_and_no_other(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        Assert.Equal("header", recipe.Widgets[0].Kind);
        Assert.DoesNotContain(recipe.Widgets.Skip(1), w => w.Kind == "header");
    }

    [Fact]
    public void S11520_screen_time_is_the_reading_over_the_bars_over_a_week_of_apps_in_one_column()
    {
        var recipe = ReadyMadeGroups.Find("screenTime")!;
        Assert.Equal(["header", "screenTime", "screenTimeChart", "screenTimeApps"], recipe.Widgets.Select(w => w.Kind));
        Assert.Single(recipe.Widgets.Select(w => w.Box.Width).Distinct());
        Assert.Equal(AddFolderId.ScreenTime, recipe.Home);

        var apps = (ScreenTimeAppsConfig)StateJson.Merged(WidgetConfig.CreateDefault(new WidgetId("screenTimeApps")), recipe.Widgets[3].Config!)!;
        Assert.Equal(ScreenTimePeriod.Week, apps.Period);
    }
}
