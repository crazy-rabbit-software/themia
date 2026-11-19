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
