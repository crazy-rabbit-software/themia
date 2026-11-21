using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-15660] A write tells what it changed (the widgets whose config or look changed, the ones that moved, the design,
/// the preferences, a view shown elsewhere), kept until the owner takes it; [S-15664] a write that changed no
/// arrangement leaves the undo history alone.
/// </summary>
public sealed class StateChangeTests
{
    private static readonly WidgetId Clock = new("clock");

    private static AppState Desk()
    {
        var view = ViewState.Create("Desk");
        for (var i = 0; i < 4; i++)
        {
            view.Widgets.Add(WidgetInstance.Create(Clock, new FracRect(0.1 * i, 0.1, 0.1, 0.1)));
        }

        var other = ViewState.Create("Other");
        other.Widgets.Add(WidgetInstance.Create(Clock, new FracRect(0.5, 0.5, 0.1, 0.1)));
        var state = new AppState { Views = [view, other], ActiveViewId = view.Id };
        state.ShowViewOn("display", view.Id);
        return state;
    }

    private static StateWriter Writer(AppState state) => new(state, AppStateStore.Detached(Path.Combine(Path.GetTempPath(), "themia-tests", "state.json")), () => { });

    [Fact]
    public void S15660_a_move_tells_the_moved_widget_and_nothing_to_apply_again()
    {
        var state = Desk();
        var writer = Writer(state);
        var id = state.Views[0].Widgets[2].Id;

        Assert.True(writer.Write(s => s.Views[0].Widgets[2].X = 0.6, UndoAction.Move));
        var change = writer.TakeChange();

        Assert.Equal([id], change.Moved);
        Assert.Empty(change.Configs);
        Assert.False(change.Design || change.Preferences || change.Whole);
        Assert.True(change.Arranged);
        Assert.False(change.Reapplies(id));
    }

    [Fact]
    public void S15660_a_config_written_in_place_before_the_write_is_that_widgets_change()
    {
        var state = Desk();
        var writer = Writer(state);
        var widget = state.Views[0].Widgets[1];

        // A widget's page writes into its config, then asks for the state's write.
        ((ClockConfig)widget.Config).ShowSeconds = true;
        Assert.True(writer.Write(_ => { }));
        var change = writer.TakeChange();

        Assert.Equal([widget.Id], change.Configs);
        Assert.Empty(change.Moved);
        Assert.True(change.Reapplies(widget.Id));
        Assert.False(change.Reapplies(state.Views[0].Widgets[0].Id));
        Assert.False(change.Arranged);
    }

    [Fact]
    public void S15660_a_new_widget_a_look_or_another_config_object_are_that_widgets_change()
    {
        var state = Desk();
        var writer = Writer(state);
        var (scaled, replaced) = (state.Views[0].Widgets[0], state.Views[0].Widgets[3]);
        var added = WidgetInstance.Create(Clock, new FracRect(0.7, 0.7, 0.1, 0.1));

        Assert.True(writer.Write(s =>
        {
            scaled.Scale = 1.5;
            replaced.Config = replaced.Config.Clone();
            s.Views[0].Widgets.Add(added);
        }));
        var change = writer.TakeChange();

        Assert.Equal(new HashSet<string> { scaled.Id, replaced.Id, added.Id }, change.Configs);
        Assert.True(change.Arranged);
    }

    [Fact]
    public void S15660_the_design_and_the_preferences_are_told_apart_and_apply_no_widget_alone()
    {
        var state = Desk();
        var writer = Writer(state);

        Assert.True(writer.Write(s => s.Design.CornerRadiusPx = 3));
        var design = writer.TakeChange();
        Assert.True(design.Design);
        Assert.False(design.Preferences || design.Whole || design.Arranged);
        Assert.Empty(design.Configs);

        Assert.True(writer.Write(s => s.Preferences.LockWidgets = !s.Preferences.LockWidgets));
        var preferences = writer.TakeChange();
        Assert.True(preferences.Preferences);
        Assert.False(preferences.Design || preferences.Arranged);
        Assert.True(preferences.Reapplies(state.Views[0].Widgets[0].Id));
    }

    [Fact]
    public void S15660_another_view_shown_on_a_display_is_drawn_whole()
    {
        var state = Desk();
        var writer = Writer(state);

        Assert.True(writer.Write(s => s.ShowViewOn("display", s.Views[1].Id)));

        Assert.True(writer.TakeChange().Whole);
    }

    [Fact]
    public void S15660_what_the_owner_did_not_take_waits_with_the_next_change_and_is_taken_once()
    {
        var state = Desk();
        var writer = Writer(state);
        var (moved, changed) = (state.Views[0].Widgets[0], state.Views[0].Widgets[1]);

        // A write whose owner drew nothing (relayout: false), then another.
        Assert.True(writer.Write(_ => moved.Y = 0.4));
        Assert.True(writer.Write(_ => ((ClockConfig)changed.Config).ShowSeconds = true));

        var change = writer.TakeChange();
        Assert.Equal([moved.Id], change.Moved);
        Assert.Equal([changed.Id], change.Configs);
        Assert.True(writer.TakeChange().IsNone);
    }

    [Fact]
    public void S15660_a_change_taken_back_or_made_outside_a_write_is_drawn_whole()
    {
        var state = Desk();
        var writer = Writer(state);

        Assert.False(writer.Write(_ => throw new InvalidOperationException("a command's bug")));
        Assert.True(writer.TakeChange().Whole);

        // An undo step restores the state outside a write: the writer is told, and the owner draws it whole.
        state.Views[0].Widgets.RemoveAt(0);
        writer.Rebase();
        Assert.True(writer.TakeChange().Whole);
        Assert.True(writer.Write(_ => { }));
        Assert.True(writer.TakeChange().IsNone);
    }

    [Fact]
    public void S15664_a_change_of_no_arrangement_records_no_step_and_keeps_the_one_before()
    {
        var state = Desk();
        var writer = Writer(state);
        writer.StartHistory();
        var history = writer.History!;

        Assert.True(writer.Write(s => s.Views[0].Widgets[0].X = 0.5, UndoAction.Move));
        var version = history.Version;

        Assert.True(writer.Write(s => s.Design.CornerRadiusPx = 2));
        Assert.True(writer.Write(_ => ((ClockConfig)state.Views[0].Widgets[1].Config).ShowSeconds = true));

        Assert.Equal(version, history.Version);
        Assert.Equal(UndoAction.Move, history.Undo(state));
        Assert.Equal(0, state.Views[0].Widgets[0].X);
    }
}
