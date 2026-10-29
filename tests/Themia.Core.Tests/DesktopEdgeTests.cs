using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-5524] [S-5551] [S-5552] [S-5560]..[S-5563] [S-5570] [S-5575] The desktop's edges: Themia's lines in
/// the background menu, New ›'s rename, displays that come and go, work areas that change, the icon
/// at each scale, the reading order and living beside other desktop programs.
/// </summary>
public sealed class DesktopEdgeTests
{
    private const string Desk = @"C:\Users\a\Desktop\";

    private sealed class ItemConfig : WidgetConfig, IDesktopItemConfig
    {
        public string Item { get; set; } = string.Empty;

        public override WidgetConfig Clone() => new ItemConfig { Item = Item };
    }

    private sealed class OtherConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new OtherConfig();
    }

    /// <summary>A display of 900 x 1100 DIPs: a 90 x 110 cell is 0.1 x 0.1 of it.</summary>
    private static readonly ScreenMetrics Screen = new(900, 1100);

    private static readonly (double, double) Cell = (90, 110);

    private static DesktopItem File(string name) => new(Desk + name, name, DesktopItemKind.File);

    private static WidgetInstance Item(string name, double x, double y)
        => new() { Id = "w-" + name + "-" + Guid.NewGuid().ToString("n")[..6], Config = new ItemConfig { Item = Desk + name }, Rect = new FracRect(x, y, 0.1, 0.1) };

    private static WidgetInstance Create(DesktopItem item, FracRect rect)
        => new() { Id = "new-" + DesktopRename.FileName(item.Key), Config = new ItemConfig { Item = item.Key }, Rect = rect };

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

    // ---- [S-5551] [S-5552] the background menu --------------------------------------------------

    [Fact]
    public void S5551_themias_lines_are_the_way_into_edit_mode_then_add_widget()
        => Assert.Equal([DesktopMenuCommand.EditWidgets, DesktopMenuCommand.AddWidget], DesktopBackground.ThemiaLines);

    [Fact]
    public void S5551_themias_group_goes_after_the_paste_group_where_an_apps_lines_go()
    {
        ShellMenuLine[] lines =
        [
            new(false, "paste"), new(false, "pastelink"), new(true, null),
            new(false, null), // New ›
            new(true, null),
            new(false, "display"), new(false, "personalize"),
        ];

        Assert.Equal(new DesktopMenuSlot(2, SeparatorBefore: true, SeparatorAfter: false), DesktopBackground.Slot(lines));
    }

    [Fact]
    public void S5551_with_the_paste_group_last_the_group_ends_the_menu_and_with_none_it_starts_it()
    {
        Assert.Equal(new DesktopMenuSlot(1, SeparatorBefore: true, SeparatorAfter: false), DesktopBackground.Slot([new(false, "PASTE")]));
        Assert.Equal(new DesktopMenuSlot(0, SeparatorBefore: false, SeparatorAfter: true), DesktopBackground.Slot([new(false, "display"), new(false, "personalize")]));
        Assert.Equal(new DesktopMenuSlot(0, false, false), DesktopBackground.Slot([]));
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
        Assert.True(DesktopBackground.RemovesFromView([new ItemConfig(), new ItemConfig()]));
        Assert.False(DesktopBackground.RemovesFromView([new ItemConfig(), new OtherConfig()]));
        Assert.False(DesktopBackground.RemovesFromView([]));
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

        // Down the primary's first column after a.txt, in the side view's order (c above b in one column).
        Assert.Equal([Desk + "a.txt", Desk + "c.txt", Desk + "b.txt"], Keys(home));
        Assert.Equal(0.1, home.Widgets[1].Rect.Y, 6);
        Assert.Equal(0.2, home.Widgets[2].Rect.Y, 6);

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

        Assert.True(low.Rect.Bottom <= 0.9 + 1e-9, $"{low.Rect}");
        Assert.Equal(0, low.Rect.X, 6);

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

    // ---- [S-5575] beside other desktop programs ------------------------------------------------

    [Theory]
    [InlineData(false, false, false, DesktopHolder.None)]
    [InlineData(false, true, false, DesktopHolder.Wallpaper)]
    [InlineData(true, true, false, DesktopHolder.Replaced)]
    [InlineData(true, true, true, DesktopHolder.KeptShown)]
    public void S5575_who_holds_the_desktop_is_read_from_its_windows(bool foreignView, bool foreignChild, bool keptShown, DesktopHolder holder)
        => Assert.Equal(holder, DesktopCoexistence.Classify(new DesktopShellFacts(foreignView, foreignChild, keptShown)));

    [Fact]
    public void S5575_themias_items_stand_down_beside_a_program_that_holds_the_icons_never_beside_a_wallpaper()
    {
        Assert.True(DesktopCoexistence.ShowsItems(DesktopShows.ThemiaItems, DesktopHolder.None));
        Assert.True(DesktopCoexistence.ShowsItems(DesktopShows.ThemiaItems, DesktopHolder.Wallpaper));
        Assert.False(DesktopCoexistence.ShowsItems(DesktopShows.ThemiaItems, DesktopHolder.Replaced));
        Assert.False(DesktopCoexistence.ShowsItems(DesktopShows.ThemiaItems, DesktopHolder.KeptShown));
        Assert.False(DesktopCoexistence.ShowsItems(DesktopShows.WindowsIcons, DesktopHolder.None));

        // Never fight: no hide while another program holds the list.
        Assert.True(DesktopCoexistence.MayHide(DesktopShows.Nothing, DesktopHolder.Wallpaper));
        Assert.False(DesktopCoexistence.MayHide(DesktopShows.Nothing, DesktopHolder.KeptShown));
        Assert.False(DesktopCoexistence.MayHide(DesktopShows.ThemiaItems, DesktopHolder.Replaced));
    }

    [Fact]
    public void S5575_settings_says_why_the_choice_does_not_show()
    {
        Assert.NotNull(DesktopCoexistence.Note(DesktopShows.ThemiaItems, DesktopHolder.Replaced));
        Assert.NotNull(DesktopCoexistence.Note(DesktopShows.Nothing, DesktopHolder.KeptShown));
        Assert.Null(DesktopCoexistence.Note(DesktopShows.ThemiaItems, DesktopHolder.Wallpaper));
        Assert.Null(DesktopCoexistence.Note(DesktopShows.WindowsIcons, DesktopHolder.Replaced));
    }
}
