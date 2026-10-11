using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1333] "This folder changed", event-driven: a <see cref="FileSystemWatcher"/> on the folder, and
/// the in-process notice every file operation this app performs raises (<see cref="ShellFileOps.Completed"/>),
/// so a paste in one folder widget redraws every other widget showing that folder at once.
/// <para>
/// Coalesced without a timer: the watcher's thread-pool events only set a pending flag, and the first
/// one of a burst posts one callback to the UI thread; the flag clears when that callback runs. A copy
/// of a thousand files is a handful of reloads, not a thousand. Disposed exactly once (the original's
/// [S-63]); a watcher that will not start (some network shares) is logged and <see cref="Watching"/>
/// says so, and the view falls back to its timer.
/// </para>
/// </summary>
internal sealed class FolderWatch : IDisposable
{
    private const string Scope = "folder-watch";

    private readonly string _path;
    private readonly Action _changed;
    private readonly DispatcherQueue _queue;
    private readonly FileSystemWatcher? _watcher;
    private int _pending;
    private int _disposed;

    private FolderWatch(string path, Action changed, DispatcherQueue queue, bool subfolders, bool namesOnly)
    {
        _path = path;
        _changed = changed;
        _queue = queue;

        ShellFileOps.Completed += OnOperation;

        try
        {
            _watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = subfolders,
                // [S-3410] A reader that only lists names (the gallery) is not woken by a file
                // being written, only by one arriving, going or being renamed.
                NotifyFilter = namesOnly
                    ? NotifyFilters.FileName | NotifyFilters.DirectoryName
                    : NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size
                        | NotifyFilters.LastWrite | NotifyFilters.Attributes,
                InternalBufferSize = 64 * 1024,
            };

            _watcher.Changed += OnEvent;
            _watcher.Created += OnEvent;
            _watcher.Deleted += OnEvent;
            _watcher.Renamed += OnEvent;
            _watcher.Error += OnError;
            _watcher.EnableRaisingEvents = true;
            Watching = true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Log.Warn(Scope, $"cannot watch '{path}'; falling back to the timer", ex);
            _watcher?.Dispose();
            _watcher = null;
        }
    }

    /// <summary>Whether change notifications are arriving; false means the caller should poll.</summary>
    public bool Watching { get; }

    /// <summary>Watch <paramref name="path"/>; <paramref name="changed"/> runs on the calling (UI) thread.</summary>
    /// <param name="subfolders">The folders below it too.</param>
    /// <param name="namesOnly">Only files and folders arriving, going or renamed; not one being written.</param>
    public static FolderWatch Start(string path, Action changed, bool subfolders = false, bool namesOnly = false)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var queue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("FolderWatch must start on a thread with a dispatcher queue");
        return new FolderWatch(path, changed, queue, subfolders, namesOnly);
    }

    private void OnEvent(object sender, FileSystemEventArgs e) => Post();

    private void OnError(object sender, ErrorEventArgs e)
    {
        // An overflowed buffer lost events: one reload sees whatever they were.
        Log.Debug(Scope, $"watcher error on '{_path}': {e.GetException().Message}");
        Post();
    }

    private void OnOperation(IReadOnlyList<string> folders)
    {
        if (folders.Any(folder => FileDrops.SamePath(folder, _path)))
        {
            Post();
        }
    }

    private void Post()
    {
        if (Volatile.Read(ref _disposed) != 0 || Interlocked.Exchange(ref _pending, 1) != 0)
        {
            return;
        }

        if (!_queue.TryEnqueue(DispatcherQueuePriority.Low, Deliver))
        {
            // The UI thread is shutting down; nothing will draw the change anyway.
            Interlocked.Exchange(ref _pending, 0);
        }
    }

    private void Deliver()
    {
        Interlocked.Exchange(ref _pending, 0);

        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        try
        {
            _changed();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a folder-change handler failed", ex);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        ShellFileOps.Completed -= OnOperation;

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnEvent;
            _watcher.Created -= OnEvent;
            _watcher.Deleted -= OnEvent;
            _watcher.Renamed -= OnEvent;
            _watcher.Error -= OnError;
            _watcher.Dispose();
        }
    }
}
