using Themia.Core.Diagnostics;
using Themia.Core.Notes;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Notes;

/// <summary>
/// [S-1348] The notes kept on this PC, as every Notes widget and its properties menu see them: one
/// pool, so two widgets can show one note, typing in one shows up in the other, and deleting a widget
/// never takes its note.
/// <para>
/// The UI thread is the only mutator. Every change lands in memory at once — the list, the body —
/// raises <see cref="Changed"/>, and queues its disk write; the writes run off the UI thread, one at
/// a time and in the order they were made (create before write, write before delete), so the
/// <see cref="NoteStore"/> sees a single writer. A body typed faster than the disk takes it is
/// coalesced: a queued write to the same note is replaced, never stacked.
/// </para>
/// <para>
/// [S-1342] A write to a note that is no longer in the pool is dropped rather than recreating the
/// note: a widget still showing a note that was deleted elsewhere must not bring it back.
/// </para>
/// </summary>
internal sealed class NotePool : INotes
{
    private const string Scope = "notes";

    private readonly NoteStore _store;
    private readonly Dictionary<string, string> _bodies = new(StringComparer.Ordinal);
    private readonly LinkedList<Pending> _queue = new();
    private List<NoteMeta> _notes;
    private Task _saving = Task.CompletedTask;
    private bool _draining;

    public NotePool(NoteStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _notes = [.. store.List()];
        Log.Info(Scope, $"{_notes.Count} note(s) in {store.Directory}");
    }

    /// <summary>
    /// [S-1348] Raised on the UI thread after every change: with a note's id when only its body
    /// changed, with null when the list did (created, renamed, deleted). A widget re-reads only when
    /// the id is its own or null.
    /// </summary>
    public event Action<string?>? Changed;

    /// <summary>Raised on the UI thread when <see cref="Saving"/> or <see cref="WriteFailed"/> may have changed.</summary>
    public event Action? SaveStateChanged;

    /// <summary>Every note, in list order (title, then id).</summary>
    public IReadOnlyList<NoteMeta> Notes => _notes;

    /// <summary>True when the last disk write failed; cleared by the next one that lands.</summary>
    public bool WriteFailed { get; private set; }

    /// <summary>True while a write is queued or running.</summary>
    public bool Saving => _queue.Count > 0 || _draining;

    public NoteMeta? Find(string? id) => id is null ? null : _notes.Find(n => n.Id == id);

    /// <summary>
    /// [S-1345] A body: the latest typed (even if not on disk yet), else read from disk once.
    /// <see cref="NoteReadStatus.Missing"/> for a note the pool does not have.
    /// </summary>
    public NoteRead Read(string? id)
    {
        if (Find(id) is null)
        {
            return NoteRead.Missing;
        }

        if (_bodies.TryGetValue(id!, out var cached))
        {
            return new NoteRead(NoteReadStatus.Found, cached);
        }

        var read = _store.Read(id);
        if (read.Status == NoteReadStatus.Found)
        {
            _bodies[id!] = read.Text;
        }

        return read;
    }

    /// <summary>[S-1346] A new note named uniquely from <paramref name="baseName"/>; its id.</summary>
    public string Create(string baseName, string content = "")
    {
        ArgumentNullException.ThrowIfNull(content);

        var id = NoteStore.NewId();
        var title = NoteStore.UniqueTitle(_notes, baseName);
        _notes = NoteStore.Sorted([.. _notes, new NoteMeta(id, title, DateTimeOffset.UtcNow.ToUnixTimeSeconds())]);
        _bodies[id] = content;

        Enqueue(new Pending(PendingKind.Create, id, title, content));
        RaiseChanged(null);
        return id;
    }

    /// <summary>[S-1348] Replace a body. Dropped, with a warning, when the note is gone.</summary>
    public void Write(string? id, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (Find(id) is null)
        {
            Log.Warn(Scope, $"a write to note {id} was dropped: it no longer exists");
            return;
        }

        if (_bodies.TryGetValue(id!, out var current) && string.Equals(current, content, StringComparison.Ordinal))
        {
            return;
        }

        _bodies[id!] = content;

        // A write to this note still waiting in the queue just takes the newer text.
        for (var node = _queue.First; node is not null; node = node.Next)
        {
            if (node.Value.Kind == PendingKind.Write && node.Value.Id == id)
            {
                node.Value.Content = content;
                RaiseChanged(id);
                return;
            }
        }

        Enqueue(new Pending(PendingKind.Write, id!, null, content));
        RaiseChanged(id);
    }

    public void Rename(string id, string title)
    {
        if (Find(id) is not { } note)
        {
            return;
        }

        var clean = NoteStore.CleanTitle(title);
        if (string.Equals(note.Title, clean, StringComparison.Ordinal))
        {
            return;
        }

        _notes = NoteStore.Sorted(_notes.Select(n => n.Id == id ? n with { Title = clean } : n));
        Enqueue(new Pending(PendingKind.Rename, id, clean, null));
        RaiseChanged(null);
    }

    /// <summary>[S-1347] Delete a note. Every widget showing it says so; none is handed another note.</summary>
    public void Delete(string id)
    {
        if (Find(id) is null)
        {
            return;
        }

        _notes = _notes.FindAll(n => n.Id != id);
        _bodies.Remove(id);

        // Its queued writes are moot; a queued create stays, so the delete has something to delete.
        for (var node = _queue.First; node is not null;)
        {
            var next = node.Next;
            if (node.Value.Id == id && node.Value.Kind is PendingKind.Write or PendingKind.Rename)
            {
                _queue.Remove(node);
            }

            node = next;
        }

        Enqueue(new Pending(PendingKind.Delete, id, null, null));
        RaiseChanged(null);
    }

    /// <summary>Wait until everything changed so far is on disk. For shutdown.</summary>
    public async Task FlushAsync()
    {
        while (_draining || !_saving.IsCompleted)
        {
            await _saving.ConfigureAwait(true);
        }
    }

    private void Enqueue(Pending pending)
    {
        _queue.AddLast(pending);

        if (!_draining)
        {
            // Set before the drain starts, and cleared by it before it reports: a change made from a
            // save-state listener must start a new drain rather than wait on one that is finishing.
            _draining = true;
            _saving = DrainAsync();
            RaiseSaveState();
        }
    }

    /// <summary>One write at a time, in order, off the UI thread; resumes on it to take the next.</summary>
    private async Task DrainAsync()
    {
        while (_queue.First is { } node)
        {
            _queue.RemoveFirst();
            var pending = node.Value;
            bool ok;

            try
            {
                ok = await Task.Run(() => Apply(pending)).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                // Any failure, not only I/O: an exception escaping here would stop the queue for good.
                // What is in memory is still right; the next change to this note tries the disk again.
                Log.Error(Scope, $"could not {pending.Kind.ToString().ToLowerInvariant()} note {pending.Id}", ex);
                ok = false;
            }

            WriteFailed = !ok;
        }

        _draining = false;
        RaiseSaveState();
    }

    private void RaiseSaveState()
    {
        try
        {
            SaveStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            // A status line that throws must not stop the queue.
            Log.Error(Scope, "a note save-state listener threw", ex);
        }
    }

    private bool Apply(Pending pending)
    {
        switch (pending.Kind)
        {
            case PendingKind.Create:
                if (_store.Create(pending.Id, pending.Title, pending.Content ?? string.Empty) is null)
                {
                    Log.Error(Scope, $"note {pending.Id} could not be created: its id was refused");
                    return false;
                }

                return true;

            case PendingKind.Write:
                return Report(pending, _store.Write(pending.Id, pending.Content ?? string.Empty));

            case PendingKind.Rename:
                return Report(pending, _store.Rename(pending.Id, pending.Title));

            default:
                return Report(pending, _store.Delete(pending.Id));
        }
    }

    private static bool Report(Pending pending, NoteOutcome outcome)
    {
        if (outcome == NoteOutcome.Done)
        {
            return true;
        }

        Log.Error(Scope, $"the store refused to {pending.Kind.ToString().ToLowerInvariant()} note {pending.Id}: {outcome}");
        return false;
    }

    private void RaiseChanged(string? id)
    {
        if (Changed is not { } changed)
        {
            return;
        }

        foreach (var listener in changed.GetInvocationList().Cast<Action<string?>>())
        {
            try
            {
                listener(id);
            }
            catch (Exception ex)
            {
                // One widget that throws must not keep the others from hearing about the change.
                Log.Error(Scope, "a note listener threw", ex);
            }
        }
    }

    private enum PendingKind
    {
        Create,
        Write,
        Rename,
        Delete,
    }

    /// <summary>One queued disk operation. <see cref="Content"/> is mutable so a queued write can be coalesced.</summary>
    private sealed class Pending(PendingKind kind, string id, string? title, string? content)
    {
        public PendingKind Kind { get; } = kind;

        public string Id { get; } = id;

        public string? Title { get; } = title;

        public string? Content { get; set; } = content;
    }
}
