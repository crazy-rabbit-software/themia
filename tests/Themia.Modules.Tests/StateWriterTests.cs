using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-41] [S-42] The live state's saves: asked for by every change, taken on the owner's thread (here, the test's) once
/// the owner's timer runs out, written off it by the store.
/// </summary>
public sealed class StateWriterTests : IDisposable
{
    private static readonly WidgetId Clock = new("clock");

    private readonly string _dir;
    private readonly string _path;
    private int _armed;

    public StateWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    private static AppState Desk()
    {
        var view = ViewState.Create("Desk");
        for (var i = 0; i < 4; i++)
        {
            view.Widgets.Add(WidgetInstance.Create(Clock, new FracRect(0.1 * i, 0.1, 0.1, 0.1)));
        }

        return new AppState { Views = [view], ActiveViewId = view.Id };
    }

    private StateWriter Writer(AppState state, AppStateStore store) => new(state, store, () => _armed++);

    /// <summary>The file holds exactly <paramref name="state"/> as it is now.</summary>
    private void Holds(AppState state)
        => Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(state, StateJson.AppState), File.ReadAllBytes(_path));

    [Fact]
    public async Task S41_a_change_while_a_write_is_out_neither_throws_nor_is_lost()
    {
        var state = Desk();
        var store = new AppStateStore(_path);
        var held = new TaskCompletionSource();
        store.WritesHeldUntil = held.Task;
        var writer = Writer(state, store);

        writer.SaveSoon();
        writer.SaveNow();

        // While that write is out, the UI thread goes on: widgets added and removed, the list replaced (an undo), a
        // rect written field by field (a drag). A write that read the live lists threw here and lost itself.
        var view = state.Views[0];
        for (var i = 0; i < 64; i++)
        {
            view.Widgets.Add(WidgetInstance.Create(Clock, new FracRect(0.5, 0.5, 0.1, 0.1)));
        }

        view.Widgets.RemoveAll(w => w.X < 0.15);
        view.Widgets = [.. view.Widgets];
        view.Widgets[0].X = 0.7;
        view.Widgets[0].Y = 0.8;
        writer.SaveSoon();
        writer.SaveNow();

        held.SetResult();
        await store.FlushAsync();

        Holds(state);
        Assert.Equal(LoadOutcome.Loaded, new AppStateStore(_path).Load().Outcome);
    }

    [Fact]
    public async Task S41_a_burst_of_changes_is_one_take_when_the_owners_timer_runs_out()
    {
        var state = Desk();
        var store = new AppStateStore(_path);
        var writer = Writer(state, store);

        writer.SaveSoon();
        state.Views[0].Widgets[0].X = 0.5;
        writer.SaveSoon();
        state.Views[0].Widgets[0].X = 0.6;
        writer.SaveSoon();

        Assert.Equal(3, _armed);
        Assert.True(writer.SaveDue);
        await store.FlushAsync();
        Assert.False(File.Exists(_path));

        writer.SaveNow();
        await store.FlushAsync();
        Holds(state);

        // Nothing asked since: the next tick takes nothing.
        File.Delete(_path);
        writer.SaveNow();
        await store.FlushAsync();
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public async Task S42_the_exit_flush_writes_the_state_as_the_closed_surfaces_left_it()
    {
        var state = Desk();
        var store = new AppStateStore(_path);
        var writer = Writer(state, store);

        // A widget writes its last words into its config as its surface closes; no save is asked for.
        state.Views[0].Widgets[1].ItemTextColor = "#123456";

        await writer.FlushAsync();

        Holds(state);
        Assert.False(writer.SaveDue);
    }

    [Fact]
    public async Task S889_a_change_that_throws_leaves_the_state_the_undo_log_and_the_file_as_they_were()
    {
        var state = Desk();
        var store = new AppStateStore(_path);
        var writer = Writer(state, store);
        writer.StartHistory();
        var history = writer.History!;

        writer.Write(s => s.Views[0].Widgets[0].X = 0.5, UndoAction.Move);
        writer.SaveNow();
        await store.FlushAsync();

        var file = File.ReadAllBytes(_path);
        var json = JsonSerializer.Serialize(state, StateJson.AppState);
        var view = state.Views[0];
        var widgets = view.Widgets.ToList();
        var config = widgets[0].Config;
        var (steps, version, armed) = (history.UndoCount, history.Version, _armed);

        // A command with a bug: it gets half way through the state and throws.
        var written = writer.Write(
            s =>
            {
                s.Views[0].Widgets.RemoveAt(1);
                s.Views[0].Widgets[0].X = 0.9;
                s.Views[0].Widgets[0].Scale = 2;
                ((ClockConfig)s.Views[0].Widgets[2].Config).ShowSeconds = true;
                s.Views[0].Widgets = [.. s.Views[0].Widgets];
                s.Views.Add(ViewState.Create("Half made"));
                s.Design.CornerRadiusPx = 30;
                s.Preferences.LockWidgets = true;
                throw new InvalidOperationException("a command's bug");
            },
            UndoAction.Delete);

        Assert.False(written);
        Assert.Equal(json, JsonSerializer.Serialize(state, StateJson.AppState));
        Assert.Same(view, Assert.Single(state.Views));
        Assert.Equal(widgets, view.Widgets);
        Assert.Same(config, view.Widgets[0].Config);
        Assert.Equal((steps, version), (history.UndoCount, history.Version));
        Assert.Equal(armed, _armed);
        Assert.False(writer.SaveDue);

        writer.SaveNow();
        await store.FlushAsync();
        Assert.Equal(file, File.ReadAllBytes(_path));

        // And the history is whole: the step before the throw is still the one Ctrl+Z takes back.
        Assert.Equal(UndoAction.Move, history.Undo(state));
        Assert.Equal(0, state.Views[0].Widgets[0].X);
    }

    [Fact]
    public void S889_a_settle_that_throws_takes_the_change_back_too()
    {
        var state = Desk();
        var writer = Writer(state, new AppStateStore(_path));
        writer.StartHistory();
        var json = JsonSerializer.Serialize(state, StateJson.AppState);

        var written = writer.Write(
            s => s.Views[0].Widgets[0].X = 0.4,
            UndoAction.Move,
            settle: () => throw new InvalidOperationException("a refit's bug"));

        Assert.False(written);
        Assert.Equal(json, JsonSerializer.Serialize(state, StateJson.AppState));
        Assert.False(writer.History!.CanUndo);
        Assert.False(writer.SaveDue);
    }

    [Fact]
    public async Task S2601_a_detached_store_is_handed_nothing()
    {
        var state = Desk();
        var store = AppStateStore.Detached(_path);
        var writer = Writer(state, store);

        writer.SaveSoon();
        writer.SaveNow();
        await writer.FlushAsync();

        Assert.False(File.Exists(_path));
    }
}
