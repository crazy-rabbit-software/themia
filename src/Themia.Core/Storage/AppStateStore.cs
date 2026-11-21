using Themia.Core.Diagnostics;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>How a load ended. "Absent" and "corrupt" are deliberately different outcomes.</summary>
public enum LoadOutcome
{
    /// <summary>No file yet — a fresh profile. Seeded defaults, nothing was lost.</summary>
    Fresh,

    /// <summary>Read and validated.</summary>
    Loaded,

    /// <summary>
    /// A file existed but could not be trusted. Defaults are returned and the original is moved
    /// aside (never deleted) so the user can recover it.
    /// </summary>
    Recovered,
}

public readonly record struct LoadResult(AppState State, LoadOutcome Outcome, string? Detail)
{
    /// <summary>[S-15026] The cards the load shared out among several kinds, for their displays to lay (<see cref="ShareOuts"/>).</summary>
    public IReadOnlyList<ShareOut> SharedOut { get; init; } = [];
}

/// <summary>
/// Single-writer, atomic persistence for <see cref="AppState"/>.
/// <para>
/// Writes go to a sibling temp file and are then moved over the target, so a crash mid-write can
/// never leave a truncated document. Saves are serialised through one gate and coalesced: a write
/// still queued is replaced by a newer one.
/// </para>
/// <para>
/// [S-41] The store is handed bytes (<see cref="StateSnapshot"/>), taken on the thread that owns the live
/// state, and never the state itself: it writes on the thread pool while that thread goes on changing it.
/// When to take them is the owner's (<see cref="StateWriter"/>).
/// </para>
/// </summary>
public sealed class AppStateStore : IAsyncDisposable
{
    private const string Scope = "store";

    private readonly string _path;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly Lock _pendingGate = new();

    private StateSnapshot? _pending;
    private bool _disposed;

    /// <summary>False for a <see cref="Detached"/> store, which accepts saves and writes nothing.</summary>
    private readonly bool _persists;

    public AppStateStore(string path)
        : this(path, persists: true)
    {
    }

    private AppStateStore(string path, bool persists)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _persists = persists;
    }

    /// <summary>
    /// A store that names <paramref name="path"/> but never writes to it: saves are accepted and
    /// dropped.
    /// <para>
    /// For a host built only to be looked at — the debug settings render — where the pages must be
    /// able to call the ordinary save path without the render ever touching the user's real state
    /// file. A flag on the store rather than a second host, so the pages run exactly the code they
    /// run in the app.
    /// </para>
    /// </summary>
    public static AppStateStore Detached(string path) => new(path, persists: false);

    public string Path => _path;

    /// <summary>False for a <see cref="Detached"/> store: whatever is built over it must not touch the disk either ([S-2601]).</summary>
    public bool Persists => _persists;

    /// <summary>
    /// The folder name the app keeps its files under, in both %APPDATA% (settings, accounts) and
    /// %LOCALAPPDATA% (caches, the browser profile). One name for every place, so they cannot drift
    /// apart again.
    /// </summary>
    public const string FolderName = "Themia";

    /// <summary>The default on-disk location: %APPDATA%\Themia\state.json.</summary>
    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(root, FolderName, "state.json");
    }

    /// <summary>
    /// Read and validate. Never throws for bad data: a corrupt document is set aside and defaults
    /// are returned, loudly. The caller can tell that apart from a fresh profile via the outcome.
    /// </summary>
    public LoadResult Load()
    {
        string json;
        try
        {
            if (!File.Exists(_path))
            {
                Log.Info(Scope, $"no state at {_path}; seeding defaults");
                return new LoadResult(AppState.CreateDefault(), LoadOutcome.Fresh, null);
            }

            json = File.ReadAllText(_path);
        }
        catch (IOException ex)
        {
            return Recover("state file could not be read", ex, setAside: false);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Recover("state file could not be read", ex, setAside: false);
        }

        var parsed = StateDocument.Parse(json);
        if (parsed.State is not { } state)
        {
            return Recover(parsed.Problem ?? "state file could not be read", parsed.Error, setAside: true);
        }

        if (parsed.Repairs.Count > 0)
        {
            Log.Warn(Scope, $"state loaded with {parsed.Repairs.Count} repair(s): {string.Join("; ", parsed.Repairs)}");
        }

        return new LoadResult(state, LoadOutcome.Loaded, parsed.Repairs.Count > 0 ? string.Join("; ", parsed.Repairs) : null) { SharedOut = parsed.SharedOut };
    }


    private LoadResult Recover(string reason, Exception? error, bool setAside)
    {
        Log.Error(Scope, $"{reason}; falling back to defaults", error);

        if (setAside)
        {
            // Never delete the user's data on a recoverable error — move it where they can find it.
            var aside = $"{_path}.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}";
            try
            {
                File.Move(_path, aside, overwrite: true);
                Log.Warn(Scope, $"previous state preserved at {aside}");
            }
            catch (IOException ex)
            {
                Log.Error(Scope, "could not set aside the corrupt state file", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error(Scope, "could not set aside the corrupt state file", ex);
            }
        }

        return new LoadResult(AppState.CreateDefault(), LoadOutcome.Recovered, reason);
    }

    /// <summary>
    /// Queue a write of <paramref name="snapshot"/>, carried out on the thread pool; one still queued is replaced, so
    /// the latest wins. A detached store drops it.
    /// </summary>
    public void SaveSoon(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_persists)
        {
            return;
        }

        lock (_pendingGate)
        {
            _pending = snapshot;
        }

        _ = Task.Run(WriteQueuedAsync);
    }

    private async Task WriteQueuedAsync()
    {
        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // This runs detached from any caller, so an escaping exception would be lost entirely.
            // WriteAtomicAsync already handles the expected I/O failures; anything reaching here is
            // unexpected and must still be visible rather than vanish into an unobserved task.
            Log.Error(Scope, "background save failed", ex);
        }
    }

    /// <summary>
    /// Tests: a write that took its snapshot waits for this before it writes, so a test can change the state
    /// and queue a newer snapshot while a write is out.
    /// </summary>
    internal Task WritesHeldUntil { get; set; } = Task.CompletedTask;

    /// <summary>
    /// Write any queued snapshot now and wait for it. Safe to call when nothing is pending.
    /// <para>
    /// The write gate is taken <i>before</i> the pending snapshot is read, so a flush that finds
    /// nothing queued still waits for an in-flight write to land. Reading the snapshot first would
    /// let this return while a queued save was mid-write, i.e. "flushed" without durability —
    /// exactly the window a shutdown flush exists to close.
    /// </para>
    /// </summary>
    public async Task FlushAsync()
    {
        await _writeGate.WaitAsync().ConfigureAwait(false);
        try
        {
            StateSnapshot? snapshot;
            lock (_pendingGate)
            {
                snapshot = _pending;
                _pending = null;
            }

            if (snapshot is null || !_persists)
            {
                return;
            }

            await WritesHeldUntil.ConfigureAwait(false);
            await WriteAtomicAsync(snapshot).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task WriteAtomicAsync(StateSnapshot snapshot)
    {
        var temp = $"{_path}.tmp";
        try
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllBytesAsync(temp, snapshot.Json).ConfigureAwait(false);

            // Atomic swap: readers see either the old document or the new one, never a partial write.
            AtomicFile.Replace(temp, _path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error(Scope, $"could not save state to {_path}", ex);

            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (IOException cleanupEx)
            {
                Log.Warn(Scope, "could not remove the temp state file", cleanupEx);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await FlushAsync().ConfigureAwait(false);
        _writeGate.Dispose();
    }
}
