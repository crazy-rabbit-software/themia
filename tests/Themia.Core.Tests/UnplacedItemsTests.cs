using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-13700]..[S-13708] The Unplaced icons page's rules: what it lists, its search, its badge, its fences, a drag's block.</summary>
public sealed class UnplacedItemsTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private static readonly ScreenMetrics Screen = new(1000, 800);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Icon(string name, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = Desk + name }, Rect = rect };

    private static WidgetInstance Fence(string id, FenceSource source = FenceSource.Held, params string[] names)
        => new() { Id = id, Config = new FenceFake { Items = [.. names.Select(n => Desk + n)], Source = source, Name = id }, Rect = new FracRect(0.5, 0.1, 0.3, 0.3) };

    private static WidgetInstance Create(DesktopItem item, FracRect rect) => new() { Id = Guid.NewGuid().ToString("n"), Config = new DeskIconFake { Item = item.Key }, Rect = rect };

    private static IEnumerable<string> Names(IEnumerable<DesktopItem> items) => items.Select(i => i.Name);

    [Fact]
    public void S13700_it_lists_what_the_view_shows_nowhere_neither_free_nor_in_a_fence()
    {
        // Owner, 2026-11-19: "a page with the desktop icons that are on the physical desktop folder but are not placed on
        // the given view".
        var home = ViewState.Create("Home");
        home.Widgets.Add(Icon("b.txt", new FracRect(0, 0, 0.09, 0.125)));
        home.Widgets.Add(Fence("f", FenceSource.Held, "c.txt"));
        DesktopItem[] listing = [File("d.txt"), File("c.txt"), File("b.txt"), File("a.txt")];

        Assert.Equal(["a.txt", "d.txt"], Names(UnplacedItems.Of(home, listing)));

        // Per view: another view lists what it lacks itself.
        var work = ViewState.Create("Work");
        Assert.Equal(["a.txt", "b.txt", "c.txt", "d.txt"], Names(UnplacedItems.Of(work, listing)));

        // A view that shows no desktop icons has nowhere to place one: none.
        work.DesktopIcons = ViewDesktopIcons.None;
        Assert.Empty(UnplacedItems.Of(work, listing));
    }

    [Fact]
    public void S13700_it_follows_the_desktop_and_the_view_as_they_change()
    {
        var home = ViewState.Create("Home");
        List<DesktopItem> listing = [File("a.txt")];
        Assert.Single(UnplacedItems.Of(home, listing));

        // A file arrives in the desktop folder: listed; placed on the view: gone; its icon removed again: back.
        listing.Add(File("b.txt"));
        Assert.Equal(["a.txt", "b.txt"], Names(UnplacedItems.Of(home, listing)));
        Assert.Empty(DesktopViews.Fill(home, [listing[0]], Screen, (90, 100), Create));
        Assert.Equal(["b.txt"], Names(UnplacedItems.Of(home, listing)));
        DesktopViews.Hide(home, [Desk + "a.txt"]);
        Assert.Equal(["a.txt", "b.txt"], Names(UnplacedItems.Of(home, listing)));

        // A file gone from the desktop leaves the list.
        listing.RemoveAt(1);
        Assert.Equal(["a.txt"], Names(UnplacedItems.Of(home, listing)));
    }

    [Fact]
    public void S13702_a_long_list_has_a_search_that_matches_every_word_in_any_case()
    {
        Assert.False(UnplacedItems.Searches(UnplacedItems.SearchFrom - 1));
        Assert.True(UnplacedItems.Searches(UnplacedItems.SearchFrom));

        DesktopItem[] items = [File("Budget 2026.xlsx"), File("Holiday photos"), File("budget notes.txt")];
        Assert.Equal(["Budget 2026.xlsx", "budget notes.txt"], Names(UnplacedItems.Matching(items, "BUDGET")));
        Assert.Equal(["budget notes.txt"], Names(UnplacedItems.Matching(items, " notes  budget ")));
        Assert.Same(items, UnplacedItems.Matching(items, "   "));
        Assert.Empty(UnplacedItems.Matching(items, "zebra"));
    }

    [Fact]
    public void S13701_the_entrys_badge_counts_up_to_99_and_says_nothing_for_none()
    {
        Assert.Null(UnplacedItems.Badge(0));
        Assert.Equal("1", UnplacedItems.Badge(1));
        Assert.Equal("99", UnplacedItems.Badge(99));
        Assert.Equal("99+", UnplacedItems.Badge(100));
    }

    [Fact]
    public void S13704_its_items_go_into_any_fence_of_the_view_but_a_search_fence()
    {
        var home = ViewState.Create("Home");
        home.Widgets.Add(Fence("search", FenceSource.Search));
        home.Widgets.Add(Fence("docs"));
        DesktopItem[] listing = [File("a.txt"), File("b.txt")];

        var fences = UnplacedItems.Fences(home);
        Assert.Equal(["docs"], fences.Select(f => f.Widget.Id));

        // Put in: it is shown on the view now, in the fence, and leaves the list.
        // [S-13040] As AppHost.PlaceInFence: the item gets its icon on the view, the fence's cell its place.
        DesktopFences.MoveIn(home, "docs", [Desk + "b.txt"], int.MaxValue, key => Icon(key[Desk.Length..], new FracRect(0, 0, 0.09, 0.125)));
        Assert.Equal(["a.txt"], Names(UnplacedItems.Of(home, listing)));
        Assert.Equal([Desk + "b.txt"], ((FenceFake)home.Widgets[1].Config).Items);
    }

    [Fact]
    public void S13703_a_press_acts_on_the_selection_that_holds_it_else_on_its_item_alone()
    {
        IReadOnlyList<string> selected = [Desk + "a.txt", Desk + "c.txt"];
        Assert.Equal(selected, UnplacedItems.Acting(selected, Desk + "C.TXT"));
        Assert.Equal([Desk + "b.txt"], UnplacedItems.Acting(selected, Desk + "b.txt"));
        Assert.Equal([Desk + "b.txt"], UnplacedItems.Acting([], Desk + "b.txt"));
    }

    [Fact]
    public void S13703_placing_several_lays_them_at_the_next_free_cells_as_the_desktops_arrange()
    {
        var home = ViewState.Create("Home");
        home.Widgets.Add(Icon("standing.txt", new FracRect(0, 0, 0.09, 0.125)));
        DesktopItem[] picked = [File("c.txt"), File("a.txt"), File("b.txt")];

        Assert.Empty(DesktopViews.Fill(home, picked, Screen, (90, 100), Create));

        // In the desktop's order, at the first free cells in reading order (Auto arrange's and an arrival's, [S-11876]):
        // along the top row after the icon already there, none over another.
        var laid = home.Widgets.Skip(1).ToList();
        Assert.Equal([Desk + "a.txt", Desk + "b.txt", Desk + "c.txt"], laid.Select(w => ((DeskIconFake)w.Config).Item));
        Assert.All(laid, w => Assert.True(Math.Abs(w.Rect.Y - laid[0].Rect.Y) < 1e-9, $"{w.Rect} not on the row of {laid[0].Rect}"));
        Assert.All(laid, w => Assert.True(DesktopGrid.Free(w.Rect, [home.Widgets[0].Rect]), $"{w.Rect} over the icon standing"));
        Assert.True(laid[1].Rect.X >= laid[0].Rect.X + laid[0].Rect.W - 1e-9 && laid[2].Rect.X >= laid[1].Rect.X + laid[1].Rect.W - 1e-9);
    }

    [Fact]
    public void S13705_a_drag_carries_its_icons_as_a_block_of_rows_four_wide()
    {
        var block = UnplacedItems.Block(6, (0.1, 0.2));

        Assert.Equal(6, block.Count);
        Assert.Equal(new FracRect(0, 0, 0.1, 0.2), block[0]);
        Assert.Equal(0.3, block[3].X, 9);
        Assert.Equal(0, block[3].Y, 9);
        Assert.Equal(0, block[4].X, 9);
        Assert.Equal(0.2, block[4].Y, 9);
        Assert.Equal(0.1, block[5].X, 9);
        Assert.Equal([new FracRect(0, 0, 0.1, 0.2)], UnplacedItems.Block(1, (0.1, 0.2)));
        Assert.Empty(UnplacedItems.Block(0, (0.1, 0.2)));
    }
}