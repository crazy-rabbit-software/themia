using Themia.Core.Localization;

namespace Themia.Core.Model;

/// <summary>
/// [S-885] What a recorded change was, so an undo can say what it took back. A closed set: the
/// notice text and the log line come from it, never from a free-form string at the call site.
/// </summary>
public enum UndoAction
{
    /// <summary>Anything without a more specific name: an edit-bar change to widgets' header, background or title ([S-3703]), a view's name or icon.</summary>
    Change = 0,

    /// <summary>A widget placed from the picker.</summary>
    Add = 1,

    /// <summary>Widgets removed.</summary>
    Delete = 2,

    /// <summary>Widgets dragged, or moved by a placement (cut and paste, Move).</summary>
    Move = 3,

    /// <summary>A widget resized by a handle.</summary>
    Resize = 4,

    /// <summary>Copies dropped by a paste.</summary>
    Paste = 5,

    /// <summary>Bring to front / send to back.</summary>
    Arrange = 7,

    /// <summary>A view created.</summary>
    AddView = 9,

    /// <summary>A view and its widgets removed.</summary>
    DeleteView = 10,

    /// <summary>A view copied.</summary>
    DuplicateView = 11,

    /// <summary>A view moved in the order.</summary>
    ReorderView = 12,
}

/// <summary>[S-885] The words a notice uses for an <see cref="UndoAction"/>.</summary>
public static class UndoText
{
    /// <summary>A short noun phrase: "Undid <i>delete</i>".</summary>
    public static string Name(UndoAction action) => Name(Strings.Current, action);

    /// <summary><see cref="Name(UndoAction)"/> in <paramref name="locale"/>.</summary>
    public static string Name(Locale locale, UndoAction action) => Strings.Get(locale, action switch
    {
        UndoAction.Add => Str.Shell_Undo_Add,
        UndoAction.Delete => Str.Shell_Undo_Delete,
        UndoAction.Move => Str.Shell_Undo_Move,
        UndoAction.Resize => Str.Shell_Undo_Resize,
        UndoAction.Paste => Str.Shell_Undo_Paste,
        UndoAction.Arrange => Str.Shell_Undo_Arrange,
        UndoAction.AddView => Str.Shell_Undo_AddView,
        UndoAction.DeleteView => Str.Shell_Undo_DeleteView,
        UndoAction.DuplicateView => Str.Shell_Undo_DuplicateView,
        UndoAction.ReorderView => Str.Shell_Undo_ReorderView,
        _ => Str.Shell_Undo_Change,
    });

    public static string Undid(UndoAction action) => Undid(Strings.Current, action);

    /// <summary>"Undid delete": one sentence per language around the action's name.</summary>
    public static string Undid(Locale locale, UndoAction action)
        => Strings.Format(locale, Str.Shell_Undo_Undid, ("action", Name(locale, action)));

    public static string Redid(UndoAction action) => Redid(Strings.Current, action);

    public static string Redid(Locale locale, UndoAction action)
        => Strings.Format(locale, Str.Shell_Undo_Redid, ("action", Name(locale, action)));
}

/// <summary>[S-885] Which step a placement records: new from the picker, copies, or the same widgets moved.</summary>
public static class UndoActions
{
    public static UndoAction ForPlacement(PlacementMode mode) => mode switch
    {
        PlacementMode.Create => UndoAction.Add,
        PlacementMode.Copy => UndoAction.Paste,
        _ => UndoAction.Move,
    };
}

/// <summary>One step in an <see cref="UndoLog{T}"/>: what it was, and the state to go back to.</summary>
public readonly record struct UndoEntry<T>(UndoAction Action, T State);

/// <summary>
/// [S-884] A bounded undo / redo history of whole snapshots, newest last.
/// <para>
/// Snapshots rather than inverse operations: an inverse has to be written (and kept right) for
/// every kind of change, and one that is subtly wrong corrupts the layout silently. A snapshot is
/// right by construction — undo is "go back to what it was" — and the caller keeps snapshots small
/// (<see cref="LayoutShape"/> shares the widget configs instead of copying them).
/// </para>
/// <para>
/// The usual editor rules: recording a new change forgets what could be redone; undo moves the
/// current state onto the redo side, so redo is the exact reverse; past <see cref="Capacity"/> the
/// oldest step is dropped. Pure and single-threaded — the host calls it on the UI thread.
/// </para>
/// </summary>
public sealed class UndoLog<T>
    where T : class
{
    /// <summary>[S-884] How many steps are kept, as in the original's operation log.</summary>
    public const int DefaultCapacity = 100;

    private readonly List<UndoEntry<T>> _undo = [];
    private readonly List<UndoEntry<T>> _redo = [];

    public UndoLog(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>The step Ctrl+Z would take back, or null.</summary>
    public UndoAction? NextUndo => _undo.Count > 0 ? _undo[^1].Action : null;

    /// <summary>The step Ctrl+Y would put back, or null.</summary>
    public UndoAction? NextRedo => _redo.Count > 0 ? _redo[^1].Action : null;

    /// <summary>Record a change: <paramref name="before"/> is what undoing it returns to.</summary>
    public void Record(UndoAction action, T before)
    {
        ArgumentNullException.ThrowIfNull(before);

        _redo.Clear();
        Push(_undo, new UndoEntry<T>(action, before));
    }

    /// <summary>
    /// Take back the newest change. <paramref name="current"/> goes onto the redo side; the result's
    /// <see cref="UndoEntry{T}.State"/> is what to restore. False, changing nothing, when there is nothing to undo.
    /// </summary>
    public bool TryUndo(T current, out UndoEntry<T> step) => Move(_undo, _redo, current, out step);

    /// <summary>Put back the newest undone change. The mirror of <see cref="TryUndo"/>.</summary>
    public bool TryRedo(T current, out UndoEntry<T> step) => Move(_redo, _undo, current, out step);

    /// <summary>Forget everything: after an import or a reset there is no "before" to go back to.</summary>
    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private bool Move(List<UndoEntry<T>> from, List<UndoEntry<T>> to, T current, out UndoEntry<T> step)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (from.Count == 0)
        {
            step = default;
            return false;
        }

        step = from[^1];
        from.RemoveAt(from.Count - 1);
        Push(to, new UndoEntry<T>(step.Action, current));
        return true;
    }

    private void Push(List<UndoEntry<T>> side, UndoEntry<T> entry)
    {
        side.Add(entry);
        if (side.Count > Capacity)
        {
            side.RemoveAt(0);
        }
    }
}
