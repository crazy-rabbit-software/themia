using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2008] [S-11874]..[S-11879] The locked desktop's moves, its local grid, desktop icons on the widget
/// grid, the one selection over icons and cards, and when an icon's move becomes the shell's drag.
/// </summary>
public sealed class DesktopGridTests
{
    /// <summary>100 × 100 DIPs: a 10 × 10 grid, one cell 0.1 of the screen.</summary>
    private static readonly ScreenMetrics Ten = new(100, 100);

    private sealed class CardConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new CardConfig();
    }

    private static void Near(FracRect expected, FracRect? actual)
    {
        Assert.NotNull(actual);
        var a = actual.Value;
        Assert.True(
            Math.Abs(expected.X - a.X) < 1e-9 && Math.Abs(expected.Y - a.Y) < 1e-9 && Math.Abs(expected.W - a.W) < 1e-9 && Math.Abs(expected.H - a.H) < 1e-9,
            $"expected {expected}, was {a}");
    }

    private static WidgetInstance Card(string id, FracRect rect) => new() { Id = id, Config = new CardConfig(), Rect = rect };

    private static WidgetInstance Icon(string id, string key, FracRect rect) => new() { Id = id, Config = new DeskIconFake { Item = key }, Rect = rect };

    // ---- [S-2008] -----------------------------------------------------------------------------------

    [Fact]
    public void S2008_dragging_a_locked_widget_moves_it_and_never_turns_on_edit_mode()
    {
        // Regression (owner, 2026-11-15): a locked widget dragged went into full edit mode. The press on the
        // locked desktop starts no edit-mode press at all; it is the card's move.
        Assert.Equal(CardPress.None, CardGestures.Press(editMode: false, leftButton: true, ctrl: false, onHandle: false));
        Assert.True(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: true, ownsDrags: false, pinned: false));

        // A view that drags its own content keeps its press; a preview and the right button never move.
        Assert.False(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: true, ownsDrags: true, pinned: false));
        Assert.False(CardGestures.MovesLocked(editMode: false, preview: true, leftButton: true, ownsDrags: false, pinned: false));
        Assert.False(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: false, ownsDrags: false, pinned: false));

        // In edit mode the edit press moves, as before.
        Assert.False(CardGestures.MovesLocked(editMode: true, preview: false, leftButton: true, ownsDrags: false, pinned: false));
    }

    // ---- [S-12950] Lock widgets ------------------------------------------------------------------------

    [Fact]
    public void S12950_a_locked_widget_dragged_does_not_move_and_an_icon_still_does()
    {
        // Regression (owner, 2026-11-17): "you should be able to lock the desktop in which case you should only be able
        // to move the desktop icons but not any other widgets". Before the lock a widget's drag always moved it.
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.3, 0.2)));

        Assert.True(WidgetLock.Pins(view, "clock", lockWidgets: true, editMode: false));
        Assert.False(WidgetLock.Pins(view, "i", lockWidgets: true, editMode: false));
        Assert.False(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: true, ownsDrags: false,
            pinned: WidgetLock.Pins(view, "clock", lockWidgets: true, editMode: false)));
        Assert.True(CardGestures.MovesLocked(editMode: false, preview: false, leftButton: true, ownsDrags: false,
            pinned: WidgetLock.Pins(view, "i", lockWidgets: true, editMode: false)));

        // Off (the default), everything moves as before.
        Assert.False(new AppPreferences().LockWidgets);
        Assert.False(WidgetLock.Pins(view, "clock", lockWidgets: false, editMode: false));
    }

    [Fact]
    public void S12950_edit_mode_moves_everything_whatever_the_lock()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.3, 0.2)));

        Assert.False(WidgetLock.Pins(view, "clock", lockWidgets: true, editMode: true));
        Assert.False(WidgetLock.Holds(lockWidgets: true, editMode: true, isIcon: false));
        Assert.Equal(["clock"], WidgetLock.Movable(view, ["clock"], lockWidgets: true, editMode: true));
    }

    [Fact]
    public void S12950_a_mixed_selection_moves_only_its_icons_while_widgets_are_locked()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Icon("j", @"C:\d\b.txt", new FracRect(0, 0.1, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.3, 0.2)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select((w, k) => new DesktopBox(KeyOf(w)!, 0, k * 10, 0, 5, 5)), _ => null);
        selection.SelectAll();

        Assert.Equal(["i", "j"], DesktopPicks.Moving(view, selection, "i", KeyOf, lockWidgets: true));
        Assert.Equal(["i", "j", "clock"], DesktopPicks.Moving(view, selection, "i", KeyOf, lockWidgets: false));
    }

    [Fact]
    public void S12950_a_group_holding_a_widget_stays_whole_and_a_group_of_icons_moves()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.1, 0, 0.3, 0.2)));
        view.Widgets.Add(Icon("j", @"C:\d\b.txt", new FracRect(0.6, 0.6, 0.1, 0.1)));
        view.Widgets.Add(Icon("k", @"C:\d\c.txt", new FracRect(0.7, 0.6, 0.1, 0.1)));
        Assert.NotNull(WidgetGroups.Group(view, ["i", "clock"]));
        Assert.NotNull(WidgetGroups.Group(view, ["j", "k"]));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();

        // The icon in a group with a widget is held with it: its press does not move the group apart.
        Assert.True(WidgetLock.Pins(view, "i", lockWidgets: true, editMode: false));
        Assert.Empty(DesktopPicks.Moving(view, selection, "i", KeyOf, lockWidgets: true));

        // A group of icons alone moves whole.
        Assert.False(WidgetLock.Pins(view, "j", lockWidgets: true, editMode: false));
        Assert.Equal(["j", "k"], DesktopPicks.Moving(view, selection, "j", KeyOf, lockWidgets: true));
    }

    // ---- [S-11874] the local grid ---------------------------------------------------------------------

    [Fact]
    public void S11874_the_falloff_is_full_on_the_box_and_gone_at_the_radius()
    {
        Assert.Equal(1, LocalGrid.Falloff(0, 30));
        Assert.Equal(0.5, LocalGrid.Falloff(15, 30), 9);
        Assert.Equal(0, LocalGrid.Falloff(30, 30));
        Assert.Equal(0, LocalGrid.Falloff(45, 30));
        Assert.True(LocalGrid.Falloff(5, 30) > LocalGrid.Falloff(10, 30));
        Assert.Equal(0, LocalGrid.Distance(new PixelRect(40, 40, 20, 20), 50, 50));
        Assert.Equal(5, LocalGrid.Distance(new PixelRect(40, 40, 20, 20), 65, 50), 9);
    }

    [Fact]
    public void S11874_S11886_only_the_lines_within_the_fade_are_drawn_in_short_stretches()
    {
        // A 200 × 200 DIP screen (20 × 20 cells of 10): room for the six-cell fade on every side.
        var screen = new ScreenMetrics(200, 200);
        var target = new FracRect(0.4, 0.4, 0.1, 0.1);
        var box = LocalGrid.Lit(target, screen);
        Assert.Equal(new PixelRect(80, 80, 20, 20), box);

        var lines = LocalGrid.Lines(target, screen);
        Assert.NotEmpty(lines);
        var radius = LocalGrid.RadiusDip(screen);
        Assert.Equal(LocalGrid.RadiusCells * 10, radius, 9);
        foreach (var line in lines)
        {
            var (mx, my) = ((line.X1 + line.X2) / 2, (line.Y1 + line.Y2) / 2);
            Assert.InRange(line.Alpha, 1e-9, 1);
            Assert.Equal(LocalGrid.Strength(box, mx, my, radius), line.Alpha, 9);

            // Every stretch is a quarter of a cell, on a grid line, inside the box grown by the radius.
            Assert.Equal(10.0 / LocalGrid.SegmentsPerCell, Math.Abs(line.X2 - line.X1) + Math.Abs(line.Y2 - line.Y1), 9);
            Assert.Equal(0, (line.X1 % 10) * (line.Y1 % 10), 9);
            Assert.InRange(mx, 80 - radius, 100 + radius);
            Assert.InRange(my, 80 - radius, 100 + radius);
        }

        // The box's own lines at full strength, further out weaker, nothing at the screen's corner.
        Assert.Contains(lines, l => l.X1 == 90 && l.X2 == 90 && l.Y1 == 80 && l.Alpha == 1);
        var near = lines.Single(l => l.X1 == 110 && l.X2 == 110 && l.Y1 == 87.5);
        var far = lines.Single(l => l.X1 == 140 && l.X2 == 140 && l.Y1 == 87.5);
        Assert.True(near.Alpha > far.Alpha);
        Assert.DoesNotContain(lines, l => l.X1 < 10 && l.Y1 < 10);
    }

    [Fact]
    public void S11886_the_fade_keeps_the_boxs_rectangle_and_reaches_six_cells()
    {
        // Owner, 2026-11-15: "the fade out of the grid should be bigger, and it should also not be round
        // anymore". A point straight out and one diagonally out at the same per-axis distance from the box's
        // edges are the same strength: the fade is the box's rectangle grown evenly, not an oval.
        Assert.Equal(6, LocalGrid.RadiusCells);
        var box = new PixelRect(40, 40, 20, 20);
        const double radius = 60;
        var straight = LocalGrid.Strength(box, 60 + 25, 50, radius);
        var diagonal = LocalGrid.Strength(box, 60 + 25, 60 + 25, radius);
        Assert.Equal(straight, diagonal, 9);
        Assert.InRange(straight, 0.01, 0.99);

        // The larger axis distance decides: a point 25 out on one axis and 10 on the other is as strong as 25 straight out.
        Assert.Equal(straight, LocalGrid.Strength(box, 60 + 25, 60 + 10, radius), 9);

        // Full on the box and at its corners, smoothly to nothing at the radius past its edges.
        Assert.Equal(1, LocalGrid.Strength(box, 50, 50, radius));
        Assert.Equal(1, LocalGrid.Strength(box, 40, 40, radius), 9);
        Assert.Equal(0, LocalGrid.Strength(box, 60 + radius, 50, radius), 9);
        Assert.Equal(0, LocalGrid.Strength(box, 60 + radius, 60 + radius, radius), 9);
        Assert.True(LocalGrid.Strength(box, 70, 50, radius) > LocalGrid.Strength(box, 75, 50, radius));

        // A wide box fades by the same distance from its edges along both axes.
        var wide = new PixelRect(0, 45, 100, 10);
        Assert.Equal(LocalGrid.Strength(wide, 100 + 20, 50, radius), LocalGrid.Strength(wide, 50, 55 + 20, radius), 9);
    }

    [Fact]
    public void S11886_the_grid_is_fainter_and_fades_out_slower_than_in()
    {
        // Owner, 2026-11-15: "less visible" (half the first 0.4) and "fade out a little slower".
        Assert.Equal(0.2, LocalGrid.PeakOpacity, 9);
        Assert.InRange(LocalGrid.FadeOutMs, 300, 400);
        Assert.True(LocalGrid.FadeOutMs > LocalGrid.FadeInMs);
    }

    [Fact]
    public void S11881_a_locked_move_shows_the_moving_borders_and_the_neighbours_fading_with_the_grid()
    {
        // Owner, 2026-11-15: "when moving the widgets around in non edit mode it should show the borders too".
        // A 200 × 200 DIP screen: the six-cell fade ([S-11886]) ends 60 past the box.
        var target = new FracRect(0.4, 0.4, 0.1, 0.1);
        var borders = LocalGrid.Borders(
            target,
            ["held"],
            [("touching", new FracRect(0.5, 0.4, 0.05, 0.1)), ("near", new FracRect(0.4, 0.55, 0.1, 0.05)), ("far", new FracRect(0, 0, 0.05, 0.05)), ("held", new FracRect(0, 0.9, 0.05, 0.05))],
            new ScreenMetrics(200, 200));

        Assert.Equal(1, borders["held"]);
        Assert.True(borders["touching"] > borders["near"]);
        Assert.InRange(borders["near"], 1e-9, 1);
        Assert.False(borders.ContainsKey("far"));

        // Nothing once it lands, and nothing on a screen not measured.
        Assert.Empty(LocalGrid.Borders(target, ["held"], [], new ScreenMetrics(0, 0)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S11889_icons_and_cards_show_their_corners_in_edit_mode_and_in_a_move(bool icon)
    {
        // Owner, 2026-11-15: "the desktop icons should also show the corners".
        Assert.True(SelectionRules.MarksShown(editing: true, selected: false, moveAlpha: null, icon));
        Assert.True(SelectionRules.MarksShown(editing: true, selected: true, moveAlpha: null, icon));
        Assert.True(SelectionRules.MarksShown(editing: false, selected: false, moveAlpha: 1, icon));
        Assert.True(SelectionRules.MarksShown(editing: false, selected: true, moveAlpha: 1, icon));
        Assert.True(SelectionRules.MarksShown(editing: false, selected: false, moveAlpha: 0.4, icon));

        // At rest on the locked desktop, unselected, neither draws any.
        Assert.False(SelectionRules.MarksShown(editing: false, selected: false, moveAlpha: null, icon));
    }

    [Fact]
    public void S11889_a_selected_icon_on_the_locked_desktop_shows_its_highlight_alone()
    {
        // Owner, 2026-11-16: "when highlighting a folder it should not show the corners."
        Assert.False(SelectionRules.MarksShown(editing: false, selected: true, moveAlpha: null, icon: true));

        // A selected card keeps its corners ([S-11877]).
        Assert.True(SelectionRules.MarksShown(editing: false, selected: true, moveAlpha: null, icon: false));
    }

    [Fact]
    public void S11889_a_locked_move_borders_the_icons_near_where_it_lands_too()
    {
        // LocalGrid.Borders takes every standing widget, icons among them: an icon touching the target fades in.
        var borders = LocalGrid.Borders(new FracRect(0.4, 0.4, 0.1, 0.1), ["held"], [("icon", new FracRect(0.5, 0.4, 0.05, 0.1))], new ScreenMetrics(200, 200));
        Assert.Equal(1, borders["icon"], 6);
    }

    [Fact]
    public void S11874_an_unusable_screen_draws_nothing()
        => Assert.Empty(LocalGrid.Lines(new FracRect(0.4, 0.4, 0.2, 0.2), new ScreenMetrics(0, 0)));

    // ---- [S-11875] an icon's size in grid cells -------------------------------------------------------

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void S11875_an_icon_is_whole_grid_cells_at_every_icon_size_and_scale(double displayScale)
    {
        // A 1920 × 1080 display at that scale, in DIPs.
        var screen = new ScreenMetrics(1920 / displayScale, 1080 / displayScale);
        var (cols, rows) = GridGeometry.GridDims(screen);
        foreach (var size in new[] { DesktopIconSize.Small, DesktopIconSize.Medium, DesktopIconSize.Large })
        {
            var cell = DesktopCell.Grid(size);
            var (w, h) = DesktopGrid.IconSize(cell, screen);

            Assert.Equal(Math.Round(w * cols), w * cols, 6);
            Assert.Equal(Math.Round(h * rows), h * rows, 6);

            // Rounded up, by less than a cell: the icon's cell always fits.
            Assert.InRange(w * screen.Width, cell.Width - 1e-6, cell.Width + (screen.Width / cols));
            Assert.InRange(h * screen.Height, cell.Height - 1e-6, cell.Height + (screen.Height / rows));
        }

        // Medium at 100 %: 76 × 100 is 8 × 10 cells of 10 DIPs.
        var medium = DesktopGrid.IconSize(DesktopCell.Grid(DesktopIconSize.Medium), new ScreenMetrics(1920, 1080));
        Assert.Equal(80 / 1920.0, medium.W, 9);
        Assert.Equal(100 / 1080.0, medium.H, 9);
    }

    // ---- [S-11876] where icons arrive ------------------------------------------------------------------

    [Fact]
    public void S11876_the_first_free_spot_is_in_reading_order_round_the_widgets()
    {
        // [S-11888] One grid cell in from the corner.
        var size = (0.2, 0.3);
        Near(new FracRect(0.1, 0.1, 0.2, 0.3), DesktopGrid.FirstFree(size, [], Ten));

        // A clock over the top-left: the next line past its right edge, on the same row.
        Near(new FracRect(0.3, 0.1, 0.2, 0.3), DesktopGrid.FirstFree(size, [new FracRect(0, 0, 0.3, 0.2)], Ten));

        // Edges that only touch do not overlap.
        Near(new FracRect(0.2, 0.1, 0.2, 0.3), DesktopGrid.FirstFree(size, [new FracRect(0, 0, 0.2, 1)], Ten));

        // A full screen has none.
        Assert.Null(DesktopGrid.FirstFree(size, [new FracRect(0, 0, 1, 1)], Ten));
    }

    [Fact]
    public void S11876_the_first_free_spot_stays_inside_the_work_area()
    {
        var screen = new ScreenMetrics(100, 100) { Allowed = new FracRect(0.05, 0.1, 0.9, 0.8) };
        Near(new FracRect(0.2, 0.2, 0.2, 0.2), DesktopGrid.FirstFree((0.2, 0.2), [], screen));

        // No whole box fits: none.
        Assert.Null(DesktopGrid.FirstFree((0.95, 0.2), [], screen));
    }

    [Fact]
    public void S11876_an_arrival_at_a_point_is_centred_there_on_the_grid_or_takes_the_nearest_free_spot()
    {
        var occupied = new List<FracRect>();
        var size = (0.2, 0.2);

        // Centred on 0.55 / 0.55: its corner 0.45 rounds to the line at 0.5.
        Near(new FracRect(0.5, 0.5, 0.2, 0.2), DesktopGrid.Arrive(occupied, size, 0.55, 0.55, Ten));

        // The same point again: the spot is taken by the first, so the nearest free one, among equals the one further up.
        Near(new FracRect(0.5, 0.3, 0.2, 0.2), DesktopGrid.Arrive(occupied, size, 0.55, 0.55, Ten));

        // Without a point: the first free spot in reading order, one cell in from the corner ([S-11888]).
        Near(new FracRect(0.1, 0.1, 0.2, 0.2), DesktopGrid.Arrive(occupied, size, null, null, Ten));
        Assert.Equal(3, occupied.Count);
    }

    [Fact]
    public void S11876_auto_arrange_packs_in_reading_order_and_a_packed_desktop_stays()
    {
        FracRect[] icons = [new(0.5, 0.5, 0.2, 0.2), new(0.1, 0, 0.2, 0.2)];
        var packed = DesktopGrid.Pack(icons, [new FracRect(0.4, 0, 0.1, 0.2)], (0.2, 0.2), Ten);

        // The one higher up first; the second goes past the widget in the way.
        Near(new FracRect(0.1, 0.1, 0.2, 0.2), packed[1]);
        Near(new FracRect(0.5, 0.1, 0.2, 0.2), packed[0]);

        var again = DesktopGrid.Pack([packed[0]!.Value, packed[1]!.Value], [new FracRect(0.4, 0, 0.1, 0.2)], (0.2, 0.2), Ten);
        Assert.All(again, r => Assert.Null(r));
    }

    [Fact]
    public void S11876_a_sort_lays_the_icons_in_reading_order_and_past_the_last_spot_gives_none()
    {
        var cells = DesktopSort.Arrange(3, [new FracRect(0.2, 0, 0.1, 0.2)], (0.2, 0.5), Ten);
        Near(new FracRect(0.3, 0.1, 0.2, 0.5), cells[0]);
        Near(new FracRect(0.5, 0.1, 0.2, 0.5), cells[1]);
        Near(new FracRect(0.7, 0.1, 0.2, 0.5), cells[2]);

        var few = DesktopSort.Arrange(3, [], (0.5, 1), Ten);
        Assert.NotNull(few[1]);
        Assert.Null(few[2]);
    }

    [Fact]
    public void S11876_new_desktop_items_arrive_at_the_first_free_spots_beside_a_widget()
    {
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        home.Widgets.Add(Card("clock", new FracRect(0, 0, 0.3, 0.2)));
        var listing = new[] { new DesktopItem(@"C:\d\a.txt", "a", DesktopItemKind.File), new DesktopItem(@"C:\d\b.txt", "b", DesktopItemKind.File) };

        var plan = DesktopReconcile.Plan(state, listing, []);
        DesktopReconcile.Apply(state, plan, Ten, (20, 30), (item, rect) => Icon(item.Key, item.Key, rect));

        Near(new FracRect(0.3, 0.1, 0.2, 0.3), home.Widgets[1].Rect);
        Near(new FracRect(0.5, 0.1, 0.2, 0.3), home.Widgets[2].Rect);
    }

    [Fact]
    public void S5593_a_new_icon_size_keeps_the_arrangement_on_the_grid()
    {
        // Two icons side by side at 0.2 wide grow to 0.3: still side by side, on grid lines, one cell off the
        // screen's edge ([S-11888]).
        var laid = DesktopGrid.Rescale([new FracRect(0.1, 0.1, 0.2, 0.2), new FracRect(0.3, 0.1, 0.2, 0.2)], [], (0.2, 0.2), (0.3, 0.3), Ten);
        Near(new FracRect(0.1, 0.1, 0.3, 0.3), laid[0]);
        Near(new FracRect(0.4, 0.1, 0.3, 0.3), laid[1]);
    }

    // ---- [S-11877] one selection, one lasso -----------------------------------------------------------

    [Fact]
    public void S11877_a_widget_is_selected_under_its_icon_key_or_its_card_key()
    {
        var own = new WidgetInstance { Id = "o", Config = new DeskIconFake { ItemId = "abc" } };
        Assert.Equal(DesktopItemKeys.Own("abc"), DesktopPicks.KeyOf(own, _ => false));
        Assert.Equal(@"C:\d\a.txt", DesktopPicks.KeyOf(Icon("i", @"C:\d\a.txt", default), _ => true));
        Assert.Null(DesktopPicks.KeyOf(Icon("i", @"C:\d\a.txt", default), _ => false));
        Assert.Equal(DesktopItemKeys.Widget("clock"), DesktopPicks.KeyOf(Card("clock", default), _ => false));

        Assert.Equal("clock", DesktopItemKeys.WidgetOf(DesktopItemKeys.Widget("clock")));
        Assert.Null(DesktopItemKeys.WidgetOf(@"C:\d\a.txt"));
        Assert.False(DesktopItemKeys.IsOwn(DesktopItemKeys.Widget("clock")));
    }

    [Fact]
    public void S11877_the_lasso_selects_icons_and_cards_by_one_overlap_rule()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.3, 0.2)));
        view.Widgets.Add(Card("far", new FracRect(0.8, 0.8, 0.1, 0.1)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);

        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select(w => new DesktopBox(KeyOf(w)!, 0, w.Rect.X * 100, w.Rect.Y * 100, w.Rect.W * 100, w.Rect.H * 100)), _ => null);
        selection.BeginBand(ctrl: false, shift: false);

        // A band from the icon's middle into the clock's corner touches both, not the far card.
        selection.Band(DesktopBand.Hits(new BandRect(5, 5, 25, 15), selection.Ordered));
        selection.EndBand();

        Assert.Equal(["i", "clock"], DesktopPicks.Selected(view, selection, KeyOf));

        // A click on empty desktop (or Escape) clears both kinds.
        selection.Clear();
        Assert.Empty(DesktopPicks.Selected(view, selection, KeyOf));
    }

    // ---- [S-11879] the block move of a mixed selection --------------------------------------------------

    [Fact]
    public void S11879_a_held_selected_widget_carries_the_whole_selection_and_an_unselected_one_moves_alone()
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.3, 0.2)));
        view.Widgets.Add(Card("other", new FracRect(0.8, 0.8, 0.1, 0.1)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select((w, k) => new DesktopBox(KeyOf(w)!, 0, k * 10, 0, 5, 5)), _ => null);
        selection.SelectAll();
        selection.Press(DesktopItemKeys.Widget("other"), ctrl: true, shift: false);

        Assert.Equal(["i", "clock"], DesktopPicks.Moving(view, selection, "clock", KeyOf));
        Assert.Equal(["other"], DesktopPicks.Moving(view, selection, "other", KeyOf));
    }

    [Fact]
    public void S11885_a_locked_move_or_lasso_of_one_member_takes_its_whole_group()
    {
        // Regression (owner, 2026-11-15): "when not in edit mode and moving widgets on the desktop directly it
        // does not respect the grouping of widgets". A member is never moved, nor selected, apart from its
        // group: the units are edit mode's (WidgetGroups.Units, [S-2451]).
        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("a", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("b", new FracRect(0.1, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("c", new FracRect(0.2, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("lone", new FracRect(0.6, 0.6, 0.1, 0.1)));
        var group = WidgetGroups.Group(view, ["a", "b", "c"])!;
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select(w => new DesktopBox(KeyOf(w)!, 0, w.Rect.X * 100, w.Rect.Y * 100, w.Rect.W * 100, w.Rect.H * 100)), _ => null);

        // Pressed and moved while nothing is selected: the held member brings its group, the lone card stays.
        Assert.Equal(["a", "b", "c"], DesktopPicks.Moving(view, selection, "b", KeyOf));
        Assert.Equal(["lone"], DesktopPicks.Moving(view, selection, "lone", KeyOf));

        // A band that touches one member selects (and carries) the whole group.
        selection.BeginBand(ctrl: false, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(22, 2, 5, 5), selection.Ordered));
        selection.EndBand();
        Assert.Equal(["a", "b", "c"], DesktopPicks.Selected(view, selection, KeyOf));
        Assert.All(view.Widgets.Where(w => w.Group == group.Id), w => Assert.True(DesktopPicks.Selects(view, selection, w, KeyOf)));
        Assert.False(DesktopPicks.Selects(view, selection, view.Widgets[3], KeyOf));
        Assert.Equal(["a", "b", "c"], DesktopPicks.Moving(view, selection, "a", KeyOf));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void S11879_a_mixed_block_moves_by_one_translation_on_the_grid_without_drift(double scale)
    {
        var screen = new ScreenMetrics(1920 / scale, 1080 / scale);
        var (cols, rows) = GridGeometry.GridDims(screen);
        FracRect[] start = [new(8.0 / cols, 4.0 / rows, 8.0 / cols, 10.0 / rows), new(20.0 / cols, 6.0 / rows, 30.0 / cols, 12.0 / rows)];

        var delta = BlockGeometry.Move(start, 0.1234, 0.0567, screen, scale);
        var moved = start.Select(r => BlockGeometry.Apply(r, delta, screen)).ToArray();

        // One translation for all: the offsets between them never change.
        Assert.Equal(start[1].X - start[0].X, moved[1].X - moved[0].X, 12);
        Assert.Equal(start[1].Y - start[0].Y, moved[1].Y - moved[0].Y, 12);

        // Whole device pixels: every card's cell moves by the same pixels.
        var before = start.Select(r => GridGeometry.ToPixels(r, screen, scale)).ToArray();
        var after = moved.Select(r => GridGeometry.ToPixels(r, screen, scale)).ToArray();
        Assert.Equal(after[0].X - before[0].X, after[1].X - before[1].X, 9);
        Assert.Equal(after[0].Y - before[0].Y, after[1].Y - before[1].Y, 9);

        // On the grid, to within one whole-pixel step.
        var step = GridGeometry.PixelStep(scale) / screen.Width;
        Assert.InRange(Math.Abs((moved[0].X * cols) - Math.Round(moved[0].X * cols)) / cols, 0, step);
    }

    [Fact]
    public void S11879_icons_dropped_back_on_another_display_keep_their_shape_and_land_on_its_grid()
    {
        // Two icons dragged out and dropped on a display of other DIPs, as the host hands them over (its fractions).
        var screen = new ScreenMetrics(1000, 1000);
        FracRect[] moving = [new(0, 0, 0.08, 0.1), new(0.08, 0, 0.08, 0.1)];
        var dropped = DesktopGrid.Drop(moving, held: 1, 0.5331, 0.4209, screen);

        Near(new FracRect(0.53, 0.42, 0.08, 0.1), dropped[1]);
        Near(new FracRect(0.45, 0.42, 0.08, 0.1), dropped[0]);
        Assert.Same(moving, DesktopGrid.Drop(moving, held: 5, 0.5, 0.5, screen));
    }

    // ---- [S-11878] the drag out ---------------------------------------------------------------------

    [Theory]
    [InlineData(true, true, true, false, false)]   // an icon on the desktop: a move
    [InlineData(true, true, false, false, true)]   // past the desktop (the taskbar, an app's window): the shell's drag
    [InlineData(true, true, true, true, true)]     // over a folder icon on it: the shell's drag
    [InlineData(false, true, false, false, false)] // a card held: never dragged out
    [InlineData(true, false, false, false, false)] // nothing to give (a web link's icon): stays a move
    public void S11878_an_icons_move_becomes_the_shells_drag_only_off_the_desktop_or_over_a_drop_target(bool icon, bool files, bool overDesktop, bool overTarget, bool starts)
        => Assert.Equal(starts, DesktopDragOut.Starts(icon, files, overDesktop, overTarget));
}
