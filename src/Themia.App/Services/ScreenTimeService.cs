using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.Core.Diagnostics;
using Themia.Core.Storage;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>Where a screen-time widget reads from. The app has one tracker.</summary>
internal interface IScreenTimeSource
{
    /// <summary>The source's own idea of today, so a widget and the tracker never disagree about midnight.</summary>
    LocalDay Today { get; }

    /// <summary>[S-454] One day, live-inclusive. Null means today.</summary>
    ScreenTimeDay Day(string? date);

    /// <summary>[S-454] The rolling window ending today, oldest first, gap days as zeros.</summary>
    IReadOnlyList<ScreenTimeDay> Range(int days);

    /// <summary>Raised on the UI thread when there is something new to draw.</summary>
    event Action? Changed;
}

/// <summary>
/// [S-458] Records which app has the foreground, for how long, per day — while Themia is running.
/// <para>
/// Every input is an event except one. The foreground hook says when the app changes, the session
/// notification says when the machine locks, and a 30-second sampling tick turns the open interval
/// into seconds and asks how long the user has been idle. The tick is a sampling cadence, not an
/// ordering crutch: accrual is bounded by it ([S-458]), and it is the only way to learn about idle
/// without a low-level input hook on every keystroke. It is the service's own timer rather than the
/// shared widget ticker, because the widget ticker is allowed to stop while the widgets cannot be
/// seen — and usage does not stop then.
/// </para>
/// <para>
/// Everything runs on the UI thread, where the hook and the session window deliver their messages,
/// so the tracker needs no lock. The two kinds of disk work stay off it: reading an executable's
/// name on first sight, and writing the history file, which is serialised here and written on the
/// pool, one write after another, so there is only ever one writer.
/// </para>
/// <para>
/// Started by the app host and nowhere else: a debug render never creates one, so rendering a
/// widget there neither installs a hook nor writes a file beside the user's state.
/// </para>
/// </summary>
internal sealed unsafe partial class ScreenTimeService : IScreenTimeSource, IDisposable
{
    private const string Scope = "screentime";

    /// <summary>[S-459] At most one write a minute while the history is changing.</summary>
    private static readonly TimeSpan FlushEvery = TimeSpan.FromSeconds(60);

    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;
    private const uint WinEventSkipOwnProcess = 0x0002;
    private const uint WmWtsSessionChange = 0x02B1;
    private const int WtsSessionLock = 0x7;
    private const int WtsSessionUnlock = 0x8;
    private const uint NotifyForThisSession = 0;

    /// <summary>The one live service, for the static hook and window callbacks to find.</summary>
    private static ScreenTimeService? _live;

    private readonly DispatcherQueue _dispatcher;
    private readonly string _path;
    private readonly ScreenTimeTracker _tracker = new();
    private readonly DispatcherQueueTimer _timer;
    private readonly HashSet<string> _naming = new(StringComparer.Ordinal);
    private readonly uint _ownPid = (uint)Environment.ProcessId;

    private nint _hook;
    private nint _window;
    private long _lastFlush = Stopwatch.GetTimestamp();
    private Task _writes = Task.CompletedTask;
    private bool _disposed;

    private ScreenTimeService(DispatcherQueue dispatcher, string path)
    {
        _dispatcher = dispatcher;
        _path = path;

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(ScreenTimeTracker.TickSeconds);
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => OnTick();
    }

    public event Action? Changed;

    public LocalDay Today => LocalDay.From(DateTimeOffset.Now);

    /// <summary>Load the history, install the hook and the session window, and start sampling. UI thread.</summary>
    public static ScreenTimeService Start(DispatcherQueue dispatcher, string path)
    {
        var service = new ScreenTimeService(dispatcher, path);
        _live = service;

        service.InstallHook();
        service.CreateSessionWindow();

        // [S-458] The app already in front is seeded now, so tracking starts before the first switch.
        service.OnForegroundWindow(GetForegroundWindow());
        service._timer.Start();

        // The file is read on the pool and merged into the live ledger, which may already have
        // accrued a few seconds: the merge adds rather than replaces.
        _ = Task.Run(() => ScreenTimeStore.Load(path)).ContinueWith(
            loaded => dispatcher.TryEnqueue(() => service.OnLoaded(loaded)),
            TaskScheduler.Default);

        return service;
    }

    public ScreenTimeDay Day(string? date)
    {
        var now = LocalStamp.From(DateTimeOffset.Now);
        return _tracker.Day(date ?? now.Date, now);
    }

    public IReadOnlyList<ScreenTimeDay> Range(int days) => _tracker.Range(LocalStamp.From(DateTimeOffset.Now), days);

    private void OnLoaded(Task<(ScreenTimeTracker Tracker, ScreenTimeLoadOutcome Outcome)> loaded)
    {
        if (_disposed)
        {
            return;
        }

        if (loaded.IsFaulted)
        {
            Log.Error(Scope, "could not load the screen-time history", loaded.Exception);
            return;
        }

        var (history, outcome) = loaded.Result;
        var dirty = _tracker.Dirty;
        _tracker.Merge(history);
        _tracker.Dirty = dirty || outcome == ScreenTimeLoadOutcome.Recovered;

        Raise();

        // [S-454] Icons and names for history buckets whose app is running right now, so the past
        // has icons without waiting for each app to come to the front again.
        var wanted = _tracker.AppsMissingPaths().Concat(_tracker.AppsMissingNames()).ToHashSet(StringComparer.Ordinal);
        if (wanted.Count > 0)
        {
            _ = Task.Run(() => Backfill(wanted)).ContinueWith(
                found => _dispatcher.TryEnqueue(() => OnBackfilled(found)),
                TaskScheduler.Default);
        }
    }

    private static List<(string App, string Path, string? Name)> Backfill(HashSet<string> wanted)
    {
        var found = new List<(string, string, string?)>();
        var processes = Process.GetProcesses();

        try
        {
            foreach (var process in processes)
            {
                var app = process.ProcessName.ToLowerInvariant();
                if (!wanted.Remove(app))
                {
                    continue;
                }

                if (ProcessNames.ExePath((uint)process.Id) is { } path)
                {
                    found.Add((app, path, ProcessNames.DisplayName(path)));
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return found;
    }

    private void OnBackfilled(Task<List<(string App, string Path, string? Name)>> found)
    {
        if (_disposed)
        {
            return;
        }

        if (found.IsFaulted)
        {
            Log.Warn(Scope, "could not fill in the icons of past apps", found.Exception);
            return;
        }

        foreach (var (app, path, name) in found.Result)
        {
            _tracker.NoteExePath(app, path);

            // A name that landed meanwhile — from a foreground switch — is not overwritten.
            if (name is not null && !_tracker.Names.ContainsKey(app))
            {
                _tracker.NoteName(app, name);
            }
        }

        Raise();
    }

    // ---- inputs ------------------------------------------------------------------------------

    private void OnForegroundWindow(nint hwnd)
    {
        if (_disposed || hwnd == 0)
        {
            return;
        }

        uint pid;
        GetWindowThreadProcessId(hwnd, &pid);

        // [S-458] Themia's own windows are never a bucket.
        if (pid == 0 || pid == _ownPid)
        {
            return;
        }

        if (ProcessNames.ExePath(pid) is not { } path || AppNames.ExeStem(path) is not { } stem)
        {
            return;
        }

        var app = stem.ToLowerInvariant();

        // [S-454] Every switch, so an app that moved to a new versioned folder keeps its icon.
        _tracker.NoteExePath(app, path);
        _tracker.OnForeground(LocalStamp.From(DateTimeOffset.Now), app);

        if (!_tracker.Names.ContainsKey(app) && _naming.Add(app))
        {
            // [S-458] The version resource is read from disk: off the hook's thread. The name may
            // appear one refresh late.
            _ = Task.Run(() => ProcessNames.DisplayName(path)).ContinueWith(
                named => _dispatcher.TryEnqueue(() =>
                {
                    _naming.Remove(app);

                    if (!_disposed && named.IsCompletedSuccessfully && named.Result is { } name)
                    {
                        _tracker.NoteName(app, name);
                    }
                }),
                TaskScheduler.Default);
        }
    }

    private void OnTick()
    {
        if (_disposed)
        {
            return;
        }

        var stamp = LocalStamp.From(DateTimeOffset.Now);
        _tracker.OnTick(stamp, IdleSeconds());

        // [S-458] The hook skips Themia's own windows, so its foreground is noticed here instead.
        uint pid = 0;
        var foreground = GetForegroundWindow();
        if (foreground != 0)
        {
            GetWindowThreadProcessId(foreground, &pid);
        }

        if (pid == _ownPid)
        {
            _tracker.OnOwnForeground(stamp);
        }

        if (_tracker.Dirty && Stopwatch.GetElapsedTime(_lastFlush) >= FlushEvery)
        {
            Flush();
        }

        Raise();
    }

    private void OnSession(int change)
    {
        if (_disposed)
        {
            return;
        }

        var stamp = LocalStamp.From(DateTimeOffset.Now);

        if (change == WtsSessionLock)
        {
            // [S-459] Locking is a natural moment to be durable: the machine may not wake.
            _tracker.OnLock(stamp);

            if (_tracker.Dirty)
            {
                Flush();
            }
        }
        else if (change == WtsSessionUnlock)
        {
            _tracker.OnUnlock(stamp);
        }

        Raise();
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a screen-time widget failed to redraw", ex);
        }
    }

    /// <summary>[S-458] Seconds since the last keyboard or mouse input; 0 when it cannot be asked, so bad data never pauses.</summary>
    private static int IdleSeconds()
    {
        var info = new LastInputInfo { Size = (uint)sizeof(LastInputInfo) };
        if (!GetLastInputInfo(ref info))
        {
            return 0;
        }

        // Both are 32-bit tick counts that wrap after 49.7 days; unsigned subtraction survives the wrap.
        return (int)(unchecked((uint)Environment.TickCount - info.Time) / 1000);
    }

    // ---- persistence -------------------------------------------------------------------------

    /// <summary>
    /// [S-459] Serialise here, write on the pool behind any write still running: one writer, in
    /// order, never blocking the UI on the disk.
    /// </summary>
    private void Flush()
    {
        var json = ScreenTimeStore.Serialize(_tracker);
        _tracker.Dirty = false;
        _lastFlush = Stopwatch.GetTimestamp();

        _writes = _writes.ContinueWith(
            _ =>
            {
                if (!ScreenTimeStore.WriteAtomic(_path, json))
                {
                    // Logged by the store; mark dirty again so the next flush retries.
                    _dispatcher.TryEnqueue(() => _tracker.Dirty = true);
                }
            },
            TaskScheduler.Default);
    }

    /// <summary>[S-459] Accrue to now and write synchronously: the process is about to end.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _live = null;
        _timer.Stop();

        if (_hook != 0)
        {
            UnhookWinEvent(_hook);
            _hook = 0;
        }

        if (_window != 0)
        {
            WTSUnRegisterSessionNotification(_window);
            DestroyWindow(_window);
            _window = 0;
        }

        _tracker.Settle(LocalStamp.From(DateTimeOffset.Now));

        // Queued behind any write still running rather than beside it: the last write to land must
        // be the newest, and there is only ever one writer.
        if (_tracker.Dirty)
        {
            var json = ScreenTimeStore.Serialize(_tracker);
            _writes = _writes.ContinueWith(_ => ScreenTimeStore.WriteAtomic(_path, json), TaskScheduler.Default);
        }

        try
        {
            _writes.Wait();
        }
        catch (AggregateException ex)
        {
            Log.Error(Scope, "the final history write failed", ex);
        }
    }

    // ---- the hook and the session window ------------------------------------------------------

    private void InstallHook()
    {
        // Out of context, so callbacks arrive on this thread's message loop rather than inside the
        // other process; skipping our own process, so Themia's windows never land in a bucket.
        _hook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            0,
            (nint)(delegate* unmanaged<nint, uint, nint, int, int, uint, uint, void>)&OnWinEvent,
            0,
            0,
            WinEventOutOfContext | WinEventSkipOwnProcess);

        if (_hook == 0)
        {
            Log.Warn(Scope, $"could not watch the foreground window (win32 {Marshal.GetLastPInvokeError()}); screen time will not record");
        }
    }

    private void CreateSessionWindow()
    {
        const string className = "Themia.ScreenTime.Session";

        var name = Marshal.StringToHGlobalUni(className);
        try
        {
            var windowClass = new WindowClass
            {
                Size = (uint)sizeof(WindowClass),
                WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&OnWindowMessage,
                Instance = GetModuleHandle(null),
                ClassName = name,
            };

            // A second registration (a restart of the service in one process) fails with "class
            // exists", which is fine: the class is the same.
            RegisterClassEx(ref windowClass);

            _window = CreateWindowEx(0, className, null, 0, 0, 0, 0, 0, HwndMessage, 0, windowClass.Instance, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(name);
        }

        if (_window == 0 || !WTSRegisterSessionNotification(_window, NotifyForThisSession))
        {
            // Without it a locked screen is still caught by the idle pause, five minutes later.
            Log.Warn(Scope, "could not listen for the session locking; idle detection will cover it");
        }
    }

    [UnmanagedCallersOnly]
    private static void OnWinEvent(nint hook, uint eventType, nint hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            _live?.OnForegroundWindow(hwnd);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not record a foreground change", ex);
        }
    }

    [UnmanagedCallersOnly]
    private static nint OnWindowMessage(nint hwnd, uint message, nint wParam, nint lParam)
    {
        try
        {
            if (message == WmWtsSessionChange)
            {
                _live?.OnSession((int)wParam);
                return 0;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not record a session change", ex);
            return 0;
        }

        return DefWindowProc(hwnd, message, wParam, lParam);
    }

    private static readonly nint HwndMessage = -3;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSmall;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial nint SetWinEventHook(uint eventMin, uint eventMax, nint module, nint callback, uint pid, uint thread, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnhookWinEvent(nint hook);

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, uint* pid);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    private static partial ushort RegisterClassEx(ref WindowClass windowClass);

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateWindowEx(uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyWindow(nint hwnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint GetModuleHandle(string? name);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSRegisterSessionNotification(nint hwnd, uint flags);

    [LibraryImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool WTSUnRegisterSessionNotification(nint hwnd);
}
