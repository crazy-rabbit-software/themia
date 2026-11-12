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
    public void S11870_a_recipe_placed_as_one_group_draws_its_outer_border_only(string id)
    {
        var boxes = Of(id).Widgets.Select(w => new PixelRect(w.Box.X, w.Box.Y, w.Box.Width, w.Box.Height)).ToList();
        var tol = CardJoins.Tolerance(1);

        var outlines = CardJoins.Outlines([.. boxes.Select(b => (PixelRect?)b)], tol, groups: [.. boxes.Select(_ => (string?)"group")]);

        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            foreach (var side in Enum.GetValues<CardSide>())
            {
                var drawn = side switch
                {
                    CardSide.Left => outlines[i].DrawsLeft,
                    CardSide.Top => outlines[i].DrawsTop,
                    CardSide.Right => outlines[i].DrawsRight,
                    _ => outlines[i].DrawsBottom,
                };
                if (!drawn)
                {
                    continue;
                }

                // The side's own axis, and what of it the seams leave out.
                var (from, length) = side is CardSide.Top or CardSide.Bottom ? (box.X, box.Width) : (box.Y, box.Height);
                var seams = (outlines[i].Seams ?? []).Where(s => s.Side == side).Select(s => (From: from + (s.From * length), To: from + (s.To * length))).ToList();

                for (var j = 0; j < boxes.Count; j++)
                {
                    var other = boxes[j];
                    var meets = side switch
                    {
                        CardSide.Left => Math.Abs((other.X + other.Width) - box.X) <= tol,
                        CardSide.Top => Math.Abs((other.Y + other.Height) - box.Y) <= tol,
                        CardSide.Right => Math.Abs(other.X - (box.X + box.Width)) <= tol,
                        _ => Math.Abs(other.Y - (box.Y + box.Height)) <= tol,
                    };
                    if (j == i || !meets)
                    {
                        continue;
                    }

                    var (a, b) = side is CardSide.Top or CardSide.Bottom ? (other.X, (other.X + other.Width)) : (other.Y, (other.Y + other.Height));
                    var across = Math.Min(b, from + length) - Math.Max(a, from);
                    var left = across - seams.Sum(s => Math.Max(0, Math.Min(b, s.To) - Math.Max(a, s.From)));
                    Assert.True(across <= tol || left <= tol, $"{id}: widget {i} draws a line along widget {j}");
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11606_every_widget_s_box_is_a_size_its_kind_takes_for_its_config_and_header(string id)
    {
        foreach (var part in Of(id).Widgets)
        {
            var kind = new WidgetId(part.Kind);
            var config = WidgetConfig.CreateDefault(kind);
            if (part.Config is { } fragment)
            {
                config = StateJson.Merged(config, fragment)!;
            }

            // Placed, the box is painted fitted into the sizing ([S-2446]): a box it is not moves the panel apart.
            var shown = WidgetInstance.CanHaveHeader(kind) ? part.Header ?? WidgetInstance.DefaultHeader(kind) : HeaderPlacement.Hidden;
            var box = new SizeDip(part.Box.Width, part.Box.Height);
            var fitted = SizeFit.Of(config, shown).Fit(box);
            Assert.True(fitted == box, $"{id}: {part.Kind} at {box.Width} x {box.Height} is painted {fitted.Width} x {fitted.Height}");
        }
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S11606_the_boxes_fill_the_group_s_bounds_with_no_notch(string id)
    {
        var recipe = Of(id);
        var bounds = recipe.Bounds;

        // No two overlap (S2472_), so they fill the box round them exactly when their areas add up to it.
        Assert.Equal(bounds.Width * bounds.Height, recipe.Widgets.Sum(w => w.Box.Width * w.Box.Height), 3);
    }

    /// <summary>[S-11607] The groups that are an old card's atoms ([S-11090], [S-11172]): its first atom names it.</summary>
    public static TheoryData<string> OldCards() =>
    [
        "cpu", "cpuCores", "cpuDetails", "memory", "memoryDetails", "gpu", "gpuDetails", "disk", "network", "networkDetails",
        "ping", "pingDetails", "battery", "batteryDetails", "drive",
        "weatherStation", "weather", "sun", "moon", "airQuality", "exchangeRates", "stocks",
    ];

    [Theory]
    [MemberData(nameof(OldCards))]
    public void S11607_an_old_card_s_group_carries_one_header_on_its_first_atom(string id)
    {
        var recipe = Of(id);
        HeaderPlacement Shown(ReadyMadeWidget w) => w.Header ?? WidgetInstance.DefaultHeader(new WidgetId(w.Kind));

        Assert.Equal(HeaderPlacement.Top, Shown(recipe.Widgets[0]));
        Assert.All(recipe.Widgets.Skip(1), w => Assert.True(Shown(w) == HeaderPlacement.Hidden, $"{id}: {w.Kind} shows a header"));
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
        Assert.Equal(["thisPc", "myDay", "weatherStation"], ReadyMadeGroups.Offered().Select(r => r.Id).Take(3));
    }

    [Fact]
    public void S11209_my_files_is_offered_and_its_three_widgets_sit_at_their_kinds_sizes_and_join()
    {
        var recipe = Assert.Single(ReadyMadeGroups.Offered(), r => r.Id == "myFiles");
        Assert.Equal(["folder", "shortcuts", "recentFiles"], recipe.Widgets.Select(w => w.Kind));

        // Each box is what its kind's sizing makes of it, so placing it moves nothing.
        foreach (var part in recipe.Widgets)
        {
            var widget = FirstRunDesktop.Build(new FirstRunDesktop.Seed(part.Kind, new Themia.Core.Geometry.FracRect(0, 0, 0.1, 0.1), Config: part.Config))!;
            var sizing = ModuleCatalog.Of(new WidgetId(part.Kind)).Sizing(widget.Config);
            Assert.Equal(new SizeDip(part.Box.Width, part.Box.Height), sizing.Fit(new SizeDip(part.Box.Width, part.Box.Height)));
        }

        // One panel: the folder as tall as the column beside it.
        Assert.Equal(recipe.Widgets[0].Box.Height, recipe.Widgets[1].Box.Height + recipe.Widgets[2].Box.Height);
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
        Assert.Equal(ReadyMadeGroups.ThisPc.Widgets.Count, WidgetGroups.Members(view, group.Id).Count);
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

        Assert.Equal(ReadyMadeGroups.ThisPc.Widgets.Count, payload.Creates);
        Assert.Equal(ReadyMadeGroups.ThisPc.Widgets.Count, payload.Count);
        Assert.Equal(ReadyMadeGroups.ThisPc.Widgets.Count, payload.Plan(0.5, 0.5, Screen, snapEnabled: false).NetAdded(state));
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
    public void S2473_the_add_page_opens_on_the_ready_made_groups_first()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], ReadyMadeGroups.Offered());

        Assert.Equal(AddFolderId.Start, page.Tabs[0].Id);
        var first = page.Sections(AddFolderId.Start)[0];
        Assert.Equal(AddSectionKind.ReadyMade, first.Kind);
        Assert.Equal(new AddGroupTile(ReadyMadeGroups.MyDay), first.Tiles[0]);
    }

    [Fact]
    public void S2473_no_ready_made_group_no_ready_made_section()
    {
        var page = AddPage.Of([.. ModuleCatalog.Widgets.Select(w => w.Id)], []);

        Assert.DoesNotContain(page.Tabs.SelectMany(t => page.Sections(t.Id)), s => s.Kind == AddSectionKind.ReadyMade);
    }

    // ---- [S-2475] the picture ----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2475_the_recipe_scene_is_its_widgets_as_one_group_with_no_edit_chrome(string id)
    {
        var recipe = Of(id);

        var scene = ReadyMadeScene.Of(recipe, new Dictionary<string, string>()).Scenes[0];

        Assert.Equal(recipe.Widgets.Count, scene.Widgets.Count);
        Assert.Equal([recipe.Name.Text()], scene.Groups);
        Assert.Null(scene.Edit);
        Assert.Empty(scene.Selected);
        Assert.Empty(SceneWidgets.Check(scene, SceneScreen.Default));
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void S2475_asked_for_edit_mode_the_group_is_selected_under_the_bar(string id)
    {
        var recipe = Of(id);

        var scene = ReadyMadeScene.Of(recipe, new Dictionary<string, string>(), edit: true).Scenes[0];

        Assert.True(scene.Edit is { Bar: true });
        Assert.Equal(scene.Widgets, scene.Selected);
        Assert.Empty(SceneWidgets.Check(scene, SceneScreen.Default));
    }
}
