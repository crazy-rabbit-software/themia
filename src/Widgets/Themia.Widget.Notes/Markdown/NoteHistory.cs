namespace Themia.Core.Notes;

/// <summary>The note and its selection at one moment.</summary>
public readonly record struct NoteSnapshot(string Text, int Start, int End);

/// <summary>
/// [S-1360] The note editor's undo and redo. A port of the original <c>history.ts</c>.
/// <para>
/// Kept here rather than left to the text control because the control's own stack would also hold
/// every formatting pass the editor applies after a keystroke — Ctrl+Z would first undo the bold
/// it just drew, then the dim on a marker, and only then the typing. So the control's stack is
/// switched off and this one, which only ever sees the text, is the one stack.
/// </para>
/// <para>
/// Timer-free: a run of single-character typing is coalesced by comparing the two strings, never by
/// how fast it was typed, and Enter starts a new step so undo lands on line boundaries. Same input,
/// same stack, every run.
/// </para>
/// </summary>
public sealed class NoteHistory
{
    /// <summary>Retained steps. A note is small, but a paste loop must not grow this for ever.</summary>
    public const int Limit = 200;

    private readonly LinkedList<NoteSnapshot> _past = new();
    private readonly Stack<NoteSnapshot> _future = new();
    private bool _lastWasTyping;

    public NoteHistory(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        State = new NoteSnapshot(text, text.Length, text.Length);
    }

    /// <summary>What the editor is showing.</summary>
    public NoteSnapshot State { get; private set; }

    public bool CanUndo => _past.Count > 0;

    public bool CanRedo => _future.Count > 0;

    /// <summary>
    /// True when <paramref name="next"/> is <paramref name="previous"/> with exactly one
    /// non-newline character inserted — the shape of an ordinary keystroke.
    /// </summary>
    public static bool IsTypingRun(string previous, string next)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(next);

        if (next.Length != previous.Length + 1)
        {
            return false;
        }

        var i = 0;
        while (i < previous.Length && previous[i] == next[i])
        {
            i++;
        }

        if (next[i] == '\n')
        {
            return false;
        }

        return previous.AsSpan(i).SequenceEqual(next.AsSpan(i + 1));
    }

    /// <summary>Start over at <paramref name="text"/> with no history: another note, or a change from elsewhere.</summary>
    public void Reset(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        _past.Clear();
        _future.Clear();
        _lastWasTyping = false;
        State = new NoteSnapshot(text, text.Length, text.Length);
    }

    /// <summary>Record the editor's state after a change. A caret move alone is not a step.</summary>
    public void Record(string text, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.Equals(text, State.Text, StringComparison.Ordinal))
        {
            State = new NoteSnapshot(text, start, end);
            return;
        }

        _future.Clear();
        var typing = IsTypingRun(State.Text, text);

        if (!(typing && _lastWasTyping))
        {
            _past.AddLast(State);
            if (_past.Count > Limit)
            {
                _past.RemoveFirst();
            }
        }

        _lastWasTyping = typing;
        State = new NoteSnapshot(text, start, end);
    }

    /// <summary>Step back; null when there is nothing to undo.</summary>
    public NoteSnapshot? Undo()
    {
        if (_past.Last is not { } last)
        {
            return null;
        }

        _past.RemoveLast();
        _future.Push(State);
        State = last.Value;
        _lastWasTyping = false;
        return State;
    }

    /// <summary>Step forward again; null when there is nothing to redo.</summary>
    public NoteSnapshot? Redo()
    {
        if (!_future.TryPop(out var next))
        {
            return null;
        }

        _past.AddLast(State);
        State = next;
        _lastWasTyping = false;
        return State;
    }
}
