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

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    private static readonly ScreenMetrics Screen = new(1000, 800);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Widget(string key, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = key }, Rect = rect };

    /// <summary>An own icon's widget: no key, what it opens held by itself.</summary>
    private static WidgetInstance OwnIcon(string target, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Target = target }, Rect = rect };

    private static WidgetInstance Create(DesktopItem item, FracRect rect) => Widget(item.Key, rect);

    private static AppState TwoViews(out ViewState home, out ViewState work)
    {
        home = ViewState.Create("Home");
        work = ViewState.Create("Work");
        return new AppState { Views = [home, work], ActiveViewId = home.Id };
    }

    private static IEnumerable<string> Keys(ViewState view) => view.Widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => c.Item);

    [Fact]
    public void S5520_S5526_a_new_file_lands_on_the_all_view_at_the_first_free_cell_never_on_a_chosen_one()
    {
        var state = TwoViews(out var home, out var work);
        work.DesktopIcons = ViewDesktopIcons.Chosen;
        var listing = new[] { File("b.txt"), File("a.txt") };

        var plan = DesktopReconcile.Plan(state, listing, []);
        var unplaced = DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], Keys(home));
        Assert.Empty(work.Widgets);

        // [S-11876] In reading order from the top-left on the widget grid, one cell in ([S-11888]): a.txt first, b.txt beside it.
        Assert.Equal(0.01, home.Widgets[0].Rect.X, 9);
        Assert.Equal(0.0125, home.Widgets[0].Rect.Y, 9);
        Assert.Equal((0.09, 0.125), (home.Widgets[0].Rect.W, home.Widgets[0].Rect.H));
        Assert.Equal(0.0125, home.Widgets[1].Y, 6);
        Assert.Equal(0.10, home.Widgets[1].X, 6);
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
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

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
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

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
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

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
        DesktopReconcile.Apply(state, plan, default, (90, 100), Create);

        Assert.Empty(state.Desktop.Known);
        Assert.Single(DesktopReconcile.Plan(state, [File("a.txt")], []).Arrivals);
    }





    [Fact]
    public void S5521_a_full_view_leaves_the_new_item_unplaced_but_known()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.Add(new WidgetInstance { Id = "wall", Config = new OtherConfig(), Rect = new FracRect(0, 0, 1, 1) });

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], []);
        var unplaced = DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal([Desk + "a.txt"], unplaced.Select(i => i.Key));
        Assert.Single(home.Widgets);
        Assert.Equal([Desk + "a.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5520_an_own_icon_is_never_gone_renamed_or_rewritten_by_the_reconcile()
    {
        var state = TwoViews(out var home, out var work);
        var own = OwnIcon(Desk + "gone.txt", new FracRect(0.5, 0.5, 0.1, 0.1));
        var waiting = OwnIcon(string.Empty, new FracRect(0.7, 0.5, 0.1, 0.1));
        var layout = OwnIcon(string.Empty, new FracRect(0.3, 0.5, 0.1, 0.1));
        ((DeskIconFake)layout.Config).Layout = LayoutNode.NewPane();
        home.Widgets.AddRange([own, waiting, Widget(Desk + "gone.txt", new FracRect(0, 0, 0.1, 0.1))]);
        work.Widgets.AddRange([layout, Widget(Desk + "old.txt", new FracRect(0, 0, 0.1, 0.1))]);
        state.Desktop.Known = [Desk + "gone.txt", Desk + "old.txt"];

        // The listing lost gone.txt (an own icon points at that very path) and old.txt was renamed.
        var plan = DesktopReconcile.Plan(state, [File("new.txt")], [(Desk + "old.txt", Desk + "new.txt")]);

        Assert.Single(plan.Gone);
        Assert.DoesNotContain(own.Id, plan.Gone);
        Assert.DoesNotContain(waiting.Id, plan.Gone);
        Assert.DoesNotContain(layout.Id, plan.Gone);

        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal([own, waiting], home.Widgets);
        Assert.Equal(2, work.Widgets.Count);
        Assert.Same(layout, work.Widgets[0]);
        Assert.Equal([Desk + "new.txt"], Keys(work).Where(key => key.Length > 0));
        foreach (var icon in new[] { own, waiting, layout })
        {
            Assert.Equal(string.Empty, ((DeskIconFake)icon.Config).Item);
        }

        Assert.Equal(Desk + "gone.txt", ((DeskIconFake)own.Config).Target);
        Assert.Equal(new FracRect(0.5, 0.5, 0.1, 0.1), own.Rect);
        Assert.Equal([Desk + "new.txt"], state.Desktop.Known);
    }

    [Fact]
    public void S5520_a_rename_from_an_own_icons_target_path_touches_no_own_icon()
    {
        var state = TwoViews(out var home, out _);
        var own = OwnIcon(Desk + "old.txt", new FracRect(0.5, 0.5, 0.1, 0.1));
        home.Widgets.Add(own);

        // Nothing known and no item widget holds old.txt: the rename touches nothing, and the listed file arrives.
        var plan = DesktopReconcile.Plan(state, [File("new.txt")], [(Desk + "old.txt", Desk + "new.txt")]);

        Assert.Empty(plan.Renames);
        Assert.Empty(plan.Gone);
        Assert.Equal([Desk + "new.txt"], plan.Arrivals.Select(i => i.Key));

        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal(Desk + "old.txt", ((DeskIconFake)own.Config).Target);
        Assert.Equal(string.Empty, ((DeskIconFake)own.Config).Item);
        Assert.Equal(2, home.Widgets.Count);
    }

    [Fact]
    public void S5520_only_own_icons_and_an_empty_listing_is_nothing_to_do()
    {
        var state = TwoViews(out var home, out _);
        home.Widgets.Add(OwnIcon(@"C:\Tools\app.exe", new FracRect(0, 0, 0.1, 0.1)));
        home.Widgets.Add(OwnIcon(string.Empty, new FracRect(0.2, 0, 0.1, 0.1)));

        var plan = DesktopReconcile.Plan(state, [], []);

        Assert.False(plan.Changes(state));
        Assert.Empty(plan.Gone);
    }

    [Fact]
    public void S5523_unplaced_ignores_own_icons_even_one_whose_target_is_a_listed_item()
    {
        var view = ViewState.Create("v");
        view.Widgets.Add(OwnIcon(Desk + "a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(OwnIcon(string.Empty, new FracRect(0.2, 0, 0.1, 0.1)));
        view.Widgets.Add(Widget(Desk + "placed.txt", new FracRect(0.4, 0, 0.1, 0.1)));

        Assert.Equal(["a.txt"], DesktopReconcile.Unplaced(view, [File("a.txt"), File("placed.txt")]).Select(i => i.Name));
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
