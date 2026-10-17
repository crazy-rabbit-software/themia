using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-884]..[S-887] The bounded undo log and the desktop's layout history built on it.</summary>
public sealed class UndoLogTests
{
    private sealed class Box(string value)
    {
        public string Value { get; } = value;
    }

    // ---------------------------------------------------------------------------------------
    // UndoLog<T>
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void S884_undo_returns_the_before_state_and_redo_returns_what_was_current()
    {
        var log = new UndoLog<Box>();
        log.Record(UndoAction.Delete, new Box("before"));

        Assert.True(log.TryUndo(new Box("after"), out var undo));
        Assert.Equal(UndoAction.Delete, undo.Action);
        Assert.Equal("before", undo.State.Value);
        Assert.False(log.CanUndo);

        Assert.True(log.TryRedo(new Box("before"), out var redo));
        Assert.Equal(UndoAction.Delete, redo.Action);
        Assert.Equal("after", redo.State.Value);
        Assert.True(log.CanUndo);
        Assert.False(log.CanRedo);
    }

    [Fact]
    public void S884_steps_come_back_newest_first()
    {
        var log = new UndoLog<Box>();
        log.Record(UndoAction.Add, new Box("0"));
        log.Record(UndoAction.Move, new Box("1"));
        log.Record(UndoAction.Resize, new Box("2"));

        Assert.Equal(UndoAction.Resize, log.NextUndo);
        Assert.True(log.TryUndo(new Box("3"), out var a));
        Assert.True(log.TryUndo(new Box("2"), out var b));
        Assert.True(log.TryUndo(new Box("1"), out var c));
        Assert.False(log.TryUndo(new Box("0"), out _));

        Assert.Equal(["2", "1", "0"], new[] { a.State.Value, b.State.Value, c.State.Value });
    }

    [Fact]
    public void S884_recording_a_new_change_forgets_the_redo_side()
    {
        var log = new UndoLog<Box>();
        log.Record(UndoAction.Move, new Box("0"));
        Assert.True(log.TryUndo(new Box("1"), out _));
        Assert.True(log.CanRedo);

        log.Record(UndoAction.Delete, new Box("0"));

        Assert.False(log.CanRedo);
        Assert.Null(log.NextRedo);
    }

    [Fact]
    public void S884_past_capacity_the_oldest_step_is_dropped()
    {
        var log = new UndoLog<Box>(capacity: 3);
        for (var i = 0; i < 5; i++)
        {
            log.Record(UndoAction.Move, new Box(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        Assert.Equal(3, log.UndoCount);

        var seen = new List<string>();
        while (log.TryUndo(new Box("x"), out var step))
        {
            seen.Add(step.State.Value);
        }

        Assert.Equal(["4", "3", "2"], seen);
    }

    [Fact]
    public void S884_the_default_capacity_is_the_originals_hundred()
    {
        var log = new UndoLog<Box>();
        for (var i = 0; i < 150; i++)
        {
            log.Record(UndoAction.Move, new Box("b"));
        }

        Assert.Equal(100, log.UndoCount);
    }

    [Fact]
    public void S884_clear_forgets_both_sides()
    {
        var log = new UndoLog<Box>();
        log.Record(UndoAction.Move, new Box("0"));
        log.Record(UndoAction.Move, new Box("1"));
        Assert.True(log.TryUndo(new Box("2"), out _));

        log.Clear();

        Assert.False(log.CanUndo);
        Assert.False(log.CanRedo);
    }

    // ---------------------------------------------------------------------------------------
    // LayoutHistory
    // ---------------------------------------------------------------------------------------

    private static (AppState State, ViewState View, WidgetInstance Clock) Desk()
    {
        var view = ViewState.Create("Desk");
        var clock = WidgetInstance.Create(ClockConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2));
        var notes = WidgetInstance.Create(NotesConfig.Descriptor.Id, new FracRect(0.5, 0.1, 0.2, 0.3));
        view.Widgets.Add(clock);
        view.Widgets.Add(notes);
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        return (state, view, clock);
    }

    [Fact]
    public void S886_a_change_that_moves_nothing_records_nothing()
    {
        var (state, view, _) = Desk();
        var history = new LayoutHistory(state);

        state.Design.WidgetGapPx = 12;
        ((NotesConfig)view.Widgets[1].Config).NoteId = "typed";

        Assert.False(history.Commit(state, UndoAction.Change));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void S885_undo_a_delete_brings_the_widget_back_with_its_config_and_place()
    {
        var (state, view, _) = Desk();
        var notes = view.Widgets[1];
        ((NotesConfig)notes.Config).NoteId = "keep me";
        var history = new LayoutHistory(state);

        view.Widgets.Remove(notes);
        Assert.True(history.Commit(state, UndoAction.Delete));

        Assert.Equal(UndoAction.Delete, history.Undo(state));

        var back = Assert.Single(state.Views[0].Widgets, w => w.Id == notes.Id);
        Assert.Equal(1, state.Views[0].Widgets.IndexOf(back));
        Assert.Equal(notes.Rect, back.Rect);
        Assert.Equal("keep me", ((NotesConfig)back.Config).NoteId);
    }

    [Fact]
    public void S885_redo_takes_the_undone_change_again()
    {
        var (state, view, clock) = Desk();
        var history = new LayoutHistory(state);

        clock.Rect = new FracRect(0.6, 0.6, 0.2, 0.2);
        history.Commit(state, UndoAction.Move);

        history.Undo(state);
        Assert.Equal(0.1, view.Widgets[0].X);

        Assert.Equal(UndoAction.Move, history.Redo(state));
        Assert.Equal(0.6, view.Widgets[0].X);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void S886_undoing_a_move_keeps_content_typed_since()
    {
        var (state, view, clock) = Desk();
        var history = new LayoutHistory(state);

        clock.Rect = new FracRect(0.6, 0.6, 0.2, 0.2);
        history.Commit(state, UndoAction.Move);

        // The user types into the note after moving the clock. Undoing the move must not take the
        // sentence back: content is not part of the arrangement.
        ((NotesConfig)view.Widgets[1].Config).NoteId = "written after the move";
        history.Commit(state, UndoAction.Change);

        history.Undo(state);

        Assert.Equal(0.1, view.Widgets[0].X);
        Assert.Equal("written after the move", ((NotesConfig)view.Widgets[1].Config).NoteId);
    }

    [Fact]
    public void S886_a_surviving_widget_keeps_its_instance_so_its_card_is_kept()
    {
        var (state, view, clock) = Desk();
        var history = new LayoutHistory(state);

        clock.Rect = new FracRect(0.6, 0.6, 0.2, 0.2);
        history.Commit(state, UndoAction.Move);
        history.Undo(state);

        Assert.Same(clock, state.Views[0].Widgets[0]);
        Assert.Same(view, state.Views[0]);
    }

    [Fact]
    public void S887_drift_is_never_lost_under_a_restore()
    {
        var (state, _, clock) = Desk();
        var history = new LayoutHistory(state);

        clock.Rect = new FracRect(0.6, 0.6, 0.2, 0.2);
        history.Commit(state, UndoAction.Move);

        // A change that skipped the commit: the first undo takes back exactly that, not it and the
        // move together.
        clock.W = 0.4;

        Assert.Equal(UndoAction.Change, history.Undo(state));
        Assert.Equal(0.6, clock.X);
        Assert.Equal(0.2, clock.W);

        Assert.Equal(UndoAction.Move, history.Undo(state));
        Assert.Equal(0.1, clock.X);
    }

    [Fact]
    public void S887_rebase_adopts_a_system_change_without_making_it_undoable()
    {
        var (state, _, _) = Desk();
        var history = new LayoutHistory(state);

        state.Views.Add(ViewState.Create("For a new display"));
        history.Rebase(state);

        Assert.Null(history.Undo(state));
        Assert.Equal(2, state.Views.Count);
    }

    [Fact]
    public void S885_undo_a_view_delete_puts_the_view_back_on_its_display()
    {
        var (state, _, _) = Desk();
        var second = ViewState.Create("Second");
        second.Widgets.Add(WidgetInstance.Create(ClockConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2)));
        state.Views.Add(second);
        state.MonitorViews["display-2"] = second.Id;
        state.DefaultViewForNewDisplays = second.Id;
        var history = new LayoutHistory(state);

        Assert.True(ViewOps.Delete(state, second.Id));
        history.Commit(state, UndoAction.DeleteView);
        Assert.False(state.MonitorViews.ContainsKey("display-2"));

        Assert.Equal(UndoAction.DeleteView, history.Undo(state));

        Assert.Equal(["Desk", "Second"], state.Views.Select(v => v.Name));
        Assert.Single(state.Views[1].Widgets);
        Assert.Equal(second.Id, state.MonitorViews["display-2"]);
    }

    [Fact]
    public void S885_undo_a_new_view_repairs_every_reference_to_it()
    {
        var (state, _, _) = Desk();
        var history = new LayoutHistory(state);

        var added = ViewState.Create("Added");
        state.Views.Add(added);
        state.MonitorViews["display-1"] = added.Id;
        state.ActiveViewId = added.Id;
        state.DefaultViewForNewDisplays = added.Id;
        state.ViewBars["display-1"] = new ViewBarSettings { VisibleViews = [added.Id, state.Views[0].Id] };
        history.Commit(state, UndoAction.AddView);

        history.Undo(state);

        Assert.Single(state.Views);
        Assert.False(state.MonitorViews.ContainsKey("display-1"));
        Assert.Equal(state.Views[0].Id, state.ActiveViewId);
        Assert.Null(state.DefaultViewForNewDisplays);
        Assert.Equal([state.Views[0].Id], state.ViewBars["display-1"].VisibleViews);
    }

    [Fact]
    public void S885_undo_z_order_restores_the_order()
    {
        var (state, view, clock) = Desk();
        var history = new LayoutHistory(state);
        var before = view.Widgets.Select(w => w.Id).ToList();

        Assert.True(view.BringToFront(clock.Id));
        history.Commit(state, UndoAction.Arrange);
        Assert.NotEqual(before, view.Widgets.Select(w => w.Id));

        history.Undo(state);

        Assert.Equal(before, view.Widgets.Select(w => w.Id));
    }

    [Fact]
    public void S885_a_widget_moved_to_another_view_comes_back_as_the_same_instance()
    {
        var (state, view, clock) = Desk();
        var other = ViewState.Create("Other");
        state.Views.Add(other);
        var history = new LayoutHistory(state);

        view.Widgets.Remove(clock);
        other.Widgets.Add(clock);
        history.Commit(state, UndoAction.Move);

        history.Undo(state);

        Assert.Same(clock, state.Views[0].Widgets[0]);
        Assert.Empty(state.Views[1].Widgets);
    }

    [Fact]
    public void S885_nothing_to_undo_or_redo_changes_nothing()
    {
        var (state, _, _) = Desk();
        var history = new LayoutHistory(state);

        Assert.Null(history.Undo(state));
        Assert.Null(history.Redo(state));
        Assert.Equal(2, state.Views[0].Widgets.Count);
    }

    [Fact]
    public void S885_clear_starts_the_history_again()
    {
        var (state, _, clock) = Desk();
        var history = new LayoutHistory(state);
        clock.X = 0.5;
        history.Commit(state, UndoAction.Move);

        history.Clear(state);

        Assert.False(history.CanUndo);
        Assert.Null(history.Undo(state));
        Assert.Equal(0.5, clock.X);
    }

    [Fact]
    public void S885_the_version_moves_with_every_step_and_not_otherwise()
    {
        var (state, _, clock) = Desk();
        var history = new LayoutHistory(state);
        var v0 = history.Version;

        history.Commit(state, UndoAction.Change);
        Assert.Equal(v0, history.Version);

        clock.X = 0.5;
        history.Commit(state, UndoAction.Move);
        var v1 = history.Version;
        Assert.NotEqual(v0, v1);

        history.Undo(state);
        Assert.NotEqual(v1, history.Version);
    }

    [Fact]
    public void S885_a_placement_records_add_paste_or_move()
    {
        Assert.Equal(UndoAction.Add, UndoActions.ForPlacement(PlacementMode.Create));
        Assert.Equal(UndoAction.Paste, UndoActions.ForPlacement(PlacementMode.Copy));
        Assert.Equal(UndoAction.Move, UndoActions.ForPlacement(PlacementMode.Move));
    }

    [Fact]
    public void S885_undo_text_names_the_step()
    {
        Assert.Equal("Undid delete", UndoText.Undid(Locale.En, UndoAction.Delete));
        Assert.Equal("Redid move", UndoText.Redid(Locale.En, UndoAction.Move));
        foreach (var locale in LanguageResolver.All)
        {
            foreach (var action in Enum.GetValues<UndoAction>())
            {
                Assert.False(string.IsNullOrWhiteSpace(UndoText.Name(locale, action)));
            }
        }
    }

    [Fact]
    public void S1100_undo_text_is_one_sentence_in_the_asked_language()
        => Assert.Equal("Rückgängig gemacht: Löschen", UndoText.Undid(Locale.De, UndoAction.Delete));
}
