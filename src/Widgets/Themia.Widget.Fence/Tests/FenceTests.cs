using System.Text.Json;
using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Fence.Tests;

/// <summary>[S-10900]..[S-10920] The fence widget's rules, with the real kind registered.</summary>
public sealed class FenceTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("fence"));

    private static FenceConfig Full() => new()
    {
        Name = "Work",
        Items = [Desk + "a.txt", Desk + "b.txt"],
        Rules = new FenceRuleSet { Types = [FenceFileType.Images], Extensions = [".psd"], NameContains = "invoice" },
        RolledUp = true,
        OpenHeight = 0.3,
        Scroll = 2,
        Sort = DesktopSortBy.DateModified,
        RollUpOnClick = false,
        HoverPeek = false,
    };

    [Fact]
    public void S10900_a_clone_is_a_fence_of_its_own_with_everything_copied()
    {
        var config = Full();
        var copy = Assert.IsType<FenceConfig>(config.Clone());

        Assert.NotSame(config, copy);
        Assert.NotSame(config.Items, copy.Items);
        Assert.NotSame(config.Rules, copy.Rules);
        Assert.Equal(config.Items, copy.Items);
        Assert.Equal((config.Name, config.RolledUp, config.OpenHeight, config.Scroll, config.Sort, config.RollUpOnClick, config.HoverPeek), (copy.Name, copy.RolledUp, copy.OpenHeight, copy.Scroll, copy.Sort, copy.RollUpOnClick, copy.HoverPeek));
        Assert.Equal(config.Rules.Types, copy.Rules.Types);
        Assert.Equal(config.Rules.Extensions, copy.Rules.Extensions);
        Assert.Equal(config.Rules.NameContains, copy.Rules.NameContains);
    }

    [Fact]
    public void S10916_it_is_stored_and_read_back_whole()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(Full(), StateJson.WidgetConfig);
        var back = Assert.IsType<FenceConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));

        Assert.Contains("\"dateModified\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"Images\"", json, StringComparison.Ordinal);
        Assert.Equal(Full().Items, back.Items);
        Assert.Equal(DesktopSortBy.DateModified, back.Sort);
        Assert.Equal([FenceFileType.Images], back.Rules.Types);
        Assert.True(back.RolledUp);
        Assert.Equal(2, back.Scroll);
        Assert.False(back.HoverPeek);

        // A new fence: as placed, rolling up on a click, opening under the pointer, no rules.
        var fresh = Assert.IsType<FenceConfig>(JsonSerializer.Deserialize(JsonSerializer.Serialize<WidgetConfig>(new FenceConfig(), StateJson.WidgetConfig), StateJson.WidgetConfig));
        Assert.Null(fresh.Sort);
        Assert.True(fresh.RollUpOnClick);
        Assert.True(fresh.HoverPeek);
        Assert.False(fresh.Rules.Any);
    }

    [Fact]
    public void S10900_what_a_fence_shows_and_its_query_are_stored_cloned_and_repaired()
    {
        var config = new FenceConfig
        {
            Source = FenceSource.Search,
            Query = new FenceQuery { Folder = @"D:\Photos", Modified = FenceRecency.Week, Max = 120 },
            Rules = new FenceRuleSet { Types = [FenceFileType.Images] },
        };

        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        Assert.Contains("\"Search\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Week\"", json, StringComparison.Ordinal);
        var back = Assert.IsType<FenceConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(FenceSource.Search, back.Source);
        Assert.Equal((@"D:\Photos", FenceRecency.Week, 120), (back.Query.Folder, back.Query.Modified, back.Query.Max));

        var copy = Assert.IsType<FenceConfig>(config.Clone());
        Assert.NotSame(config.Query, copy.Query);
        Assert.Equal((config.Source, config.Query.Folder, config.Query.Modified, config.Query.Max), (copy.Source, copy.Query.Folder, copy.Query.Modified, copy.Query.Max));

        // A new fence holds what is put in it; a hand-edited query is made whole, loudly.
        Assert.Equal(FenceSource.Held, new FenceConfig().Source);
        var edited = new FenceConfig { Source = (FenceSource)9, Query = null! };
        Assert.Equal(2, edited.Repair().Count);
        Assert.Equal(FenceSource.Held, edited.Source);
        Assert.NotNull(edited.Query);
    }

    [Fact]
    public void S10917_repair_makes_a_hand_edited_fence_whole()
    {
        var config = new FenceConfig { Name = "  Wo\nrk  " + new string('x', 200), Items = null!, Rules = null! };

        var repairs = config.Repair();

        Assert.Equal(3, repairs.Count);
        Assert.Empty(config.Items);
        Assert.NotNull(config.Rules);
        Assert.StartsWith("Work", config.Name, StringComparison.Ordinal);
        Assert.Equal(FenceConfig.MaxName, config.Name.Length);
        Assert.Empty(config.Repair());
    }

    [Fact]
    public void S10901_a_fence_grows_as_made_and_rolled_up_is_its_title_bar_alone()
    {
        var open = Kind.SizingOf(new FenceConfig());
        var rolled = Kind.SizingOf(new FenceConfig { RolledUp = true });

        Assert.True(open.Natural.Height > FenceGrid.TitleDip + 100);
        Assert.True(open.Max.Height >= SizeStretch.Ceiling);
        Assert.Equal((FenceGrid.TitleDip, FenceGrid.TitleDip, FenceGrid.TitleDip), (rolled.Min.Height, rolled.Natural.Height, rolled.Max.Height));

        // [S-13040] Grid-fitted on any display (a cell 9.5..10.5 DIPs) the title bar is exactly its rows.
        foreach (var screen in new[] { new ScreenMetrics(1536, 864), new ScreenMetrics(1920, 1080), new ScreenMetrics(1366, 728), new ScreenMetrics(2560, 1392) })
        {
            var limits = SizeFit.Limits(rolled, screen, 1)!;
            Assert.Equal(FenceGrid.TitleCells, limits.Height.Range(0) * GridGeometry.GridDims(screen).Rows, 6);
            Assert.Equal(FenceCells.TitleHeight(screen), limits.Height.Range(0), 9);
        }
        Assert.Equal(open.Min.Width, rolled.Min.Width);
        Assert.True(rolled.Max.Width >= SizeStretch.Ceiling);
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.NeverJoins));
        Assert.Equal(Themia.Core.Widgets.AddFolderId.Files, Kind.Add!.Shelf);
    }

    [Fact]
    public void S10918_an_undo_puts_items_back_where_they_stood_free_or_fenced()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.5, 0.1, 0.3, 0.3));
        var item = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.01, 0.01, 0.08, 0.125));
        ((IDesktopItemConfig)item.Config).Item = Desk + "a.txt";
        home.Widgets.AddRange([fence, item]);
        var history = new LayoutHistory(state);
        var screen = new ScreenMetrics(1000, 800);
        var box = DesktopGrid.IconSize(new SizeDip(76, 100), screen);

        // Into the fence: its icon is the fence's, laid on its first cell; one step of the arrangement.
        DesktopFences.MoveIn(home, fence.Id, [Desk + "a.txt"], 0);
        DesktopFences.Lay(home, screen, box);
        Assert.True(history.Commit(state, UndoAction.Move));
        Assert.Equal(2, home.Widgets.Count);
        var laid = home.Widgets.Single(w => w.Id == item.Id).Rect;
        Assert.NotEqual(new FracRect(0.01, 0.01, 0.08, 0.125), laid);

        // Its undo: the free icon back where it stood and the fence empty again; its redo: in the fence, on its cell.
        Assert.Equal(UndoAction.Move, history.Undo(state));
        Assert.Equal(2, home.Widgets.Count);
        Assert.Empty(((FenceConfig)home.Widgets.Single(w => w.Id == fence.Id).Config).Items);
        Assert.Equal(new FracRect(0.01, 0.01, 0.08, 0.125), home.Widgets.Single(w => w.Id == item.Id).Rect);
        Assert.Equal(UndoAction.Move, history.Redo(state));
        Assert.Equal([Desk + "a.txt"], ((FenceConfig)home.Widgets.Single(w => w.Id == fence.Id).Config).Items);
        Assert.Equal(laid, home.Widgets.Single(w => w.Id == item.Id).Rect);
    }

    [Fact]
    public void S10919_a_removed_fences_undo_brings_it_back_with_its_items_on_their_cells()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var screen = new ScreenMetrics(1000, 800);
        var box = DesktopGrid.IconSize(new SizeDip(76, 100), screen);
        var fence = WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.5, 0.1, 0.3, 0.3));
        home.Widgets.Add(fence);
        foreach (var name in new[] { "a.txt", "b.txt" })
        {
            var icon = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0, 0, 0, 0));
            ((IDesktopItemConfig)icon.Config).Item = Desk + name;
            home.Widgets.Add(icon);
            ((FenceConfig)fence.Config).Items.Add(Desk + name);
        }

        DesktopFences.Lay(home, screen, box);
        var history = new LayoutHistory(state);
        var cells = home.Widgets.Where(w => w.Config is IDesktopItemConfig).Select(w => w.Rect).ToList();

        // Removed: the icons it showed stay where they stood, free now.
        home.Widgets.Remove(fence);
        DesktopFences.PutBack(home, fence, screen, box, (76, 100), key => new Themia.Sdk.DesktopItem(key, key, DesktopItemKind.File), (i, r) =>
        {
            var w = WidgetInstance.Create(new WidgetId("desktopItem"), r);
            ((IDesktopItemConfig)w.Config).Item = i.Key;
            return w;
        });
        Assert.True(history.Commit(state, UndoAction.Delete));
        Assert.Equal(cells, home.Widgets.Where(w => w.Config is IDesktopItemConfig).Select(w => w.Rect));
        Assert.Equal(2, DesktopViews.Free(home).Count);

        Assert.Equal(UndoAction.Delete, history.Undo(state));
        var back = Assert.Single(home.Widgets, w => w.Id == fence.Id);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], ((FenceConfig)back.Config).Items);
        Assert.Empty(DesktopViews.Free(home));
    }

    [Fact]
    public void S10920_S13044_load_gives_a_fences_items_their_icons_and_none_is_free_beside()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.5, 0.1, 0.3, 0.3));
        ((FenceConfig)fence.Config).Items = [Desk + "a.txt", Desk + "b.txt"];
        var item = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.01, 0.01, 0.08, 0.125));
        ((IDesktopItemConfig)item.Config).Item = Desk + "a.txt";
        home.Widgets.AddRange([fence, item]);

        // Stored when a fence drew its own items: b.txt gets its icon (the real kind's), a.txt's is the fence's.
        Assert.NotEmpty(DesktopFences.Repair(state));
        Assert.Equal(3, home.Widgets.Count);
        Assert.Empty(DesktopViews.Free(home));
        Assert.Empty(DesktopFences.Repair(state));
    }
}
