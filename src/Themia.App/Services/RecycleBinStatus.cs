using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1389] How full the Recycle Bin is, for the badge on a shortcut that points at it (original
/// S-75): one process-wide answer, so any number of widgets showing the bin cost one query.
/// <para>
/// <c>SHQueryRecycleBin</c> over every drive walks each drive's <c>$Recycle.Bin</c>, which is disk
/// I/O, so it runs on the thread pool. It is asked when the bin changes, never on a clock: the
/// command window registers for the shell's change notifications on the Recycle Bin folder
/// (<c>SHChangeNotifyRegister</c>, every drive's bin) and hands each one to <see cref="BinChanged"/>.
/// Only while a widget shows the bin (<see cref="Watch"/>) does a change ask again; a change with
/// nobody watching marks the answer stale, and the next watcher asks once. A query is single-flight:
/// a change during one asks once more after it, so a burst (a folder of files deleted) costs two
/// queries, not one per file. A failed query keeps the last answer and never invents "empty".
/// </para>
/// <para>
/// [S-4863] The Recycle Bin widget reads the same answer, and empties the bin through
/// <see cref="EmptyAsync"/>: Windows' own confirmation, never a silent empty.
/// </para>
/// </summary>
internal static partial class RecycleBinStatus
{
    private const string Scope = "recycle-bin";

    private static Task? _inFlight;
    private static bool _again;
    private static int _watchers;

    /// <summary>The bin changed since the last answer was asked for (or none was ever asked).</summary>
    private static bool _stale = true;

    /// <summary>The last answer, or null when none has come back yet. UI thread only.</summary>
    public static RecycleBinState? Current { get; private set; }

    /// <summary>Raised on the UI thread when <see cref="Current"/> changes.</summary>
    public static event Action? Changed;

    /// <summary>
    /// Show the bin: <see cref="Changed"/> follows every change from now on, and an answer that is
    /// missing or stale is asked for once. UI thread; dispose to stop (twice is once).
    /// </summary>
    public static IDisposable Watch()
    {
        _watchers++;

        if (_stale)
        {
            Ask();
        }

        return new Watcher();
    }

    /// <summary>The shell said the Recycle Bin changed (an item in, out, or the bin emptied). UI thread; never throws.</summary>
    public static void BinChanged()
    {
        _stale = true;

        if (_watchers > 0)
        {
            Ask();
        }
    }

    /// <summary>Query now, or once more after the query in flight. UI thread.</summary>
    private static void Ask()
    {
        // IsCompleted, not null: a query that finished before its task was stored must not read as
        // one still running.
        if (_inFlight is { IsCompleted: false })
        {
            _again = true;
            return;
        }

        _stale = false;
        _inFlight = RefreshAsync();
    }

    /// <summary>
    /// [S-4863] Empty the bin on every drive the way Explorer does: Windows asks first (no
    /// <c>SHERB_NOCONFIRMATION</c>), then shows its progress and plays its sound. The call blocks
    /// while the dialog is up, so it runs on its own STA thread and the desktop keeps drawing; the
    /// answer is asked again when it returns, whatever the user chose. UI thread; never throws.
    /// </summary>
    public static async Task EmptyAsync()
    {
        var done = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                done.SetResult(SHEmptyRecycleBin(0, null, 0));
            }
            catch (Exception ex)
            {
                // A thread's own top: nothing above it may see an exception.
                Log.Error(Scope, "emptying the Recycle Bin threw", ex);
                done.TrySetResult(-1);
            }
        })
        {
            IsBackground = true,
            Name = "Themia empty Recycle Bin",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var result = await done.Task.ConfigureAwait(true);
        if (result < 0)
        {
            // Cancelled in Windows' dialog is ERROR_CANCELLED; an already empty bin is E_UNEXPECTED.
            Log.Debug(Scope, $"SHEmptyRecycleBin returned 0x{result:X8}");
        }

        Ask();
    }

    private static async Task RefreshAsync()
    {
        RecycleBinState? answer;

        try
        {
            answer = await Task.Run(Query).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "asking the Recycle Bin how full it is threw", ex);
            answer = null;
        }
        finally
        {
            _inFlight = null;
        }

        if (_again)
        {
            // A change arrived while this query ran: its answer may predate it, so ask once more.
            _again = false;
            _stale = false;
            _inFlight = RefreshAsync();
        }

        if (answer is not { } state)
        {
            return; // the last answer stands; a failed query is not "empty".
        }

        if (Current == state)
        {
            return;
        }

        Current = state;

        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a Recycle Bin listener threw", ex);
        }
    }

    private static unsafe RecycleBinState? Query()
    {
        var info = new QueryInfo { Size = (uint)sizeof(QueryInfo) };

        var result = SHQueryRecycleBin(null, &info);

        if (result < 0)
        {
            Log.Debug(Scope, $"SHQueryRecycleBin failed (0x{result:X8})");
            return null;
        }

        return new RecycleBinState(Math.Max(0, info.Items), Math.Max(0, info.Bytes));
    }

    private sealed class Watcher : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _watchers--;
            }
        }
    }

    /// <summary>SHQUERYRBINFO, as laid out on x64 (cbSize, then two 8-byte-aligned 64-bit counts).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct QueryInfo
    {
        public uint Size;
        public long Bytes;
        public long Items;
    }

    [LibraryImport("shell32.dll", EntryPoint = "SHEmptyRecycleBinW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHEmptyRecycleBin(nint window, string? rootPath, uint flags);

    [LibraryImport("shell32.dll", EntryPoint = "SHQueryRecycleBinW", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial int SHQueryRecycleBin(string? rootPath, QueryInfo* info);
}
