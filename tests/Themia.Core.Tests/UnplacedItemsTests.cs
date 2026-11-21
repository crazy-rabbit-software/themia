using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
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

    [Fact]
    public void S13707_the_flyout_is_exactly_as_wide_as_the_bar_and_still_points_at_its_entry()
    {
        // Owner, 2026-11-19: "the on desktop not on home view should be as wide as the edit bar".
        Assert.True(SettingsPages.BarWide(SettingsPageId.DesktopItems));
        Assert.All(Enum.GetValues<SettingsPageId>().Where(p => p != SettingsPageId.DesktopItems), p => Assert.False(SettingsPages.BarWide(p)));

        var area = new PixelRect(0, 0, 1920, 1040);
        var bar = new PixelRect(12, 12, 1896, 48);
        var entry = new PixelRect(400, 16, 40, 40);
        var spot = FlyoutPlacement.Wide(entry, bar, area, 300);
        var add = FlyoutPlacement.Under(bar, area, 300);

        // The add page's box, one rule: the bar's width, flush with its sides, its height the page's.
        Assert.Equal((add.X, add.Y, add.Width, add.Height), (spot.X, spot.Y, spot.Width, spot.Height));
        Assert.Equal((bar.X, bar.Width), (spot.X, spot.Width));

        // Hung from its entry: the beak's tip over the entry's middle, in the panel's own DIPs.
        Assert.Equal(420 - bar.X, spot.Tip, 9);

        // Wider bars make it wider; a tall page is still cut at 70 % of the work area.
        var narrow = FlyoutPlacement.Wide(entry, new PixelRect(12, 12, 876, 48), new PixelRect(0, 0, 900, 1040), 5000);
        Assert.Equal(876, narrow.Width);
        Assert.Equal(Math.Floor(1040 * 0.7), narrow.Height);
    }

    [Theory]
    [InlineData(DesktopIconSize.Small)]
    [InlineData(DesktopIconSize.Medium)]
    [InlineData(DesktopIconSize.Large)]
    public void S13707_an_icon_on_the_page_has_the_box_the_desktop_gives_it(DesktopIconSize size)
    {
        // Owner, 2026-11-19: "it should show the icons exactly how they would look like on the desktop".
        var screen = new ScreenMetrics(1920, 1080);
        var (w, h) = DesktopGrid.IconSize(DesktopCell.Grid(size), screen);
        var box = UnplacedItems.Dips((w, h), screen);

        Assert.Equal(w * screen.Width, box.Width, 9);
        Assert.Equal(h * screen.Height, box.Height, 9);

        // At least Explorer's cell at that size: whole grid cells round it up, never down.
        Assert.True(box.Width >= DesktopCell.Grid(size).Width - 1e-9 && box.Height >= DesktopCell.Grid(size).Height - 1e-9);
    }

    [Fact]
    public void S13707_icons_stand_box_against_box_in_rows_as_wide_as_the_page_holds()
    {
        var screen = new ScreenMetrics(1920, 1080);
        var medium = UnplacedItems.Dips(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium), screen), screen);
        var large = UnplacedItems.Dips(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Large), screen), screen);

        // Cells per row: as many whole boxes as the width holds, at least one.
        Assert.Equal((int)Math.Floor(1848 / medium.Width), UnplacedItems.Columns(1848, medium.Width));
        Assert.True(UnplacedItems.Columns(1848, large.Width) < UnplacedItems.Columns(1848, medium.Width));
        Assert.True(UnplacedItems.Columns(852, medium.Width) < UnplacedItems.Columns(1848, medium.Width));
        Assert.Equal(3, UnplacedItems.Columns(300, 100));
        Assert.Equal(3, UnplacedItems.Columns(399.9, 100));
        Assert.Equal(1, UnplacedItems.Columns(50, 100));
        Assert.Equal(1, UnplacedItems.Columns(0, 100));

        // Reading order, no gap, wrapping into the next row.
        var cells = UnplacedItems.Cells(7, 300, new SizeDip(100, 120));
        Assert.Equal(new PixelRect(0, 0, 100, 120), cells[0]);
        Assert.Equal(new PixelRect(200, 0, 100, 120), cells[2]);
        Assert.Equal(new PixelRect(0, 120, 100, 120), cells[3]);
        Assert.Equal(new PixelRect(0, 240, 100, 120), cells[6]);
        Assert.Empty(UnplacedItems.Cells(0, 300, new SizeDip(100, 120)));

        // As tall as its rows.
        Assert.Equal(360, UnplacedItems.Height(7, 300, new SizeDip(100, 120)));
        Assert.Equal(120, UnplacedItems.Height(3, 300, new SizeDip(100, 120)));
        Assert.Equal(0, UnplacedItems.Height(0, 300, new SizeDip(100, 120)));
    }

    [Fact]
    public void S13712_the_grid_runs_at_the_desktops_pitch_from_the_corner_both_edges_and_every_fifth_line_heavier()
    {
        // Owner, 2026-11-21: "it should show the grid too": the desktop's cells, so every icon box (whole cells) stands on lines.
        var screen = new ScreenMetrics(1920, 1080);
        var pitch = UnplacedItems.Pitch(screen);
        Assert.Equal(GridGeometry.CellPitch(screen), pitch.X, 9);
        var box = UnplacedItems.Dips(DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium), screen), screen);
        var lines = UnplacedItems.GridLines(3 * box.Width, pitch.X);
        var at = lines.Select(l => l.At).ToList();
        var rows = UnplacedItems.GridLines(2 * box.Height, pitch.Y).Select(l => l.At).ToList();
        foreach (var cell in UnplacedItems.Cells(6, 3 * box.Width, box))
        {
            Assert.Contains(at, x => Math.Abs(x - cell.X) < 1e-6);
            Assert.Contains(rows, y => Math.Abs(y - cell.Y) < 1e-6);
        }

        Assert.Equal(0, lines[0].At);
        Assert.True(lines[0].Major);
        Assert.Equal(3 * box.Width, lines[^1].At, 6);
        Assert.Equal([false, false, false, false, true], lines.Skip(1).Take(5).Select(l => l.Major));

        // A side of 25 at a pitch of 10: 0, 10, 20 (the last whole cell); nothing for no room or no pitch.
        Assert.Equal([0.0, 10.0, 20.0], UnplacedItems.GridLines(25, 10).Select(l => l.At));
        Assert.Empty(UnplacedItems.GridLines(0, 10));
        Assert.Empty(UnplacedItems.GridLines(100, 0));
        Assert.Empty(UnplacedItems.GridLines(double.NaN, 10));
    }

    [Fact]
    public void S13713_a_move_over_the_page_takes_its_desktop_items_never_an_own_icon_a_card_or_what_a_moving_fence_carries()
    {
        var home = ViewState.Create("Home");
        var a = Icon("a.txt", new FracRect(0, 0, 0.09, 0.125));
        var b = Icon("b.txt", new FracRect(0.1, 0, 0.09, 0.125));
        var own = new WidgetInstance { Id = "own", Config = new DeskIconFake { Target = "app.exe" }, Rect = new FracRect(0.2, 0, 0.09, 0.125) };
        var card = new WidgetInstance { Id = "card", Config = new OtherConfig(), Rect = new FracRect(0.3, 0.3, 0.2, 0.2) };
        var fenced = Icon("c.txt", new FracRect(0.5, 0.2, 0.09, 0.125));
        var fence = Fence("f", FenceSource.Held, "c.txt");
        home.Widgets.AddRange([a, b, own, card, fenced, fence]);

        Assert.Equal([Desk + "b.txt", Desk + "a.txt"], UnplacedItems.Taken(home, [b.Id, own.Id, card.Id, a.Id, a.Id]));

        // An icon out of its fence (the fence stays) is taken; with its fence moving it is the fence's, not taken.
        Assert.Equal([Desk + "c.txt"], UnplacedItems.Taken(home, [fenced.Id]));
        Assert.Empty(UnplacedItems.Taken(home, [fence.Id, fenced.Id]));
        Assert.Empty(UnplacedItems.Taken(home, ["gone"]));
    }

    [Fact]
    public void S13713_dropped_on_the_page_icons_leave_the_view_and_its_fences_and_the_page_lists_them_again()
    {
        var home = ViewState.Create("Home");
        home.Widgets.Add(Icon("a.txt", new FracRect(0, 0, 0.09, 0.125)));
        home.Widgets.Add(Icon("b.txt", new FracRect(0.1, 0, 0.09, 0.125)));
        home.Widgets.Add(Icon("c.txt", new FracRect(0.5, 0.2, 0.09, 0.125)));
        home.Widgets.Add(Fence("f", FenceSource.Held, "c.txt"));
        DesktopItem[] listing = [File("a.txt"), File("b.txt"), File("c.txt")];
        Assert.Empty(UnplacedItems.Of(home, listing));

        Assert.Equal([Desk + "a.txt", Desk + "c.txt"], UnplacedItems.Unplace(home, [Desk + "a.txt", Desk + "c.txt", Desk + "zzz.txt"]));
        Assert.Equal(["a.txt", "c.txt"], Names(UnplacedItems.Of(home, listing)));
        Assert.Empty(((FenceFake)home.Widgets.Single(w => w.Id == "f").Config).Items);
        Assert.Equal([Desk + "b.txt"], DesktopViews.Shown(home));

        // Not shown (any more): nothing leaves. Placed again (the page's drag, a double-click): off the list again.
        Assert.Empty(UnplacedItems.Unplace(home, [Desk + "a.txt"]));
        Assert.Empty(DesktopViews.Fill(home, [listing[0]], Screen, (90, 100), Create));
        Assert.Equal(["c.txt"], Names(UnplacedItems.Of(home, listing)));
    }

    [Fact]
    public void S13713_an_all_view_stays_all_and_what_was_taken_off_it_stays_off_until_placed_again()
    {
        // The decision (2026-11-21): taking an icon off a view keeps its Desktop icons value, as Hide from this view and Remove
        // from this view do ([S-5527], [S-5524]): All decides where a new item arrives, and a known item is not new.
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        DesktopItem[] listing = [File("a.txt"), File("b.txt")];
        DesktopReconcile.Apply(state, DesktopReconcile.Plan(state, listing, []), Screen, (90, 100), Create);
        Assert.Equal(ViewDesktopIcons.All, home.DesktopIcons);
        Assert.Empty(UnplacedItems.Of(home, listing));

        Assert.Equal([Desk + "a.txt"], UnplacedItems.Unplace(home, [Desk + "a.txt"]));
        Assert.Equal(ViewDesktopIcons.All, home.DesktopIcons);

        // The next read of the desktop does not bring it back; a new file still arrives on the All view.
        DesktopItem[] more = [.. listing, File("new.txt")];
        DesktopReconcile.Apply(state, DesktopReconcile.Plan(state, more, []), Screen, (90, 100), Create);
        Assert.Equal(["a.txt"], Names(UnplacedItems.Of(home, more)));

        // A Chosen view's chosen set loses it the same way; a None view shows none, so nothing leaves it.
        home.DesktopIcons = ViewDesktopIcons.Chosen;
        Assert.Equal([Desk + "b.txt"], UnplacedItems.Unplace(home, [Desk + "b.txt"]));
        Assert.Equal(ViewDesktopIcons.Chosen, home.DesktopIcons);
        Assert.Equal(["a.txt", "b.txt"], Names(UnplacedItems.Of(home, more)));
        DesktopViews.Set(home, ViewDesktopIcons.None);
        Assert.Empty(UnplacedItems.Unplace(home, [Desk + "new.txt"]));
    }

    [Fact]
    public void S13715_the_unplaced_icons_page_alone_stays_open_under_a_press_outside_it()
    {
        // Owner, 2026-11-21: "it should not close when i click outside of it, that way it should also be possible to move desktop
        // icons back to it".
        Assert.True(SettingsPages.StaysOpen(SettingsPageId.DesktopItems));
        Assert.All(Enum.GetValues<SettingsPageId>().Where(p => p != SettingsPageId.DesktopItems), p => Assert.False(SettingsPages.StaysOpen(p)));
        Assert.False(SettingsPages.StaysOpen(null));
    }

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }
}

/// <summary>[S-13711] The page's title, in the one language the tests set.</summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class UnplacedTitleTests
{
    [Fact]
    public void S13711_the_title_names_the_view_the_page_is_for_on_one_line()
    {
        // Owner, 2026-11-21: "it should show a title though".
        Assert.Equal("Not on Home", UnplacedItems.Title("Home"));
        Assert.Equal("Not on Work", UnplacedItems.Title("  Work "));
        Assert.Equal($"Not on {Str.Shell_View_DefaultName.Text()}", UnplacedItems.Title("   "));
        Assert.DoesNotContain('\n', UnplacedItems.Title("Home"));
    }
}
