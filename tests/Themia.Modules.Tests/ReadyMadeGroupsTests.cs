using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Scenes;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2470]..[S-2475] The ready-made groups: every recipe held to the rules (here, where every widget
/// is), placed as one group in one placement, and offered first in the add menu.
/// </summary>
public sealed class ReadyMadeGroupsTests
{
    /// <summary>[S-2472] The work area a recipe must fit, in DIPs.</summary>
    private const double WorkWidth = 1280;
    private const double WorkHeight = 720;

    private static readonly ScreenMetrics Screen = new(1920, 1080);

    public static TheoryData<string> Recipes()
    {
        var data = new TheoryData<string>();
        foreach (var recipe in ReadyMadeGroups.All)
        {
            data.Add(recipe.Id);
        }

        return data;
    }

    private static ReadyMadeGroup Of(string id) => ReadyMadeGroups.Find(id)!;

    // ---- [S-2472] the rules every recipe keeps ---------------------------------------------------

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2472_every_kind_is_installed_and_every_config_fragment_names_only_its_properties(string id)
    {
        foreach (var part in Of(id).Widgets)
        {
            Assert.True(WidgetId.TryParse(part.Kind, out var kind), $"{id}: '{part.Kind}' is not a widget id");
            Assert.True(ModuleCatalog.TryGet(kind, out var descriptor), $"{id}: '{part.Kind}' is not installed");

            if (part.Config is { } fragment)
            {
                var known = StateJson.Options.GetTypeInfo(descriptor.ConfigType).Properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
                using var document = JsonDocument.Parse(fragment);
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    Assert.True(known.Contains(property.Name), $"{id}: {part.Kind} has no config property '{property.Name}'");
                }

                Assert.NotNull(StateJson.Merged(WidgetConfig.CreateDefault(kind), fragment));
            }

            if (part.Header is { } header)
            {
                Assert.True(header == HeaderPlacement.Hidden || WidgetInstance.CanHaveHeader(kind), $"{id}: {part.Kind} can have no header");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2472_has_two_widgets_or_more_and_no_two_boxes_overlap(string id)
    {
        var boxes = Of(id).Widgets.Select(w => w.Box).ToList();

        Assert.True(boxes.Count >= 2, $"{id}: a group holds two widgets or more");
        Assert.All(boxes, b => Assert.True(b.Width > 0 && b.Height > 0 && b.X >= 0 && b.Y >= 0, $"{id}: a box is empty or before the corner"));
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var (a, b) = (boxes[i], boxes[j]);
                var overlaps = a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;
                Assert.False(overlaps, $"{id}: widgets {i} and {j} overlap");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2472_every_widget_touches_another_so_the_recipe_draws_as_one_panel(string id)
    {
        PixelRect?[] boxes = [.. Of(id).Widgets.Select(w => (PixelRect?)new PixelRect(w.Box.X, w.Box.Y, w.Box.Width, w.Box.Height))];

        var outlines = CardJoins.Outlines(boxes, CardJoins.Tolerance(1));

        for (var i = 0; i < outlines.Length; i++)
        {
            Assert.NotEqual(CardOutline.Free, outlines[i]);
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2472_fits_a_1280_by_720_work_area(string id)
    {
        var bounds = Of(id).Bounds;

        Assert.True(bounds.Width <= WorkWidth && bounds.Height <= WorkHeight, $"{id} is {bounds.Width} x {bounds.Height} DIPs");
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2472_has_its_words_and_a_glyph(string id)
    {
        var recipe = Of(id);

        Assert.False(string.IsNullOrWhiteSpace(recipe.Name.Text()));
        Assert.False(string.IsNullOrWhiteSpace(recipe.About.Text()));
        Assert.False(string.IsNullOrEmpty(recipe.Glyph));
        Assert.Single(ReadyMadeGroups.All, r => r.Id == id);
    }

    [Fact]
    public void S2474_the_three_seeds_are_offered_in_order()
    {
        Assert.Equal(["thisPc", "myDay", "weather"], ReadyMadeGroups.Offered().Select(r => r.Id));
    }

    // ---- [S-2471] placing one ---------------------------------------------------------------------

    [Fact]
    public void S2471_placing_lands_every_widget_as_one_new_group_named_after_the_recipe_in_one_step()
    {
        var state = new AppState { Views = [ViewState.Create("v")] };
        state.ActiveViewId = state.Views[0].Id;
        var history = new LayoutHistory(state);
        var payload = PlacementPayload.ForReadyMade(ReadyMadeGroups.ThisPc);

        var plan = payload.Plan(0.5, 0.5, Screen, snapEnabled: true);
        state.ApplyPlacement(plan, state.Views[0].Id);
        history.Commit(state, UndoActions.ForPlacement(payload.Mode));

        var view = state.Views[0];
        var group = Assert.Single(view.Groups);
        Assert.Equal(Str.Groups_Recipe_ThisPc.Text(), group.Name);
        Assert.Equal(4, WidgetGroups.Members(view, group.Id).Count);
        Assert.Equal(plan.Roots, view.Widgets.Select(w => w.Id));
        Assert.Equal(1, history.UndoCount);

        history.Undo(state);
        Assert.Empty(state.Views[0].Widgets);
        Assert.Empty(state.Views[0].Groups);
    }

    [Fact]
    public void S2471_the_widgets_land_where_the_ghost_was_and_still_touch()
    {
        var payload = PlacementPayload.ForReadyMade(ReadyMadeGroups.MyDay);

        var ghost = payload.Outlines(0.4, 0.4, Screen, snapEnabled: true);
        var plan = payload.Plan(0.4, 0.4, Screen, snapEnabled: true);

        Assert.Equal(ghost.Select(g => g.Rect), plan.Added.Select(w => w.Rect));
        Assert.Equal(ReadyMadeGroups.MyDay.Widgets.Select(w => new WidgetId(w.Kind)), ghost.Select(g => g.Kind));
        var clock = GridGeometry.ToPixels(plan.Added[0].Rect, Screen);
        var meeting = GridGeometry.ToPixels(plan.Added[1].Rect, Screen);
        Assert.Equal(clock.Y + clock.Height, meeting.Y, 6);
        Assert.Equal(320, clock.Width, 6);
    }

    [Fact]
    public void S2471_the_licence_counts_a_recipe_as_its_widgets_one_by_one()
    {
        var payload = PlacementPayload.ForReadyMade(ReadyMadeGroups.ThisPc);
        var state = new AppState { Views = [ViewState.Create("v")] };

        Assert.Equal(4, payload.Creates);
        Assert.Equal(4, payload.Count);
        Assert.Equal(4, payload.Plan(0.5, 0.5, Screen, snapEnabled: false).NetAdded(state));
        Assert.Equal(UndoAction.Add, UndoActions.ForPlacement(payload.Mode));
    }

    [Fact]
    public void S2471_each_placement_is_a_new_group_with_new_ids()
    {
        var payload = PlacementPayload.ForReadyMade(ReadyMadeGroups.Weather);

        var first = payload.Plan(0.3, 0.3, Screen, snapEnabled: false);
        var second = payload.Plan(0.6, 0.3, Screen, snapEnabled: false);

        Assert.NotEqual(first.Groups[0].Id, second.Groups[0].Id);
        Assert.Empty(first.Roots.Intersect(second.Roots));
    }

    // ---- [S-2473] the add menu ---------------------------------------------------------------------

    [Fact]
    public void S2473_the_menu_offers_the_ready_made_groups_first_and_enter_picks_one()
    {
        var menu = AddTree.WithReadyMade(AddTree.On([.. ModuleCatalog.Widgets.Select(w => w.Id)]), ReadyMadeGroups.Offered());

        Assert.Equal(AddFolderId.ReadyMade, menu[0].Shelf.Id);
        Assert.Equal(ReadyMadeGroups.Offered(), menu[0].Rows.Cast<AddReadyMade>().Select(r => r.Group));

        var step = AddMenuKeys.Move(menu, new AddMenuNav([], new AddSpot(0, 1)), WidgetKey.Enter);
        Assert.Equal(AddMenuAction.Pick, step.Action);
        Assert.Same(ReadyMadeGroups.MyDay, step.ReadyMade);
    }

    [Fact]
    public void S2473_no_ready_made_group_no_column()
    {
        var menu = AddTree.On([.. ModuleCatalog.Widgets.Select(w => w.Id)]);

        Assert.Same(menu, AddTree.WithReadyMade(menu, []));
    }

    // ---- [S-2475] the picture ----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2475_the_recipe_scene_is_its_widgets_selected_as_one_group(string id)
    {
        var recipe = Of(id);

        var scene = ReadyMadeScene.Of(recipe, new Dictionary<string, string>()).Scenes[0];

        Assert.Equal(recipe.Widgets.Count, scene.Widgets.Count);
        Assert.Equal([recipe.Name.Text()], scene.Groups);
        Assert.Equal(scene.Widgets, scene.Selected);
        Assert.Empty(SceneWidgets.Check(scene, SceneScreen.Default));
    }
}
