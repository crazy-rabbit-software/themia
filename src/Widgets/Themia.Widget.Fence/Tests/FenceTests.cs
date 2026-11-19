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
        Assert.Equal((config.Name, config.RolledUp, config.OpenHeight, config.Sort, config.RollUpOnClick, config.HoverPeek), (copy.Name, copy.RolledUp, copy.OpenHeight, copy.Sort, copy.RollUpOnClick, copy.HoverPeek));
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
        Assert.False(back.HoverPeek);

        // A new fence: as placed, rolling up on a click, opening under the pointer, no rules.
        var fresh = Assert.IsType<FenceConfig>(JsonSerializer.Deserialize(JsonSerializer.Serialize<WidgetConfig>(new FenceConfig(), StateJson.WidgetConfig), StateJson.WidgetConfig));
        Assert.Null(fresh.Sort);
        Assert.True(fresh.RollUpOnClick);
        Assert.True(fresh.HoverPeek);
        Assert.False(fresh.Rules.Any);
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

        Assert.True(open.Natural.Height > FenceGrid.TitleHeight + 100);
        Assert.True(open.Max.Height >= SizeStretch.Ceiling);
        Assert.Equal((FenceGrid.TitleHeight, FenceGrid.TitleHeight, FenceGrid.TitleHeight), (rolled.Min.Height, rolled.Natural.Height, rolled.Max.Height));
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

        // Into the fence: one step of the arrangement.
        DesktopFences.MoveIn(home, fence.Id, [Desk + "a.txt"], 0);
        Assert.True(history.Commit(state, UndoAction.Move));
        Assert.Single(home.Widgets);

        // Its undo: the free icon back where it stood and the fence empty again; its redo: in the fence again.
        Assert.Equal(UndoAction.Move, history.Undo(state));
        Assert.Equal(2, home.Widgets.Count);
        Assert.Empty(((FenceConfig)home.Widgets.Single(w => w.Id == fence.Id).Config).Items);
        Assert.Equal(item.Rect, home.Widgets.Single(w => w.Id == item.Id).Rect);
        Assert.Equal(UndoAction.Move, history.Redo(state));
        Assert.Equal([Desk + "a.txt"], ((FenceConfig)home.Widgets.Single().Config).Items);
    }

    [Fact]
    public void S10919_a_removed_fences_undo_brings_it_back_with_its_items_and_takes_their_free_icons_away()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.5, 0.1, 0.3, 0.3));
        ((FenceConfig)fence.Config).Items = [Desk + "a.txt", Desk + "b.txt"];
        home.Widgets.Add(fence);
        var history = new LayoutHistory(state);

        home.Widgets.Remove(fence);
        DesktopFences.LayFree(home, [new Themia.Sdk.DesktopItem(Desk + "a.txt", "a.txt", DesktopItemKind.File), new Themia.Sdk.DesktopItem(Desk + "b.txt", "b.txt", DesktopItemKind.File)],
            fence.X, fence.Y, new ScreenMetrics(1000, 800), (90, 100), (i, r) =>
            {
                var w = WidgetInstance.Create(new WidgetId("desktopItem"), r);
                ((IDesktopItemConfig)w.Config).Item = i.Key;
                return w;
            });
        Assert.True(history.Commit(state, UndoAction.Delete));
        Assert.Equal(2, home.Widgets.Count);

        Assert.Equal(UndoAction.Delete, history.Undo(state));
        var back = Assert.Single(home.Widgets);
        Assert.Equal(fence.Id, back.Id);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], ((FenceConfig)back.Config).Items);
    }

    [Fact]
    public void S10920_load_keeps_no_item_both_free_and_in_a_fence_of_one_view()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = WidgetInstance.Create(new WidgetId("fence"), new FracRect(0.5, 0.1, 0.3, 0.3));
        ((FenceConfig)fence.Config).Items = [Desk + "a.txt"];
        var item = WidgetInstance.Create(new WidgetId("desktopItem"), new FracRect(0.01, 0.01, 0.08, 0.125));
        ((IDesktopItemConfig)item.Config).Item = Desk + "a.txt";
        home.Widgets.AddRange([fence, item]);

        Assert.NotEmpty(DesktopFences.Repair(state));
        Assert.Equal([fence.Id], home.Widgets.Select(w => w.Id));
    }
}
