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

    // ---- [S-12953] Locked widgets are not selected ------------------------------------------------------

    /// <summary>A view of an icon, a card and an own icon, laid out in the selection by <paramref name="locked"/>'s keys.</summary>
    private static (ViewState View, DesktopSelection Selection, Func<WidgetInstance, string?> KeyOf) LockedDesk(bool locked)
    {
        var view = ViewState.Create("Home");
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("clock", new FracRect(0.2, 0, 0.2, 0.1)));
        view.Widgets.Add(new WidgetInstance { Id = "own", Config = new DeskIconFake { ItemId = "own" }, Rect = new FracRect(0.5, 0, 0.1, 0.1) });
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true, widgetsLocked: locked);
        var selection = new DesktopSelection();
        selection.SetLayout(
            view.Widgets.Select(w => (W: w, Key: KeyOf(w))).Where(p => p.Key is not null)
                .Select(p => new DesktopBox(p.Key!, 0, p.W.Rect.X * 100, p.W.Rect.Y * 100, p.W.Rect.W * 100, p.W.Rect.H * 100)),
            _ => null);
        return (view, selection, KeyOf);
    }

    [Fact]
    public void S12953_locked_icons_are_selected_and_cards_are_refused()
    {
        // Owner, 2026-11-19: "when the widgets are locked you should also not be able to select them except for the
        // desktop icons of course". Before, a locked card was picked as any (the lock only held its move).
        var (view, selection, keyOf) = LockedDesk(locked: true);
        Assert.Null(keyOf(view.Widgets[1]));
        Assert.NotNull(keyOf(view.Widgets[0]));
        Assert.NotNull(keyOf(view.Widgets[2]));
        Assert.True(WidgetLock.Selectable(lockWidgets: true, isIcon: true));
        Assert.False(WidgetLock.Selectable(lockWidgets: true, isIcon: false));
        Assert.True(WidgetLock.Selectable(lockWidgets: false, isIcon: false));

        // A Ctrl or Shift press on a locked card is not a pick: it stays the widget's own press.
        Assert.False(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: true, shift: false, widgetsLocked: true));
        Assert.False(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: false, shift: true, widgetsLocked: true));
        Assert.True(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: true, shift: false, widgetsLocked: false));

        // An icon's press selects it; Ctrl+A takes the icons and never the card.
        selection.Press(keyOf(view.Widgets[0])!, ctrl: false, shift: false);
        selection.Release(keyOf(view.Widgets[0])!);
        Assert.Equal(["i"], DesktopPicks.Selected(view, selection, keyOf));
        selection.SelectAll();
        Assert.Equal(["i", "own"], DesktopPicks.Selected(view, selection, keyOf));
        Assert.False(DesktopPicks.Selects(selection, view.Widgets[1], keyOf));
        Assert.Equal(["i", "own"], DesktopPicks.Moving(view, selection, "i", keyOf, lockWidgets: true));
    }

    [Fact]
    public void S12953_a_lasso_while_locked_catches_icons_only()
    {
        var (view, selection, keyOf) = LockedDesk(locked: true);
        selection.BeginBand(ctrl: false, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(0, 0, 100, 20), selection.Ordered));
        selection.EndBand();

        Assert.Equal(["i", "own"], DesktopPicks.Selected(view, selection, keyOf));
        Assert.DoesNotContain(selection.Ordered, b => b.Key == DesktopItemKeys.Widget("clock"));

        // Unlocked, the same band takes the card too.
        var (_, open, openKeyOf) = LockedDesk(locked: false);
        open.BeginBand(ctrl: false, shift: false);
        open.Band(DesktopBand.Hits(new BandRect(0, 0, 100, 20), open.Ordered));
        open.EndBand();
        Assert.Equal(["i", "clock", "own"], DesktopPicks.Selected(view, open, openKeyOf));
    }

    [Fact]
    public void S12953_turning_the_lock_on_lets_go_of_the_selected_cards_and_keeps_the_icons()
    {
        var (view, selection, openKeyOf) = LockedDesk(locked: false);
        selection.SelectAll();
        Assert.Equal(["i", "clock", "own"], DesktopPicks.Selected(view, selection, openKeyOf));

        // The lock turns on: the next layout has no key for the card, so the selection lets it go.
        string? lockedKeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true, widgetsLocked: true);
        selection.SetLayout(
            view.Widgets.Select(w => (W: w, Key: lockedKeyOf(w))).Where(p => p.Key is not null)
                .Select(p => new DesktopBox(p.Key!, 0, p.W.Rect.X * 100, p.W.Rect.Y * 100, p.W.Rect.W * 100, p.W.Rect.H * 100)),
            _ => null);
        Assert.False(selection.IsSelected(DesktopItemKeys.Widget("clock")));
        Assert.Equal(["i", "own"], DesktopPicks.Selected(view, selection, lockedKeyOf));
        Assert.Equal(2, selection.Count);
    }

    /// <summary>
    /// [S-12954] The host's one desk as it runs a gesture: lays the view out (by <paramref name="locked"/>'s keys) before each
    /// step and again after it, as every repaint of the icons does, so a rule that forgets across a layout fails here.
    /// </summary>
    private sealed class Desk(ViewState view, bool locked)
    {
        public DesktopSelection Selection { get; } = new();

        public string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true, widgetsLocked: locked);

        public void Layout() => Selection.SetLayout(
            view.Widgets.Select(w => (W: w, Key: KeyOf(w))).Where(p => p.Key is not null)
                .Select(p => new DesktopBox(p.Key!, 0, p.W.Rect.X * 100, p.W.Rect.Y * 100, p.W.Rect.W * 100, p.W.Rect.H * 100)),
            _ => null);

        /// <summary>A left click on <paramref name="id"/>: an icon's press is the desk's; a card's only when it picks.</summary>
        public void Click(string id, bool ctrl = false, bool shift = false)
        {
            var widget = view.Widgets.Find(w => w.Id == id)!;
            if (!WidgetLock.IsIcon(widget) && !DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl, shift, locked))
            {
                return;
            }

            if (KeyOf(widget) is not { } key)
            {
                return;
            }

            Layout();
            Selection.Press(key, ctrl, shift);
            Layout();
            Selection.Release(key);
            Layout();
        }

        public void Band(BandRect band)
        {
            Layout();
            Selection.BeginBand(ctrl: false, shift: false);
            Layout();
            Selection.Band(DesktopBand.Hits(band, Selection.Ordered));
            Layout();
            Selection.EndBand();
        }

        public void Key(WidgetKey key)
        {
            Layout();
            _ = Selection.Key(key, shift: false);
            Layout();
        }

        public List<string> Selected => DesktopPicks.Selected(view, Selection, KeyOf);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S12954_unlocked_cards_and_icons_are_picked_together_and_locked_icons_by_every_gesture(bool locked)
    {
        // Owner, 2026-11-19: "selecting mutliple widgets in non edit mode is not possible anymore. only in locked mode it
        // should not be possible." and "the desktop icons should also be selectable in locked mode. make sure of that".
        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("clock", new FracRect(0, 0, 0.2, 0.1)));
        view.Widgets.Add(Card("tasks", new FracRect(0, 0.2, 0.2, 0.2)));
        view.Widgets.Add(Icon("a", @"C:\d\a.txt", new FracRect(0.8, 0, 0.1, 0.1)));
        view.Widgets.Add(Icon("b", @"C:\d\b.txt", new FracRect(0.8, 0.1, 0.1, 0.1)));
        view.Widgets.Add(new WidgetInstance { Id = "own", Config = new DeskIconFake { ItemId = "own" }, Rect = new FracRect(0.8, 0.2, 0.1, 0.1) });
        var desk = new Desk(view, locked);
        desk.Layout();
        List<string> Allowed(params string[] ids) => [.. ids.Where(id => !locked || WidgetLock.IsIcon(view.Widgets.Find(w => w.Id == id)!))];

        // Ctrl+click: the cards while unlocked, the icons always.
        desk.Click("clock", ctrl: true);
        desk.Click("tasks", ctrl: true);
        desk.Click("a", ctrl: true);
        desk.Click("own", ctrl: true);
        Assert.Equal(Allowed("clock", "tasks", "a", "own"), desk.Selected);

        // Ctrl+click again takes one out.
        desk.Click("a", ctrl: true);
        Assert.Equal(Allowed("clock", "tasks", "own"), desk.Selected);

        // A click on one of the selection selects it alone on the release, across the layouts between.
        desk.Click("own");
        Assert.Equal(["own"], desk.Selected);

        // Shift+click: the icons' range; a card's Shift+click is its own press while locked.
        desk.Click("a");
        desk.Click("b", shift: true);
        Assert.Equal(["a", "b"], desk.Selected);
        desk.Click("clock", shift: true);
        Assert.Equal(locked ? ["a", "b"] : ["clock", "tasks", "a"], desk.Selected);

        // The band over everything.
        desk.Band(new BandRect(0, 0, 100, 100));
        Assert.Equal(Allowed("clock", "tasks", "a", "b", "own"), desk.Selected);

        // Ctrl+A and the arrows.
        desk.Click("a");
        desk.Key(WidgetKey.Down);
        Assert.Equal(["b"], desk.Selected);
        desk.Key(WidgetKey.SelectAll);
        Assert.Equal(Allowed("clock", "tasks", "a", "b", "own"), desk.Selected);

        // A move of the selection by an icon carries what the lock lets move.
        Assert.Equal(Allowed("clock", "tasks", "a", "b", "own"), DesktopPicks.Moving(view, desk.Selection, "a", desk.KeyOf, locked));
    }

    [Fact]
    public void S11877_a_locked_move_or_lasso_takes_what_it_touches_and_never_a_neighbour_it_only_touches()
    {
        // There are no groups (owner, 2026-11-17): three cards edge to edge are three widgets. The held one moves
        // alone while nothing is selected; a band takes the cards it touches, and the move carries the selection.
        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("a", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("b", new FracRect(0.1, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("c", new FracRect(0.2, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("lone", new FracRect(0.6, 0.6, 0.1, 0.1)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select(w => new DesktopBox(KeyOf(w)!, 0, w.Rect.X * 100, w.Rect.Y * 100, w.Rect.W * 100, w.Rect.H * 100)), _ => null);

        Assert.Equal(["b"], DesktopPicks.Moving(view, selection, "b", KeyOf));
        Assert.Equal(["lone"], DesktopPicks.Moving(view, selection, "lone", KeyOf));

        selection.BeginBand(ctrl: false, shift: false);
        selection.Band(DesktopBand.Hits(new BandRect(12, 2, 27, 7), selection.Ordered));
        selection.EndBand();
        Assert.Equal(["b", "c"], DesktopPicks.Selected(view, selection, KeyOf));
        Assert.True(DesktopPicks.Selects(selection, view.Widgets[1], KeyOf));
        Assert.False(DesktopPicks.Selects(selection, view.Widgets[0], KeyOf));
        Assert.Equal(["b", "c"], DesktopPicks.Moving(view, selection, "c", KeyOf));
    }

    [Fact]
    public void S11879_widgets_are_picked_with_ctrl_or_shift_and_move_as_one_block_while_unlocked()
    {
        // The owner, 2026-11-19: "in non edit mode when the dashboard is not locked i should be able to select multiple
        // widgets and move them around together." A plain press stays the widget's; Ctrl or Shift picks it.
        Assert.True(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: true, shift: false));
        Assert.True(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: false, shift: true));
        Assert.False(DesktopPicks.PicksOnPress(editMode: false, leftButton: true, ctrl: false, shift: false));
        Assert.False(DesktopPicks.PicksOnPress(editMode: false, leftButton: false, ctrl: true, shift: false));
        Assert.False(DesktopPicks.PicksOnPress(editMode: true, leftButton: true, ctrl: true, shift: false));

        var view = ViewState.Create("Home");
        view.Widgets.Add(Card("clock", new FracRect(0, 0, 0.1, 0.1)));
        view.Widgets.Add(Card("weather", new FracRect(0.5, 0, 0.1, 0.1)));
        view.Widgets.Add(Icon("i", @"C:\d\a.txt", new FracRect(0.8, 0.8, 0.05, 0.05)));
        view.Widgets.Add(Card("tasks", new FracRect(0.3, 0.5, 0.1, 0.1)));
        string? KeyOf(WidgetInstance w) => DesktopPicks.KeyOf(w, _ => true);
        var selection = new DesktopSelection();
        selection.SetLayout(view.Widgets.Select(w => new DesktopBox(KeyOf(w)!, 0, w.Rect.X * 100, w.Rect.Y * 100, w.Rect.W * 100, w.Rect.H * 100)), _ => null);

        // Ctrl+clicks, as the card hands them to the selection (a press and its release).
        foreach (var id in new[] { "clock", "weather" })
        {
            var key = KeyOf(view.Widgets.Find(w => w.Id == id)!)!;
            selection.Press(key, ctrl: true, shift: false);
            selection.Release(key);
        }

        var icon = KeyOf(view.Widgets[2])!;
        selection.Press(icon, ctrl: true, shift: false);
        selection.Release(icon);

        Assert.Equal(["clock", "weather", "i"], DesktopPicks.Selected(view, selection, KeyOf));
        Assert.Equal(["clock", "weather", "i"], DesktopPicks.Moving(view, selection, "weather", KeyOf, lockWidgets: false));
        Assert.Equal(["tasks"], DesktopPicks.Moving(view, selection, "tasks", KeyOf, lockWidgets: false));

        // Ctrl+click again takes one out; with Lock widgets on the picked widgets stay and the icon moves.
        var weather = KeyOf(view.Widgets[1])!;
        selection.Press(weather, ctrl: true, shift: false);
        selection.Release(weather);
        Assert.Equal(["clock", "i"], DesktopPicks.Selected(view, selection, KeyOf));
        Assert.Equal(["i"], DesktopPicks.Moving(view, selection, "i", KeyOf, lockWidgets: true));
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

        var delta = BlockGeometry.Move(start, 0.1234, 0.0567, screen);
        var moved = start.Select(r => BlockGeometry.Apply(r, delta, screen)).ToArray();

        // One translation for all: the offsets between them never change.
        Assert.Equal(start[1].X - start[0].X, moved[1].X - moved[0].X, 12);
        Assert.Equal(start[1].Y - start[0].Y, moved[1].Y - moved[0].Y, 12);

        // [S-11892] Whole cells: every card on the grid's lines, at every scale.
        foreach (var r in moved)
        {
            Assert.Equal(Math.Round(r.X * cols), r.X * cols, 9);
            Assert.Equal(Math.Round(r.Y * rows), r.Y * rows, 9);
        }
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
