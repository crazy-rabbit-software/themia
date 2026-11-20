using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>A fence's config as Core sees it, no module needed.</summary>
internal sealed class FenceFake : WidgetConfig, IHoldsDesktopItems
{
    public List<string> Items { get; set; } = [];

    public FenceRuleSet Rules { get; set; } = new();

    public bool RolledUp { get; set; }

    public double OpenHeight { get; set; }

    public int Scroll { get; set; }

    public DesktopSortBy? Sort { get; set; }

    public string Name { get; set; } = string.Empty;

    public FenceSource Source { get; set; }

    public FenceQuery Query { get; set; } = new();

    public override WidgetConfig Clone() => new FenceFake { Items = [.. Items], Rules = Rules.Clone(), RolledUp = RolledUp, OpenHeight = OpenHeight, Scroll = Scroll, Sort = Sort, Name = Name, Source = Source, Query = Query.Clone() };
}

/// <summary>[S-13000]..[S-13014] The fences' rules: membership, routing, the reconcile, making one, taking it away, load.</summary>
public sealed class DesktopFencesTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly ScreenMetrics Screen = new(1000, 800);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Icon(string name, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = Desk + name }, Rect = rect };

    private static WidgetInstance Fence(string id, params string[] names)
        => new() { Id = id, Config = new FenceFake { Items = [.. names.Select(n => Desk + n)] }, Rect = new FracRect(0.5, 0.1, 0.3, 0.3) };

    private static FenceFake F(WidgetInstance fence) => (FenceFake)fence.Config;

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    private static bool Overlaps(FracRect a, FracRect b)
        => a.X < b.X + b.W - 1e-9 && b.X < a.X + a.W - 1e-9 && a.Y < b.Y + b.H - 1e-9 && b.Y < a.Y + a.H - 1e-9;

    private static WidgetInstance Create(DesktopItem item, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = item.Key }, Rect = rect };

    /// <summary>[S-13042] A new icon for a key, as the host makes one for an item put into a fence.</summary>
    private static WidgetInstance Make(string key) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = key }, Rect = new FracRect(0, 0, 0, 0) };

    /// <summary>[S-13042] The free icons: item widgets no fence holds.</summary>
    private static IEnumerable<string> FreeKeys(ViewState view) => DesktopViews.Free(view).Select(k => k.Replace(Desk, string.Empty, StringComparison.Ordinal)).Order();

    private static IEnumerable<string> Names(WidgetInstance fence) => F(fence).Items.Select(k => k.Replace(Desk, string.Empty, StringComparison.Ordinal));

    // ---- membership ([S-13001]) -------------------------------------------------------------------------------------

    [Fact]
    public void S13001_a_fences_items_are_shown_on_its_view_and_never_offered_or_laid_free_there()
    {
        var home = ViewState.Create("Home");
        home.Widgets.Add(Fence("f", "a.txt"));
        home.Widgets.Add(Icon("b.txt", new FracRect(0, 0, 0.09, 0.125)));

        Assert.Equal([Desk + "a.txt", Desk + "b.txt"], DesktopViews.Shown(home).Order());
        Assert.Equal([Desk + "b.txt"], DesktopViews.Free(home));
        Assert.Empty(DesktopReconcile.Unplaced(home, [File("a.txt"), File("b.txt")]));

        // Set to All: what it lacks is laid in, never the fenced item a second time.
        home.DesktopIcons = ViewDesktopIcons.Chosen;
        DesktopViews.Set(home, ViewDesktopIcons.All);
        Assert.Empty(DesktopViews.Fill(home, [File("a.txt"), File("b.txt")], Screen, (90, 100), Create));
        Assert.Equal(["b.txt"], FreeKeys(home));
    }

    [Fact]
    public void S13001_a_view_set_to_none_keeps_its_fences_items()
    {
        var home = ViewState.Create("Home");
        var fence = Fence("f", "a.txt");
        home.Widgets.Add(fence);
        home.Widgets.Add(Icon("b.txt", new FracRect(0, 0, 0.09, 0.125)));

        DesktopViews.Set(home, ViewDesktopIcons.None);

        Assert.Empty(FreeKeys(home));
        Assert.Equal(["a.txt"], Names(fence));
        Assert.Empty(DesktopViews.Repair(home));
    }

    [Fact]
    public void S13005_items_moved_into_a_fence_leave_the_free_desktop_and_other_fences_of_that_view_only()
    {
        var home = ViewState.Create("Home");
        var work = ViewState.Create("Work");
        var target = Fence("f", "x.txt", "y.txt");
        var other = Fence("g", "b.txt");
        home.Widgets.AddRange([target, other, Icon("a.txt", new FracRect(0, 0, 0.09, 0.125))]);
        work.Widgets.Add(Icon("a.txt", new FracRect(0, 0, 0.09, 0.125)));

        var added = DesktopFences.MoveIn(home, "f", [Desk + "a.txt", Desk + "b.txt"], 1, Make);

        Assert.Equal(2, added);
        Assert.Equal(["x.txt", "a.txt", "b.txt", "y.txt"], Names(target));
        Assert.Empty(Names(other));
        Assert.Empty(FreeKeys(home));

        // [S-13042] The free icon is the fence's now (kept, laid on its cell); one with none got one.
        Assert.Equal(2, home.Widgets.Count(w => w.Config is IDesktopItemConfig));

        // Another view keeps its own free icon: a fence lives on one view.
        Assert.Equal(["a.txt"], FreeKeys(work));
    }

    [Fact]
    public void S13005_a_move_inside_a_fence_lands_where_it_was_pointed_at()
    {
        var home = ViewState.Create("Home");
        var fence = Fence("f", "a", "b", "c", "d");
        home.Widgets.Add(fence);

        // "a" dropped before "d" (index 3 of the order as shown): it goes between c and d.
        Assert.Equal(0, DesktopFences.MoveIn(home, "f", [Desk + "a"], 3, Make));
        Assert.Equal(["b", "c", "a", "d"], Names(fence));

        // To the end, and to the front.
        DesktopFences.MoveIn(home, "f", [Desk + "b"], 99, Make);
        Assert.Equal(["c", "a", "d", "b"], Names(fence));
        DesktopFences.MoveIn(home, "f", [Desk + "d"], -5, Make);
        Assert.Equal(["d", "c", "a", "b"], Names(fence));
    }

    [Fact]
    public void S13005_a_move_into_a_fence_that_is_not_on_the_view_changes_nothing()
    {
        var home = ViewState.Create("Home");
        home.Widgets.Add(Icon("a.txt", new FracRect(0, 0, 0.09, 0.125)));

        Assert.Equal(0, DesktopFences.MoveIn(home, "nope", [Desk + "a.txt"], 0));
        Assert.Equal(["a.txt"], FreeKeys(home));
    }

    // ---- out of a fence ([S-13006], [S-13009]) ----------------------------------------------------------------------

    [Fact]
    public void S13006_items_taken_out_of_a_fence_arrive_free_at_the_drop_point_on_the_grid()
    {
        var home = ViewState.Create("Home");
        var fence = Fence("f", "a.txt", "b.txt");
        home.Widgets.Add(fence);

        var left = DesktopFences.TakeOut(home, [Desk + "a.txt"]);
        var unplaced = DesktopFences.LayFree(home, [File("a.txt")], 0.2, 0.5, Screen, (90, 100), Create);

        Assert.Equal([Desk + "a.txt"], left);
        Assert.Empty(unplaced);
        Assert.Equal(["b.txt"], Names(fence));
        var icon = Assert.Single(home.Widgets, w => w.Config is IDesktopItemConfig);

        // [S-11876] Centred on the point on the grid: its box round (200, 400) DIPs.
        Assert.InRange((icon.Rect.X + (icon.Rect.W / 2)) * Screen.Width, 190, 210);
        Assert.InRange((icon.Rect.Y + (icon.Rect.H / 2)) * Screen.Height, 390, 410);
        Assert.False(Overlaps(icon.Rect, fence.Rect));
    }

    [Fact]
    public void S13009_put_back_lays_the_items_at_the_free_spots_nearest_where_the_fence_stood_none_on_another()
    {
        var home = ViewState.Create("Home");
        var fence = Fence("f", "a.txt", "b.txt", "c.txt");
        home.Widgets.Add(fence);
        var wall = new WidgetInstance { Id = "clock", Config = new OtherConfig(), Rect = new FracRect(0, 0, 0.45, 1) };
        home.Widgets.Add(wall);
        home.Widgets.Remove(fence);

        var unplaced = DesktopFences.LayFree(home, [File("a.txt"), File("b.txt"), File("c.txt")], fence.X + 0.045, fence.Y + 0.0625, Screen, (90, 100), Create);

        Assert.Empty(unplaced);
        var icons = home.Widgets.Where(w => w.Config is IDesktopItemConfig).ToList();
        Assert.Equal(3, icons.Count);
        Assert.All(icons, i => Assert.False(Overlaps(i.Rect, wall.Rect)));
        Assert.All(icons, i => Assert.True(i.Rect.X >= 0.45 - 1e-9));
        for (var i = 0; i < icons.Count; i++)
        {
            for (var j = i + 1; j < icons.Count; j++)
            {
                Assert.False(Overlaps(icons[i].Rect, icons[j].Rect));
            }
        }
    }

    // ---- making one ([S-13007], [S-13008]) --------------------------------------------------------------------------

    [Fact]
    public void S13007_a_drawn_rectangle_becomes_whole_grid_cells_at_least_the_kinds_minimum()
    {
        var drawn = new FracRect(0.1234, 0.2345, 0.2001, 0.0101);

        var rect = DesktopFences.FromRect(drawn, new SizeDip(120, 140), Screen)!.Value;

        var (cols, rows) = GridGeometry.GridDims(Screen);
        Assert.Equal(Math.Round(rect.X * cols), rect.X * cols, 6);
        Assert.Equal(Math.Round(rect.Y * rows), rect.Y * rows, 6);
        Assert.Equal(Math.Round(rect.W * cols), rect.W * cols, 6);
        Assert.Equal(Math.Round(rect.H * rows), rect.H * rows, 6);
        Assert.True(rect.X <= drawn.X && rect.Y <= drawn.Y);
        Assert.True(rect.W >= drawn.W && rect.H * Screen.Height >= 140 - 1e-6);
        Assert.Null(DesktopFences.FromRect(drawn, new SizeDip(120, 140), new ScreenMetrics(0, 0)));
    }

    [Fact]
    public void S13008_a_fence_round_a_selection_holds_its_icons_box_with_the_title_bar_above()
    {
        var cell = new SizeDip(76, 100);
        FracRect[] icons = [new(0.10, 0.25, 0.08, 0.125), new(0.18, 0.25, 0.08, 0.125), new(0.10, 0.375, 0.08, 0.125)];

        var rect = DesktopFences.AroundIcons(icons, 3, cell, Screen)!.Value;

        Assert.True(rect.X <= 0.10 + 1e-9);
        Assert.True(rect.Y * Screen.Height <= (0.25 * Screen.Height) - (FenceCells.TitleHeight(Screen) * Screen.Height) + 1e-6);
        Assert.True((rect.X + rect.W) >= 0.26 - 1e-9);
        Assert.True((rect.Y + rect.H) >= 0.5 - 1e-9);

        // At least what shows the three without scrolling.
        var need = FenceGrid.Around(3, cell);
        Assert.True(rect.W * Screen.Width >= need.Width - 1e-6);
        Assert.True(rect.H * Screen.Height >= need.Height - FenceGrid.TitleDip + (FenceCells.TitleHeight(Screen) * Screen.Height) - 1e-6);
        Assert.Null(DesktopFences.AroundIcons([], 0, cell, Screen));
    }

    // ---- rules ([S-13004]) ------------------------------------------------------------------------------------------

    [Fact]
    public void S13004_a_new_item_goes_to_the_first_fence_whose_rules_take_it_on_each_view()
    {
        var home = ViewState.Create("Home");
        var work = ViewState.Create("Work");
        var state = new AppState { Views = [home, work], ActiveViewId = home.Id };
        var pictures = Fence("pics");
        F(pictures).Rules.Types.Add(FenceFileType.Images);
        var anything = Fence("any");
        F(anything).Rules.NameContains = "photo";
        home.Widgets.AddRange([pictures, anything]);

        var plan = DesktopReconcile.Plan(state, [File("holiday photo.jpg"), File("photo list.txt"), File("plain.txt")], []);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        // Home: the picture to the first matching fence, the list to the name rule's, the rest free.
        Assert.Equal(["holiday photo.jpg"], Names(pictures));
        Assert.Equal(["photo list.txt"], Names(anything));
        Assert.Equal(["plain.txt"], FreeKeys(home));

        // Work has no fence: every item arrives free, as an All view's.
        Assert.Equal(["holiday photo.jpg", "photo list.txt", "plain.txt"], FreeKeys(work).Order());
    }

    [Fact]
    public void S13004_a_fences_rule_takes_new_items_on_a_chosen_or_none_view_too()
    {
        var home = ViewState.Create("Home");
        home.DesktopIcons = ViewDesktopIcons.None;
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var docs = Fence("docs");
        F(docs).Rules.Types.Add(FenceFileType.Documents);
        home.Widgets.Add(docs);

        DesktopReconcile.Apply(state, DesktopReconcile.Plan(state, [File("a.pdf"), File("b.png")], []), Screen, (90, 100), Create);

        Assert.Equal(["a.pdf"], Names(docs));
        Assert.Empty(FreeKeys(home));
    }

    [Fact]
    public void S13005_a_drop_on_a_fence_lands_there_whatever_its_rules_say()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var target = Fence("target");
        var images = Fence("images");
        F(images).Rules.Types.Add(FenceFileType.Images);
        home.Widgets.AddRange([images, target]);
        var plan = DesktopReconcile.Plan(state, [File("x.png")], []);

        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create, [new DesktopLandingSpot(home.Id, null, null, Fence: "target")], _ => Screen);

        Assert.Equal(["x.png"], Names(target));
        Assert.Empty(Names(images));
        Assert.Empty(FreeKeys(home));
    }

    [Fact]
    public void S13004_a_drop_at_a_point_of_the_free_desktop_stays_free_there()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var images = Fence("images");
        F(images).Rules.Types.Add(FenceFileType.Images);
        home.Widgets.Add(images);

        DesktopReconcile.Apply(state, DesktopReconcile.Plan(state, [File("x.png")], []), Screen, (90, 100), Create, [new DesktopLandingSpot(home.Id, 0.2, 0.5)], _ => Screen);

        Assert.Equal(["x.png"], FreeKeys(home));
        Assert.Empty(Names(images));
    }

    // ---- the reconcile ([S-13003]) ----------------------------------------------------------------------------------

    [Fact]
    public void S13003_a_rename_in_explorer_keeps_the_item_in_its_place_in_the_fence()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = Fence("f", "a.txt", "b.txt", "c.txt");
        home.Widgets.Add(fence);
        state.Desktop.Known = [Desk + "a.txt", Desk + "b.txt", Desk + "c.txt"];

        var plan = DesktopReconcile.Plan(state, [File("a.txt"), File("B2.txt"), File("c.txt")], [(Desk + "b.txt", Desk + "B2.txt")]);
        Assert.True(plan.Changes(state));
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal(["a.txt", "B2.txt", "c.txt"], Names(fence));
        Assert.Empty(FreeKeys(home));
    }

    [Fact]
    public void S13003_an_item_deleted_in_explorer_leaves_its_fence_and_one_being_put_back_stays()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        var fence = Fence("f", "a.txt", "gone.txt", "back.txt");
        home.Widgets.Add(fence);
        state.Desktop.Known = [Desk + "a.txt", Desk + "gone.txt", Desk + "back.txt"];
        var expected = new HashSet<string>([Desk + "back.txt"], DesktopItemKeys.Comparer);

        var plan = DesktopReconcile.Plan(state, [File("a.txt")], [], expected);
        DesktopReconcile.Apply(state, plan, Screen, (90, 100), Create);

        Assert.Equal(["a.txt", "back.txt"], Names(fence));
        Assert.False(DesktopReconcile.Plan(state, [File("a.txt"), File("back.txt")], []).Changes(state));
    }

    // ---- load ([S-13002]) -------------------------------------------------------------------------------------------

    [Fact]
    public void S13002_S13044_load_drops_bad_and_repeated_items_the_first_fence_wins_and_a_held_item_gets_its_icon()
    {
        var home = ViewState.Create("Home");
        var work = ViewState.Create("Work");
        var state = new AppState { Views = [home, work], ActiveViewId = home.Id };
        var first = Fence("f", "a.txt", "a.txt", "b.txt");
        F(first).Items.Add("   ");
        F(first).Items.Add("bad\u0001key");
        var second = Fence("g", "A.TXT", "c.txt");
        F(second).OpenHeight = double.NaN;
        F(second).Sort = (DesktopSortBy)77;
        F(second).Rules.Extensions = ["PDF", "*.docx", "bad.ext", "pdf"];
        home.Widgets.AddRange([first, second, Icon("c.txt", new FracRect(0, 0, 0.09, 0.125)), Icon("d.txt", new FracRect(0.2, 0, 0.09, 0.125))]);

        // Another view may hold the same item in a fence of its own.
        var elsewhere = Fence("h", "a.txt");
        work.Widgets.Add(elsewhere);

        var repairs = DesktopFences.Repair(state);

        Assert.NotEmpty(repairs);
        Assert.Equal(["a.txt", "b.txt"], Names(first));
        Assert.Equal(["c.txt"], Names(second));
        Assert.Equal(["d.txt"], FreeKeys(home));

        Assert.Equal(["a.txt"], Names(elsewhere));
        Assert.Equal(0, F(second).OpenHeight);
        Assert.Null(F(second).Sort);
        Assert.Equal([".pdf", ".docx"], F(second).Rules.Extensions);

        // Whole now: a second pass says nothing.
        Assert.Empty(DesktopFences.Repair(state));
    }
}
