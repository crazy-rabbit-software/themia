using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Services.Toggles;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Services.Brightness;

/// <summary>
/// [S-4441] Every display's brightness, as Karpo's <c>Brightness.cs</c> has it, without WMI (Native AOT):
/// <list type="bullet">
/// <item><b>A built-in panel</b> through Windows' power policy, the setting its own slider moves:
/// <c>VIDEO_BRIGHTNESS</c> in the active plan (AC or DC, whichever is in force), applied with
/// <c>PowerSetActiveScheme</c>; heard through <c>GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS</c>, which Windows
/// sends at registration and at every change — the keys, the quick settings, adaptive brightness.
/// (Karpo takes WMI's <c>WmiMonitorBrightness</c> for the panel; WMI is not available under AOT.)</item>
/// <item><b>An external monitor</b> over DDC/CI, the Monitor Configuration API's luminance (VCP 0x10):
/// <c>GetMonitorBrightness</c>/<c>SetMonitorBrightness</c>, where the monitor says it has it. DDC/CI has no
/// notification: it is read when a widget comes up and when the pointer comes onto it (a monitor's own
/// buttons may have moved it).</item>
/// </list>
/// A display neither way reaches has no brightness, as Windows shows no slider for it.
/// <para>
/// <b>One worker thread</b> does every read and write, one at a time, because DDC/CI takes tens to
/// hundreds of milliseconds a call. A drag asks at every move; the worker writes only each display's
/// newest level (<see cref="NewestLevels"/>), so the last level always wins and nothing queues ([S-4443]).
/// Physical monitor handles are kept per scan and destroyed with it.
/// </para>
/// </summary>
internal static unsafe class BrightnessService
{
    private const string Scope = "brightness";

    private static readonly Lock Gate = new();
    private static readonly List<BrightnessLease> Leases = [];
    private static readonly NewestLevels Wanted = new();
    private static readonly SemaphoreSlim Wake = new(0);

    private static BrightnessSnapshot _snapshot = BrightnessSnapshot.Pending;
    private static List<Screen> _screens = [];
    private static Thread? _worker;
    private static bool _scan;
    private static bool _read;
    private static bool _stop;

    // The built-in panel's notification.
    private static nint _registration;
    private static ToggleNative.SubscribeParameters* _parameters;
    private static int _panelLevel = -1;

    /// <summary>One display found: how it is reached, and its DDC/CI handle and range.</summary>
    private sealed class Screen(string device, BrightnessPath path, SafePhysicalMonitor? monitor, uint minimum, uint maximum) : IDisposable
    {
        public string Device { get; } = device;
        public BrightnessPath Path { get; } = path;
        public SafePhysicalMonitor? Monitor { get; } = monitor;
        public uint Minimum { get; } = minimum;
        public uint Maximum { get; } = maximum;
        public int Level { get; set; } = -1;

        public void Dispose() => Monitor?.Dispose();
    }

    public static BrightnessSnapshot Snapshot
    {
        get
        {
            lock (Gate)
            {
                return _snapshot;
            }
        }
    }

    /// <summary>Follow the displays; <paramref name="changed"/> runs on the calling thread's dispatcher. Each widget coming up looks at the displays again.</summary>
    public static BrightnessLease Acquire(Action changed)
    {
        var queue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("acquire the brightness from a thread with a dispatcher");
        var lease = new BrightnessLease(queue, changed);

        lock (Gate)
        {
            Leases.Add(lease);
            _scan = true;
            _stop = false;
            if (_worker is null)
            {
                _worker = new Thread(Work) { IsBackground = true, Name = "Themia brightness" };
                _worker.Start();
            }
        }

        Signal();
        return lease;
    }

    internal static void Release(BrightnessLease lease)
    {
        lock (Gate)
        {
            if (!Leases.Remove(lease) || Leases.Count > 0)
            {
                return;
            }

            _stop = true;
        }

        Signal();
    }

    /// <summary>Read every level again (the pointer came by: a monitor's own buttons may have moved one).</summary>
    public static void Refresh()
    {
        lock (Gate)
        {
            _read = true;
        }

        Signal();
    }

    /// <summary>[S-4442] Ask <paramref name="level"/> of every display one slider sets; false where it sets none.</summary>
    public static bool Set(string? own, BrightnessScreens screens, int level)
    {
        var dimmed = BrightnessRules.Dimmed(Snapshot.Displays, own, screens);
        if (dimmed.Count == 0)
        {
            return false;
        }

        foreach (var display in dimmed)
        {
            Wanted.Ask(display.Device, level);
        }

        Signal();
        return true;
    }

    /// <summary>Wake the worker, unless a round is already waiting: that round reads what was asked after this.</summary>
    private static void Signal()
    {
        if (Wake.CurrentCount == 0)
        {
            _ = Wake.Release();
        }
    }

    /// <summary>The level being written to a display, until it has said what it took.</summary>
    public static int? Asked(string device) => Wanted.Asked(device);

    private static void Work()
    {
        while (true)
        {
            Wake.Wait();
            bool scan, read, stop;
            lock (Gate)
            {
                (scan, read, stop) = (_scan, _read, _stop);
                (_scan, _read) = (false, false);
            }

            try
            {
                if (stop)
                {
                    Shutdown();
                    lock (Gate)
                    {
                        // A widget that came up while this stopped starts it again.
                        if (Leases.Count == 0)
                        {
                            _worker = null;
                            return;
                        }

                        _stop = false;
                        _scan = true;
                    }

                    Signal();
                    continue;
                }

                Step(scan, read);
            }
            catch (Exception ex)
            {
                // The worker's loop: nothing may end it but the last widget going.
                Log.Error(Scope, "the brightness could not be read or set", ex);
            }
        }
    }

    private static void Step(bool scan, bool read)
    {
        var changed = false;
        if (scan)
        {
            Scan();
            changed = true;
        }
        else if (read)
        {
            foreach (var screen in _screens)
            {
                ReadLevel(screen);
            }

            changed = true;
        }

        foreach (var (device, _) in Wanted.Pending())
        {
            var screen = _screens.FirstOrDefault(each => string.Equals(each.Device, device, StringComparison.OrdinalIgnoreCase));
            if (screen is null)
            {
                Wanted.Forget(device);
                continue;
            }

            // The newest asked now, not when the round began: the drag went on while a slow monitor took the last one.
            if (Wanted.Asked(device) is not { } level)
            {
                continue;
            }

            var took = Write(screen, level);
            if (screen.Path == BrightnessPath.Ddc)
            {
                // A monitor rounds to its own steps, or refuses: what it says it took is what shows.
                ReadLevel(screen);
                if (took && screen.Level < 0)
                {
                    screen.Level = level;
                }
            }
            else if (took)
            {
                // The panel's own notification follows with what it took; until then, what was asked.
                screen.Level = level;
            }

            Wanted.Written(device, level);
            changed = true;
            Publish();
        }

        if (changed)
        {
            Publish();
        }
    }

    /// <summary>Find the displays again: the built-in panel by its output technology, the rest by DDC/CI.</summary>
    private static void Scan()
    {
        foreach (var old in _screens)
        {
            old.Dispose();
        }

        _screens = [];
        var described = DisplayNames.Describe();
        var found = new List<Screen>();

        foreach (var (monitor, device) in Monitors())
        {
            if (described.TryGetValue(device, out var descriptor) && descriptor.BuiltIn)
            {
                Follow();
                var panel = new Screen(device, BrightnessPath.BuiltIn, null, 0, 100);
                ReadLevel(panel);
                if (panel.Level >= 0)
                {
                    found.Add(panel);
                }

                continue;
            }

            if (Ddc(monitor, device) is { } screen)
            {
                found.Add(screen);
            }
        }

        _screens = found;
        Log.Info(Scope, $"{found.Count} display(s) with a brightness: {string.Join(", ", found.Select(s => $"{s.Device} {s.Path}"))}");
    }

    /// <summary>The first physical monitor of a display that answers DDC/CI's luminance; the others' handles are destroyed.</summary>
    private static Screen? Ddc(nint monitor, string device)
    {
        if (!BrightnessNative.GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out var count) || count == 0)
        {
            return null;
        }

        var monitors = new BrightnessNative.PhysicalMonitor[count];
        fixed (BrightnessNative.PhysicalMonitor* first = monitors)
        {
            if (!BrightnessNative.GetPhysicalMonitorsFromHMONITOR(monitor, count, first))
            {
                Log.Info(Scope, $"{device}: no physical monitors (win32 {Marshal.GetLastPInvokeError()})");
                return null;
            }
        }

        Screen? chosen = null;
        foreach (var physical in monitors)
        {
            var handle = new SafePhysicalMonitor(physical.Handle);
            if (chosen is not null)
            {
                handle.Dispose();
                continue;
            }

            // A monitor that answers the read has brightness over DDC/CI; the capabilities string is
            // slow and some monitors that have it never list it, so the read is the test.
            if (BrightnessNative.GetMonitorBrightness(handle, out var minimum, out var current, out var maximum)
                && BrightnessRules.Percent(minimum, current, maximum) is { } level)
            {
                chosen = new Screen(device, BrightnessPath.Ddc, handle, minimum, maximum) { Level = level };
            }
            else
            {
                handle.Dispose();
            }
        }

        return chosen;
    }

    private static void ReadLevel(Screen screen)
    {
        if (screen.Path == BrightnessPath.BuiltIn)
        {
            var heard = Volatile.Read(ref _panelLevel);
            screen.Level = heard >= 0 ? heard : PlanLevel() ?? screen.Level;
            return;
        }

        if (screen.Monitor is { IsInvalid: false } handle
            && BrightnessNative.GetMonitorBrightness(handle, out var minimum, out var current, out var maximum)
            && BrightnessRules.Percent(minimum, current, maximum) is { } level)
        {
            screen.Level = level;
        }

        // A missed DDC/CI reply keeps the level last said: a slider that jumped back for a dropped
        // reply would be worse than a level a moment old (Karpo's rule).
    }

    private static bool Write(Screen screen, int level)
    {
        if (screen.Path == BrightnessPath.Ddc)
        {
            if (screen.Monitor is not { IsInvalid: false } handle)
            {
                return false;
            }

            if (!BrightnessNative.SetMonitorBrightness(handle, BrightnessRules.Raw(level, screen.Minimum, screen.Maximum)))
            {
                Log.Info(Scope, $"{screen.Device} did not take brightness {level} (win32 {Marshal.GetLastPInvokeError()})");
                return false;
            }

            return true;
        }

        if (BrightnessNative.ActiveScheme() is not { } scheme)
        {
            Log.Warn(Scope, "the active power plan could not be read; the panel's brightness was not set");
            return false;
        }

        var subgroup = BrightnessNative.VideoSubgroup;
        var setting = BrightnessNative.VideoBrightness;
        var value = (uint)Math.Clamp(level, 0, 100);
        var written = BrightnessNative.OnMains()
            ? BrightnessNative.PowerWriteACValueIndex(0, &scheme, &subgroup, &setting, value)
            : BrightnessNative.PowerWriteDCValueIndex(0, &scheme, &subgroup, &setting, value);
        if (written != 0)
        {
            Log.Warn(Scope, $"the power plan refused brightness {level} ({written})");
            return false;
        }

        // Applying the plan again is what makes Windows move the backlight to the written value.
        var applied = BrightnessNative.PowerSetActiveScheme(0, &scheme);
        if (applied != 0)
        {
            Log.Warn(Scope, $"the power plan could not be applied ({applied})");
            return false;
        }

        return true;
    }

    /// <summary>The panel's brightness as the plan in force has it; null where it cannot be read.</summary>
    private static int? PlanLevel()
    {
        if (BrightnessNative.ActiveScheme() is not { } scheme)
        {
            return null;
        }

        var subgroup = BrightnessNative.VideoSubgroup;
        var setting = BrightnessNative.VideoBrightness;
        uint value = 0;
        var result = BrightnessNative.OnMains()
            ? BrightnessNative.PowerReadACValueIndex(0, &scheme, &subgroup, &setting, &value)
            : BrightnessNative.PowerReadDCValueIndex(0, &scheme, &subgroup, &setting, &value);
        return result == 0 ? (int)Math.Min(value, 100) : null;
    }

    /// <summary>Register for the panel's current brightness, once; the first answer comes at registration.</summary>
    private static void Follow()
    {
        if (Volatile.Read(ref _registration) != 0)
        {
            return;
        }

        _parameters = (ToggleNative.SubscribeParameters*)NativeMemory.Alloc((nuint)sizeof(ToggleNative.SubscribeParameters));
        _parameters->Callback = &OnSetting;
        _parameters->Context = null;

        var setting = BrightnessNative.CurrentMonitorBrightness;
        nint registration = 0;
        var result = ToggleNative.PowerSettingRegisterNotification(&setting, ToggleNative.DeviceNotifyCallback, (nint)_parameters, &registration);
        if (result != 0 || registration == 0)
        {
            Log.Info(Scope, $"the panel's brightness is not announced here ({result}); read when the pointer comes by");
            NativeMemory.Free(_parameters);
            _parameters = null;
            return;
        }

        Volatile.Write(ref _registration, registration);
    }

    /// <summary>Windows' callback: the panel's brightness moved. Nothing may escape into the power manager.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static uint OnSetting(void* context, uint type, void* setting)
    {
        try
        {
            if (type != ToggleNative.PbtPowerSettingChange || setting is null)
            {
                return 0;
            }

            var broadcast = (ToggleNative.PowerBroadcastSetting*)setting;
            if (broadcast->Setting != BrightnessNative.CurrentMonitorBrightness || broadcast->Length < sizeof(uint))
            {
                return 0;
            }

            Volatile.Write(ref _panelLevel, (int)Math.Min(*(uint*)&broadcast->Data, 100));
            Refresh();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a brightness notification could not be read", ex);
        }

        return 0;
    }

    private static void Shutdown()
    {
        var registration = Interlocked.Exchange(ref _registration, 0);
        if (registration != 0)
        {
            var result = ToggleNative.PowerSettingUnregisterNotification(registration);
            if (result != 0)
            {
                Log.Warn(Scope, $"the panel's brightness notification could not be unregistered ({result})");
            }
        }

        // Only now no callback can still be reading what the registration pointed at.
        if (_parameters is not null)
        {
            NativeMemory.Free(_parameters);
            _parameters = null;
        }

        Volatile.Write(ref _panelLevel, -1);
        foreach (var screen in _screens)
        {
            screen.Dispose();
        }

        _screens = [];
        lock (Gate)
        {
            _snapshot = BrightnessSnapshot.Pending;
        }
    }

    private static void Publish()
    {
        BrightnessLease[] leases;
        lock (Gate)
        {
            _snapshot = new BrightnessSnapshot(true, [.. _screens.Where(s => s.Level >= 0).Select(s => new BrightnessDisplay(s.Device, s.Path, s.Level))]);
            leases = [.. Leases];
        }

        foreach (var lease in leases)
        {
            lease.Post();
        }
    }

    // ---- displays ----------------------------------------------------------------------------

    private static List<(nint Monitor, string Device)>? _collecting;

    private static List<(nint Monitor, string Device)> Monitors()
    {
        _collecting = [];
        try
        {
            var callback = (nint)(delegate* unmanaged<nint, nint, Win32.Rect*, nint, int>)&OnMonitor;
            if (!Win32.EnumDisplayMonitors(0, 0, callback, 0))
            {
                Log.Warn(Scope, $"EnumDisplayMonitors failed (win32 {Marshal.GetLastPInvokeError()})");
            }

            return _collecting;
        }
        finally
        {
            _collecting = null;
        }
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static int OnMonitor(nint monitor, nint hdc, Win32.Rect* clip, nint data)
    {
        try
        {
            var info = new Win32.MonitorInfoEx { Size = sizeof(Win32.MonitorInfoEx) };
            if (Win32.GetMonitorInfo(monitor, ref info) && !string.IsNullOrEmpty(info.DeviceName))
            {
                _collecting?.Add((monitor, info.DeviceName));
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the display enumeration callback threw", ex);
        }

        return 1;
    }
}

/// <summary>One widget's hold on the brightness.</summary>
internal sealed class BrightnessLease : IDisposable
{
    private readonly DispatcherQueue _queue;
    private readonly Action _changed;
    private volatile bool _disposed;

    internal BrightnessLease(DispatcherQueue queue, Action changed)
    {
        _queue = queue;
        _changed = changed;
    }

    internal void Post()
    {
        if (_disposed)
        {
            return;
        }

        _ = _queue.TryEnqueue(() =>
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _changed();
            }
            catch (Exception ex)
            {
                Log.Error("brightness", "a brightness widget could not take a change", ex);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        BrightnessService.Release(this);
    }
}
