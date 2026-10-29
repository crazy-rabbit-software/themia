using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5520]..[S-5523] [S-5506] The reconcile across views, the next free cell, what is shown and offered.</summary>
public sealed partial class DesktopLayerTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    /// <summary>An item widget's config as Core sees it: no module needed.</summary>
    private sealed class ItemConfig : WidgetConfig, IDesktopItemConfig
    {
        public string Item { get; set; } = string.Empty;

        public override WidgetConfig Clone() => new ItemConfig { Item = Item };
    }

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    private static readonly ScreenMetrics Screen = new(1000, 800);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Widget(string key, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new ItemConfig { Item = key }, Rect = rect };

    private static WidgetInstance Create(DesktopItem item, FracRect rect) => Widget(item.Key, rect);

    private static AppState TwoViews(out ViewState home, out ViewState work)
    {
        home = ViewState.Create("Home");
        work = ViewState.Create("Work");
        return new AppState { Views = [home, work], ActiveViewId = home.Id };
    }

    private static IEnumerable<string> Keys(ViewState view) => view.Widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => c.Item);

    [Fact]
    public void S5520_a_new_file_lands_in_the_target_view_only_at_the_first_free_cell()
    {
        var state = TwoViews(out var home, out var work);
        var listing = new[] { File("b.txt"), File("a.txt") };

        var plan = DesktopReconcile.Plan(state, listing, []);
        var unplaced = DesktopReconcile.Apply(state, plan, home.Id, Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], Keys(home));
        Assert.Empty(work.Widgets);

        // Down the first column from the top-left: a.txt in the first cell, b.txt under it.
        Assert.Equal(new FracRect(0, 0, 0.09, 0.125), home.Widgets[0].Rect);
        Assert.Equal(0.125, home.Widgets[1].Y, 6);
        Assert.Equal(0, home.Widgets[1].X, 6);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], state.Desktop.Known.Order());

        // Read again: nothing new, nothing to do.
        Assert.False(DesktopReconcile.Plan(state, listing, []).Changes(state));
    }

    [Fact]
    public void S5520_a_file_gone_from_disk_loses_its_widgets_in_every_view_and_others_stay()
    {
        var state = TwoViews(out var home, out var work);
        home.Widgets.Add(Widget(Desk + "gone.txt", new FracRect(0, 0, 0.1, 0.1)));
        work.Widgets.Add(Widget(Desk + "GONE.TXT", new FracRect(0.5, 0.5, 0.1, 0.1)));
        work.Widgets.Add(Widget(Desk + "kept.txt", new FracRect(0, 0, 0.1, 0.1)));
        work.Widgets.Add(new WidgetInstance { Id = "clock", Config = new OtherConfig(), Rect = new FracRect(0.2, 0, 0.2, 0.2) });
        state.Desktop.Known = [Desk + "gone.txt", Desk + "kept.txt"];

        var plan = DesktopReconcile.Plan(state, [File("kept.txt")], []);
        DesktopReconcile.Apply(state, plan, home.Id, Screen, (90, 100), Create);

        Assert.Empty(home.Widgets);
        Assert.Equal(["kept.txt", null], work.Widgets.Select(w => (w.Config as IDesktopItemConfig)?.Item.Replace(Desk, string.Empty, StringComparison.Ordinal)));
        Assert.Equal([Desk + "kept.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5520_a_rename_keeps_every_views_widgets_where_they_are()
    {
        var state = TwoViews(out var home, out var work);
        var where = new FracRect(0.3, 0.4, 0.09, 0.125);
        home.Widgets.Add(Widget(Desk + "old.txt", where));
        work.Widgets.Add(Widget(Desk + "old.txt", new FracRect(0, 0, 0.09, 0.125)));
        state.Desktop.Known = [Desk + "old.txt"];

        var plan = DesktopReconcile.Plan(state, [File("new.txt")], [(Desk + "old.txt", Desk + "new.txt")]);
        DesktopReconcile.Apply(state, plan, home.Id, Screen, (90, 100), Create);

        Assert.Equal([Desk + "new.txt"], Keys(home));
        Assert.Equal(where, home.Widgets[0].Rect);
        Assert.Equal([Desk + "new.txt"], Keys(work));
        Assert.Equal([Desk + "new.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5520_a_rename_away_from_the_desktop_is_a_removal()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.Add(Widget(Desk + "old.txt", new FracRect(0, 0, 0.1, 0.1)));
        state.Desktop.Known = [Desk + "old.txt"];

        var plan = DesktopReconcile.Plan(state, [], [(Desk + "old.txt", @"C:\Users\a\Documents\old.txt")]);
        DesktopReconcile.Apply(state, plan, home.Id, Screen, (90, 100), Create);

        Assert.Empty(home.Widgets);
        Assert.Empty(state.Desktop.Known);
    }

    [Fact]
    public void S5520_an_item_known_but_in_no_view_stays_unplaced()
    {
        var state = TwoViews(out var home, out var work);
        state.Desktop.Known = [Desk + "removed.txt"];

        var plan = DesktopReconcile.Plan(state, [File("removed.txt")], []);

        Assert.False(plan.Changes(state));
        Assert.Empty(home.Widgets);
        Assert.Equal([Desk + "removed.txt"], DesktopReconcile.Unplaced(work, [File("removed.txt")]).Select(i => i.Key));
    }

    [Fact]
    public void S5520_with_no_display_to_land_on_new_items_stay_unknown_for_the_next_read()
    {
        var state = TwoViews(out _, out _);

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], []);
        DesktopReconcile.Apply(state, plan, targetViewId: null, Screen, (90, 100), Create);

        Assert.Empty(state.Desktop.Known);
        Assert.Single(DesktopReconcile.Plan(state, [File("a.txt")], []).Arrivals);
    }

    [Fact]
    public void S5521_the_next_free_cell_skips_any_widget_and_a_full_area_has_none()
    {
        var cell = new FracRect(0, 0, 0.25, 0.5);
        var area = new FracRect(0, 0, 1, 1);

        Assert.Equal(new FracRect(0, 0, 0.25, 0.5), DesktopGrid.NextFree([], cell, area));

        // A clock over the top-left cell: the cell under it, then the next column.
        Assert.Equal(new FracRect(0, 0.5, 0.25, 0.5), DesktopGrid.NextFree([new FracRect(0.1, 0.1, 0.3, 0.2)], cell, area));
        Assert.Equal(new FracRect(0.25, 0, 0.25, 0.5), DesktopGrid.NextFree([new FracRect(0, 0, 0.2, 1)], cell, area));

        // Touching edges do not overlap.
        Assert.Equal(new FracRect(0, 0.5, 0.25, 0.5), DesktopGrid.NextFree([new FracRect(0, 0, 0.25, 0.5)], cell, area));

        Assert.Null(DesktopGrid.NextFree([new FracRect(0, 0, 1, 1)], cell, area));
    }

    [Fact]
    public void S5521_a_full_view_leaves_the_new_item_unplaced_but_known()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.Add(new WidgetInstance { Id = "wall", Config = new OtherConfig(), Rect = new FracRect(0, 0, 1, 1) });

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], []);
        var unplaced = DesktopReconcile.Apply(state, plan, home.Id, Screen, (90, 100), Create);

        Assert.Equal([Desk + "a.txt"], unplaced.Select(i => i.Key));
        Assert.Single(home.Widgets);
        Assert.Equal([Desk + "a.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5506_item_widgets_have_cards_only_while_themias_items_show()
    {
        var item = Widget(Desk + "a.txt", new FracRect(0, 0, 0.1, 0.1));
        var other = new WidgetInstance { Id = "clock", Config = new OtherConfig() };
        WidgetInstance[] widgets = [item, other];

        Assert.Equal([item, other], DesktopReconcile.Shown(widgets, showsItems: true));
        Assert.Equal([other], DesktopReconcile.Shown(widgets, showsItems: false));
    }

    [Fact]
    public void S5523_unplaced_lists_this_views_missing_items_in_desktop_order()
    {
        var view = ViewState.Create("v");
        view.Widgets.Add(Widget(Desk + "placed.txt", new FracRect(0, 0, 0.1, 0.1)));
        DesktopItem[] listing = [File("placed.txt"), File("z.txt"), new(Desk + "Folder", "Folder", DesktopItemKind.Folder)];

        Assert.Equal(["Folder", "z.txt"], DesktopReconcile.Unplaced(view, listing).Select(i => i.Name));
    }
}
