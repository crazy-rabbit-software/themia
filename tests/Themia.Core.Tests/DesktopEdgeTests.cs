using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5524] [S-5551] [S-5552] [S-5560]..[S-5563] [S-5570] The desktop's edges: Themia's lines in
/// the background menu, New ›'s rename, displays that come and go, work areas that change, the icon
/// at each scale and the reading order.
/// </summary>
public sealed class DesktopEdgeTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    /// <summary>A display of 900 x 1100 DIPs: a 90 x 110 cell is 0.1 x 0.1 of it.</summary>
    private static readonly ScreenMetrics Screen = new(900, 1100);

    private static readonly (double, double) Cell = (90, 110);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Item(string name, double x, double y)
        => new() { Id = "w-" + name + "-" + Guid.NewGuid().ToString("n")[..6], Config = new DeskIconFake { Item = Desk + name }, Rect = new FracRect(x, y, 0.1, 0.1) };

    private static WidgetInstance Create(DesktopItem item, FracRect rect)
        => new() { Id = "new-" + DesktopRename.FileName(item.Key), Config = new DeskIconFake { Item = item.Key }, Rect = rect };

    private static IEnumerable<string> Keys(ViewState view) => view.Widgets.Select(w => w.Config).OfType<IDesktopItemConfig>().Select(c => c.Item);

    /// <summary>Two displays: "main" shows Home, "side" shows Side.</summary>
    private static AppState TwoDisplays(out ViewState home, out ViewState side)
    {
        home = ViewState.Create("Home");
        side = ViewState.Create("Side");
        var state = new AppState { Views = [home, side], ActiveViewId = home.Id };
        state.MonitorViews["main"] = home.Id;
        state.MonitorViews["side"] = side.Id;
        return state;
    }

    private static DesktopDisplayPlan Settle(AppState state, string[] connected, params DesktopShownView[] shown)
    {
        var plan = DesktopDisplays.Plan(state, connected, shown, shown.Length > 0 ? shown[0] : null, [File("a.txt"), File("b.txt"), File("c.txt")], Cell);
        DesktopDisplays.Apply(state, plan, Create);
        return plan;
    }

    // ---- [S-5548] [S-5551] [S-5552] the background menu ------------------------------------------

    /// <summary>The menu as a flat list: a command, a submenu's group, or null for a separator.</summary>
    private static object?[] Flat(IReadOnlyList<DesktopMenuEntry> menu)
        => [.. menu.Select(entry => entry.IsSeparator ? null : entry.Group is { } group ? group : (object?)entry.Command)];

    [Fact]
    public void S5548_the_background_menu_is_windows_elevens_with_themias_lines_where_an_apps_go()
    {
        var menu = DesktopBackground.Menu(new DesktopBackgroundFacts(ClipboardHasFiles: true, HasTerminal: true));

        Assert.Equal(
            [
                DesktopMenuGroup.View, DesktopMenuGroup.SortBy, DesktopMenuCommand.Refresh, null,
                DesktopMenuCommand.Paste, DesktopMenuCommand.PasteShortcut, DesktopMenuGroup.New, null,
                DesktopMenuCommand.DisplaySettings, DesktopMenuCommand.Personalise, DesktopMenuCommand.OpenInTerminal, null,
                DesktopMenuCommand.UnlockWidgets, DesktopMenuCommand.AddWidget, null,
                DesktopMenuCommand.ShowMoreOptions,
            ],
            Flat(menu));
        Assert.Equal(
            [DesktopMenuCommand.NewFolder, DesktopMenuCommand.NewShortcut],
            menu.Single(entry => entry.Group == DesktopMenuGroup.New).Children.Select(child => child.Command!.Value));
    }

    [Fact]
    public void S5548_paste_is_there_only_with_files_on_the_clipboard_and_the_terminal_only_where_it_is_installed()
    {
        var menu = DesktopBackground.Menu(new DesktopBackgroundFacts(ClipboardHasFiles: false, HasTerminal: false));

        Assert.Equal(
            [
                DesktopMenuGroup.View, DesktopMenuGroup.SortBy, DesktopMenuCommand.Refresh, null,
                DesktopMenuGroup.New, null,
                DesktopMenuCommand.DisplaySettings, DesktopMenuCommand.Personalise, null,
                DesktopMenuCommand.UnlockWidgets, DesktopMenuCommand.AddWidget, null,
                DesktopMenuCommand.ShowMoreOptions,
            ],
            Flat(menu));
    }

    [Fact]
    public void S5548_no_background_menu_has_two_separators_in_a_row_or_one_at_an_end_and_none_offers_undo()
    {
        foreach (var clipboard in new[] { true, false })
        {
            foreach (var terminal in new[] { true, false })
            {
                var menu = DesktopBackground.Menu(new DesktopBackgroundFacts(clipboard, terminal));

                Assert.False(menu[0].IsSeparator);
                Assert.False(menu[^1].IsSeparator);
                Assert.DoesNotContain(Enumerable.Range(1, menu.Count - 1), i => menu[i].IsSeparator && menu[i - 1].IsSeparator);
                Assert.Equal(DesktopMenuCommand.ShowMoreOptions, menu[^1].Command);
                Assert.DoesNotContain(Enum.GetNames<DesktopMenuCommand>(), name => name.Contains("Undo", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void S5551_themias_lines_are_the_way_into_edit_mode_then_add_widget_between_windows_lines_and_show_more_options()
    {
        var menu = Flat(DesktopBackground.Menu(new DesktopBackgroundFacts(true, true)));
        var at = Array.IndexOf(menu, DesktopMenuCommand.UnlockWidgets);

        Assert.Equal([null, DesktopMenuCommand.UnlockWidgets, DesktopMenuCommand.AddWidget, null, DesktopMenuCommand.ShowMoreOptions], menu[(at - 1)..]);
    }

    [Fact]
    public void S5552_a_command_beside_new_folder_is_one_of_news()
    {
        Assert.True(DesktopBackground.IsNewCommand([null, "NewFolder", ".txt"]));
        Assert.True(DesktopBackground.IsNewCommand(["newfolder"]));
        Assert.False(DesktopBackground.IsNewCommand(["paste", "display"]));
        Assert.False(DesktopBackground.IsNewCommand(null));
    }

    [Fact]
    public void S5552_news_hint_renames_the_first_arrival_it_lands_only()
    {
        var landings = new DesktopLandings();
        landings.Expect(new DesktopLandingHint("home", 0.5, 0.5, [], Renames: true));

        var spots = landings.Place([File("New folder"), File("other.txt")]);

        Assert.True(spots[0]!.Value.Renames);
        Assert.False(spots[1]!.Value.Renames);
        Assert.Null(landings.Pending);
    }

    [Fact]
    public void S5552_a_paste_or_a_drop_renames_nothing()
    {
        var landings = new DesktopLandings();
        landings.Expect(new DesktopLandingHint("home", 0.5, 0.5, ["a.txt"]));

        Assert.False(landings.Place([File("a.txt")])[0]!.Value.Renames);
    }

    // ---- [S-5524] the edit-mode delete's words -------------------------------------------------

    [Fact]
    public void S5524_edit_modes_delete_of_desktop_items_only_says_remove_from_this_view()
    {
        DeskIconFake Item(string name) => new() { Item = Desk + name };

        Assert.True(DesktopBackground.RemovesFromView([Item("a.txt"), Item("b.txt")]));
        Assert.False(DesktopBackground.RemovesFromView([Item("a.txt"), new OtherConfig()]));
        Assert.False(DesktopBackground.RemovesFromView([]));
    }

    [Fact]
    public void S5524_an_own_icon_in_the_selection_says_delete_for_it_is_gone_with_its_widget()
    {
        var item = new DeskIconFake { Item = Desk + "a.txt" };
        var own = new DeskIconFake { Target = @"C:\Tools\app.exe" };

        Assert.False(DesktopBackground.RemovesFromView([own]));
        Assert.False(DesktopBackground.RemovesFromView([item, own]));
        Assert.False(DesktopBackground.RemovesFromView([own, item]));
        Assert.False(DesktopBackground.RemovesFromView([new DeskIconFake()]));
    }

    // ---- [S-5560] [S-5561] displays unplugged and back ----------------------------------------

    [Fact]
    public void S5560_an_unplugged_displays_items_get_stand_ins_on_the_primary_and_keep_their_cells_there()
    {
        var state = TwoDisplays(out var home, out var side);
        home.Widgets.Add(Item("a.txt", 0, 0));
        var b = Item("b.txt", 0.5, 0.5);
        var c = Item("c.txt", 0.5, 0.2);
        side.Widgets.AddRange([b, c]);

        Settle(state, ["main"], new DesktopShownView(home.Id, Screen));

        // [S-11876] The primary's first free spots after a.txt in reading order, in the side view's order (c above b in one column).
        Assert.Equal([Desk + "a.txt", Desk + "c.txt", Desk + "b.txt"], Keys(home));
        // One cell off the top edge ([S-11888]).
        Assert.Equal(0.1, home.Widgets[1].Rect.X, 6);
        Assert.Equal(1 / 110.0, home.Widgets[1].Rect.Y, 6);
        Assert.Equal(0.2, home.Widgets[2].Rect.X, 6);
        Assert.Equal(1 / 110.0, home.Widgets[2].Rect.Y, 6);

        // The side view itself is untouched, and the stand-ins are recorded.
        Assert.Equal(new FracRect(0.5, 0.5, 0.1, 0.1), b.Rect);
        Assert.Equal(2, state.Desktop.Fallbacks!.Count);

        // Settled: a second pass changes nothing.
        Assert.False(DesktopDisplays.Plan(state, ["main"], [new(home.Id, Screen)], new(home.Id, Screen), [File("a.txt"), File("b.txt"), File("c.txt")], Cell).Changes(state));
    }

    [Fact]
    public void S5561_a_display_back_takes_its_stand_ins_away_and_its_view_is_as_it_was()
    {
        var state = TwoDisplays(out var home, out var side);
        side.Widgets.Add(Item("b.txt", 0.5, 0.5));
        Settle(state, ["main"], new DesktopShownView(home.Id, Screen));
        Assert.Single(home.Widgets);

        Settle(state, ["main", "side"], new DesktopShownView(home.Id, Screen), new DesktopShownView(side.Id, Screen));

        Assert.Empty(home.Widgets);
        Assert.Equal([Desk + "b.txt"], Keys(side));
        Assert.Equal(new FracRect(0.5, 0.5, 0.1, 0.1), side.Widgets[0].Rect);
        Assert.Null(state.Desktop.Fallbacks);
    }

    [Fact]
    public void S5561_a_stand_in_the_user_moved_is_theirs_and_stays_when_the_display_is_back()
    {
        var state = TwoDisplays(out var home, out var side);
        side.Widgets.Add(Item("b.txt", 0.5, 0.5));
        Settle(state, ["main"], new DesktopShownView(home.Id, Screen));
        home.Widgets[0].Rect = new FracRect(0.3, 0.3, 0.1, 0.1);

        Settle(state, ["main", "side"], new DesktopShownView(home.Id, Screen), new DesktopShownView(side.Id, Screen));

        Assert.Equal([Desk + "b.txt"], Keys(home));
        Assert.Null(state.Desktop.Fallbacks);
    }

    [Fact]
    public void S5560_views_no_display_shows_are_the_users_own_and_lend_nothing()
    {
        // A third view with no display: per view, by the user's choice; only an unplugged display's view is stranded.
        var state = TwoDisplays(out var home, out var side);
        var spare = ViewState.Create("Spare");
        spare.Widgets.Add(Item("c.txt", 0, 0));
        state.Views.Add(spare);
        side.Widgets.Add(Item("b.txt", 0, 0));

        Settle(state, ["main", "side"], new DesktopShownView(home.Id, Screen), new DesktopShownView(side.Id, Screen));

        Assert.Empty(home.Widgets);
    }

    [Fact]
    public void S5560_an_item_already_shown_on_a_connected_display_gets_no_stand_in()
    {
        var state = TwoDisplays(out var home, out var side);
        home.Widgets.Add(Item("b.txt", 0, 0));
        side.Widgets.Add(Item("b.txt", 0.5, 0.5));
        side.Widgets.Add(Item("gone.txt", 0.5, 0.1)); // not listed: the reconcile's to remove, never a stand-in

        Settle(state, ["main"], new DesktopShownView(home.Id, Screen));

        Assert.Equal([Desk + "b.txt"], Keys(home));
        Assert.Null(state.Desktop.Fallbacks);
    }

    [Fact]
    public void S5560_stand_in_records_are_repaired_at_load()
    {
        var state = new AppState();
        state.Desktop.Fallbacks =
        [
            new DesktopFallback { Widget = "w", View = "v", Item = Desk + "a.txt", Rect = new FracRect(0, 0, 0.1, 0.1) },
            new DesktopFallback { Widget = "", View = "v", Item = Desk + "b.txt", Rect = new FracRect(0, 0, 0.1, 0.1) },
            new DesktopFallback { Widget = "x", View = "v", Item = Desk + "c.txt", Rect = new FracRect(double.NaN, 0, 0.1, 0.1) },
        ];

        Assert.Single(DesktopDisplays.RepairFallbacks(state));
        Assert.Single(state.Desktop.Fallbacks);

        state.Desktop.Fallbacks = [];
        Assert.Empty(DesktopDisplays.RepairFallbacks(state));
        Assert.Null(state.Desktop.Fallbacks);
    }

    // ---- [S-5562] the work area ----------------------------------------------------------------

    [Fact]
    public void S5562_an_item_the_taskbar_now_covers_moves_to_a_free_cell_inside_the_work_area()
    {
        var state = TwoDisplays(out var home, out _);
        var low = Item("a.txt", 0, 0.9);
        var other = new WidgetInstance { Id = "clock", Config = new OtherConfig(), Rect = new FracRect(0.5, 0.85, 0.3, 0.15) };
        home.Widgets.AddRange([low, other]);

        // The taskbar now takes the bottom tenth.
        var shrunk = new ScreenMetrics(900, 1100) { Allowed = new FracRect(0, 0, 1, 0.9) };
        Settle(state, ["main"], new DesktopShownView(home.Id, shrunk));

        // [S-11887] Laid by the one rule: inside, one cell off the edges ([S-11888]).
        Assert.True(low.Rect.Bottom <= 0.9 - (1 / 110.0) + 1e-9, $"{low.Rect}");
        Assert.Equal(1 / 90.0, low.Rect.X, 6);

        // Only item widgets follow the work area ([S-841] for every other widget).
        Assert.Equal(new FracRect(0.5, 0.85, 0.3, 0.15), other.Rect);
    }

    [Fact]
    public void S5562_a_work_area_that_grows_moves_nothing()
    {
        var state = TwoDisplays(out var home, out _);
        var item = Item("a.txt", 0.3, 0.4);
        home.Widgets.Add(item);

        var plan = Settle(state, ["main"], new DesktopShownView(home.Id, Screen));

        Assert.Empty(plan.Moves);
        Assert.Equal(new FracRect(0.3, 0.4, 0.1, 0.1), item.Rect);
    }

    // ---- [S-5563] the icon at each scale -------------------------------------------------------

    [Theory]
    [InlineData(48, 1.0, 48, DesktopIconList.ExtraLarge)]
    [InlineData(48, 1.25, 60, DesktopIconList.Jumbo)]
    [InlineData(48, 1.5, 72, DesktopIconList.Jumbo)]
    [InlineData(48, 2.0, 96, DesktopIconList.Jumbo)]
    [InlineData(32, 1.5, 48, DesktopIconList.ExtraLarge)]
    [InlineData(16, 0, 16, DesktopIconList.ExtraLarge)]
    public void S5563_an_icon_takes_the_pixels_of_its_displays_scale_from_the_list_that_has_them(double dip, double scale, int pixels, DesktopIconList list)
    {
        Assert.Equal(pixels, DesktopIconPixels.For(dip, scale));
        Assert.Equal(list, DesktopIconPixels.ListFor(pixels));
    }

    [Fact]
    public void S5563_a_jumbo_image_that_is_only_a_small_icon_in_a_corner_is_drawn_from_the_extra_large_list()
    {
        const int Side = 256;
        var small = new byte[Side * Side * 4];
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                small[((y * Side) + x) * 4 + 3] = 255;
            }
        }

        Assert.True(DesktopIconPixels.HoldsOnlySmallImage(small, Side, Side));

        var large = (byte[])small.Clone();
        large[((200 * Side) + 200) * 4 + 3] = 255;
        Assert.False(DesktopIconPixels.HoldsOnlySmallImage(large, Side, Side));

        // An extra-large image is never "too small" for itself.
        Assert.False(DesktopIconPixels.HoldsOnlySmallImage(new byte[48 * 48 * 4], 48, 48));
    }

    // ---- [S-5570] the reading order -------------------------------------------------------------

    [Fact]
    public void S5570_a_screen_reader_hears_an_items_place_in_the_desktops_order()
    {
        DesktopBox[] boxes = [new(Desk + "b", 0, 0, 110, 90, 110), new(Desk + "a", 0, 0, 0, 90, 110), new(Desk + "z", 1, 2000, 0, 90, 110)];
        var ordered = DesktopNav.Order(boxes);

        Assert.Equal((1, 3), DesktopNav.PositionOf(ordered, Desk + "A"));
        Assert.Equal((2, 3), DesktopNav.PositionOf(ordered, Desk + "b"));
        Assert.Equal((3, 3), DesktopNav.PositionOf(ordered, Desk + "z"));
        Assert.Null(DesktopNav.PositionOf(ordered, Desk + "gone"));
    }
}
