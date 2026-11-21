using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// [S-884] [S-41] The live state's one write path, all on the thread that owns the state (the UI thread, the only one
/// that changes it): a change applied and settled, committed to the undo history (<see cref="Write"/>), and saved.
/// <para>
/// A change asks for a save (<see cref="SaveSoon"/>); the owner's one-shot timer, restarted by every ask, calls
/// <see cref="SaveNow"/> once the changes pause (<see cref="SaveDelay"/>: a burst of changes is one write); that takes
/// the state's bytes there (<see cref="StateSnapshot"/>) and hands them to the store, which writes them off the
/// owner's thread through its one gate. Nothing that leaves the owner's thread holds the live state.
/// </para>
/// </summary>
public sealed class StateWriter
{
    private const string Scope = "store";

    /// <summary>[S-41] The quiet time after the last asked-for save before the state is taken and written.</summary>
    public static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(400);

    private readonly AppStateStore _store;
    private readonly Action _arm;
    private bool _due;

    /// <summary>[S-15660] The state as the last write (or the start) left it.</summary>
    private StateMarks? _marks;

    /// <summary>[S-15660] What the writes changed since the owner last took it (<see cref="TakeChange"/>).</summary>
    private StateChange _pending = StateChange.None;

    /// <param name="state">The live state.</param>
    /// <param name="store">Where it is written.</param>
    /// <param name="arm">
    /// Start the owner's one-shot timer of <see cref="SaveDelay"/>, or start it again when it runs; its tick calls
    /// <see cref="SaveNow"/> on the owner's thread.
    /// </param>
    public StateWriter(AppState state, AppStateStore store, Action arm)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(arm);

        State = state;
        _store = store;
        _arm = arm;
        _marks = StateMarks.Of(state);
    }

    /// <summary>The live state, changed on the owner's thread only.</summary>
    public AppState State { get; }

    /// <summary>[S-884] The desktop's undo history, from <see cref="StartHistory"/> on; null before.</summary>
    public LayoutHistory? History { get; private set; }

    /// <summary>[S-884] The undo history starts from the state as it is now (the desktop as built).</summary>
    public void StartHistory() => History ??= new LayoutHistory(State);

    /// <summary>
    /// [S-884] Apply a change: <paramref name="change"/>, then the owner's <paramref name="settle"/> (what follows every
    /// change in the same step; true when it changed something no undo takes back, so the change is adopted without a
    /// step), then the commit to the undo history (a step named <paramref name="undo"/> when the arrangement changed;
    /// a null <paramref name="undo"/> adopts the change without one, [S-5520]; <paramref name="effect"/> is what the
    /// step did outside the state, [S-5597]), then a save asked for.
    /// <para>
    /// [S-889] When the change or the settle throws, the change is taken back whole: the state is made what it was
    /// before (its views and widgets the same instances, so their cards stay), nothing is committed and no save is
    /// asked for, and the error is logged. Returns false then: the owner says so and draws the state again.
    /// </para>
    /// <para>
    /// [S-15660] What the write changed (and what was written in place since the last one) is told by the state's marks
    /// before and after it and kept for the owner to take (<see cref="TakeChange"/>). [S-15664] A write that changed no
    /// arrangement (<see cref="StateChange.Arranged"/>) and did nothing outside the state leaves the history as it was:
    /// no snapshot is taken for it.
    /// </para>
    /// </summary>
    public bool Write(Action<AppState> change, UndoAction? undo = UndoAction.Change, IUndoEffect? effect = null, Func<bool>? settle = null)
    {
        ArgumentNullException.ThrowIfNull(change);

        // [S-889] The way back, taken before anything changes: the whole state as bytes, and its views and widgets.
        var before = TakeOrNull("before a change; a change that throws cannot be taken back");
        var instances = Instances.Of(State);

        try
        {
            change(State);

            if (settle?.Invoke() == true)
            {
                undo = null;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a change threw; it is taken back whole: no undo step, nothing saved", ex);
            TakeBack(before, instances);
            Rebase();
            return false;
        }

        var marks = StateMarks.Of(State);
        var changed = marks.Since(_marks);
        _marks = marks;
        _pending = _pending.With(changed);

        if (undo is { } step)
        {
            if (changed.Arranged || effect is not null)
            {
                History?.Commit(State, step, effect);
            }
        }
        else if (changed.Arranged)
        {
            History?.Rebase(State);
        }

        SaveSoon();
        return true;
    }

    /// <summary>
    /// [S-15660] What the writes changed since this was last asked (a write whose owner drew nothing leaves its change
    /// here for the next), and nothing from now on.
    /// </summary>
    public StateChange TakeChange()
    {
        var taken = _pending;
        _pending = StateChange.None;
        return taken;
    }

    /// <summary>
    /// [S-15660] The state was changed outside a write (an undo step, a change taken back): its marks are taken again
    /// and the owner draws it whole (<see cref="StateChange.All"/> is what is left to take).
    /// </summary>
    public void Rebase()
    {
        _marks = StateMarks.Of(State);
        _pending = StateChange.All;
    }

    /// <summary>[S-889] Make the state what <paramref name="before"/> holds again; said in the log when it cannot be.</summary>
    private void TakeBack(StateSnapshot? before, Instances instances)
    {
        if (before is null)
        {
            Log.Error(Scope, "the change could not be taken back: there is no state from before it");
            return;
        }

        try
        {
            Restore(State, before.Read(), instances);
        }
        catch (Exception ex)
        {
            // A restore that throws is a bug of its own; the state stays as the change left it, unsaved and no step.
            Log.Error(Scope, "the change could not be taken back", ex);
        }
    }

    /// <summary>The views and widgets of a state by id, as instances: what a restore puts back in place.</summary>
    private readonly record struct Instances(Dictionary<string, ViewState> Views, Dictionary<string, WidgetInstance> Widgets)
    {
        public static Instances Of(AppState state)
        {
            var views = new Dictionary<string, ViewState>(state.Views.Count, StringComparer.Ordinal);
            var widgets = new Dictionary<string, WidgetInstance>(StringComparer.Ordinal);
            foreach (var view in state.Views)
            {
                views.TryAdd(view.Id, view);
                foreach (var widget in view.Widgets)
                {
                    widgets.TryAdd(widget.Id, widget);
                }
            }

            return new(views, widgets);
        }
    }

    /// <summary>
    /// [S-889] Make <paramref name="live"/> <paramref name="was"/> again, in place. A view or widget that was there before
    /// is that instance again (its card is kept), its values <paramref name="was"/>'s; its config stays the same object
    /// unless the change wrote into it, and is then the one it was. Everything else of the state is
    /// <paramref name="was"/>'s (<see cref="AppState.CopyFrom"/>).
    /// </summary>
    private static void Restore(AppState live, AppState was, Instances before)
    {
        var views = new List<ViewState>(was.Views.Count);
        foreach (var wasView in was.Views)
        {
            var view = wasView;
            if (before.Views.TryGetValue(wasView.Id, out var kept))
            {
                kept.CopyFrom(wasView);
                view = kept;
            }

            var widgets = new List<WidgetInstance>(wasView.Widgets.Count);
            foreach (var wasWidget in wasView.Widgets)
            {
                if (!before.Widgets.TryGetValue(wasWidget.Id, out var widget))
                {
                    widgets.Add(wasWidget);
                    continue;
                }

                widget.CopyArrangementFrom(wasWidget);
                if (!SameContent(widget.Config, wasWidget.Config))
                {
                    widget.Config = wasWidget.Config;
                }

                widgets.Add(widget);
            }

            view.Widgets = widgets;
            views.Add(view);
        }

        was.Views = views;
        live.CopyFrom(was);
    }

    /// <summary>Whether a config holds what it held before: the same document.</summary>
    private static bool SameContent(WidgetConfig? now, WidgetConfig was)
    {
        if (now is null)
        {
            return false;
        }

        try
        {
            return JsonSerializer.Serialize(now, StateJson.WidgetConfig) == JsonSerializer.Serialize(was, StateJson.WidgetConfig);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            // ignore: a config the change left unwritable is not what it was; the one it was is put back
            return false;
        }
    }

    /// <summary>Whether a save was asked for and not yet taken.</summary>
    public bool SaveDue => _due;

    /// <summary>The state changed: save it once the changes pause.</summary>
    public void SaveSoon()
    {
        _due = true;
        _arm();
    }

    /// <summary>The owner's timer ran out: take the state now, on this thread, and hand it to the store.</summary>
    public void SaveNow()
    {
        if (!_due)
        {
            return;
        }

        _due = false;
        Hand();
    }

    /// <summary>
    /// [S-42] Exit: the state as it is now handed over, asked for or not (closing the surfaces lets a widget write its
    /// last words into its config), and every write landed.
    /// </summary>
    public Task FlushAsync()
    {
        _due = false;
        Hand();
        return _store.FlushAsync();
    }

    private void Hand()
    {
        // [S-2601] A detached store writes nothing: nothing is taken for it.
        if (!_store.Persists)
        {
            return;
        }

        if (TakeOrNull("to save it; the file keeps the last one written") is { } snapshot)
        {
            _store.SaveSoon(snapshot);
        }
    }

    /// <summary>
    /// The state's bytes now, or null when it cannot be written (a bug: a value JSON has no word for, a NaN in a rect);
    /// said in the log with <paramref name="why"/>. Never throws: it runs from the owner's timer and from every change.
    /// </summary>
    private StateSnapshot? TakeOrNull(string why)
    {
        try
        {
            return StateSnapshot.Take(State);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"could not take the state {why}", ex);
            return null;
        }
    }
}
