using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-11520] The screen-time and office family's ready-made groups: each atom's box is a size its kind
/// takes with the header the recipe gives it (so placing it moves nothing), only the first carries a
/// header, and every atom of a group is as wide as the others (one column, one panel).
/// </summary>
public sealed class OfficeRecipesTests
{
    /// <summary>The family's recipes.</summary>
    public static TheoryData<string> Recipes() => ["screenTime"];

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11520_every_atom_s_box_is_a_size_its_kind_takes_with_its_header(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        foreach (var part in recipe.Widgets)
        {
            var kind = new WidgetId(part.Kind);
            var config = WidgetConfig.CreateDefault(kind);
            if (part.Config is { } fragment)
            {
                config = StateJson.Merged(config, fragment)!;
            }

            var sizing = SizeFit.Of(config, part.Header ?? WidgetInstance.DefaultHeader(kind));
            var box = new SizeDip(part.Box.Width, part.Box.Height);
            Assert.True(sizing.Fit(box) == box, $"{id}: {part.Kind} at {box} is fitted to {sizing.Fit(box)}");
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11520_the_first_atom_carries_the_header_and_the_others_none(string id)
    {
        var recipe = ReadyMadeGroups.Find(id)!;
        var first = recipe.Widgets[0];
        Assert.NotEqual(HeaderPlacement.Hidden, first.Header ?? WidgetInstance.DefaultHeader(new WidgetId(first.Kind)));
        Assert.All(recipe.Widgets.Skip(1), w => Assert.Equal(HeaderPlacement.Hidden, w.Header ?? WidgetInstance.DefaultHeader(new WidgetId(w.Kind))));
    }

    [Fact]
    public void S11520_screen_time_is_the_reading_over_the_bars_over_a_week_of_apps_in_one_column()
    {
        var recipe = ReadyMadeGroups.Find("screenTime")!;
        Assert.Equal(["screenTime", "screenTimeChart", "screenTimeApps"], recipe.Widgets.Select(w => w.Kind));
        Assert.Single(recipe.Widgets.Select(w => w.Box.Width).Distinct());
        Assert.Equal(AddFolderId.ScreenTime, recipe.Home);

        var apps = (ScreenTimeAppsConfig)StateJson.Merged(WidgetConfig.CreateDefault(new WidgetId("screenTimeApps")), recipe.Widgets[2].Config!)!;
        Assert.Equal(ScreenTimePeriod.Week, apps.Period);
    }
}
