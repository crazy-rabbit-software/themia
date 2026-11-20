using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// [S-41] The live state's saves, all on the thread that owns the state (the UI thread, the only one that changes it).
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
    }

    /// <summary>The live state, changed on the owner's thread only.</summary>
    public AppState State { get; }

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

        StateSnapshot snapshot;
        try
        {
            snapshot = StateSnapshot.Take(State);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // A state that cannot be written is a bug; the file keeps the last state that could.
            Log.Error(Scope, "could not take the state to save it; the file keeps the last one written", ex);
            return;
        }

        _store.SaveSoon(snapshot);
    }
}
