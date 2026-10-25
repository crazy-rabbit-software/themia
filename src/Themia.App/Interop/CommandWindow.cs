using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Shell;

namespace Themia.App.Interop;

/// <summary>
/// The app's one hidden window, and the only way to reach it from outside its own surfaces.
/// <para>
/// Themia deliberately puts nothing in the notification area: it is wallpaper with widgets on it,
/// not a program the user runs, and a permanent tray entry advertises it as the latter. That leaves
/// two ways in, and this window is both of them — a global hotkey, and a second launch of the
/// executable, which activates the copy already running instead of starting another one.
/// </para>
/// <para>
/// Not a XAML window: it is never shown and never painted, and paying for a XAML island and its
/// render thread to own a hotkey is exactly the cost this port exists to avoid.
/// </para>
/// </summary>
internal sealed unsafe class CommandWindow : IDisposable
{
    private const string Scope = "command";

    /// <summary>
    /// Also the address the next launch looks the running copy up by, so it must stay stable across
    /// versions — an old copy and a new one that disagree about it are two visible desktops.
    /// </summary>
    private const string ClassName = "ThemiaCommandWindow";

    private const uint WsPopup = 0x80000000;

    private const uint WmDisplayChange = 0x007E;
    private const uint WmSettingChange = 0x001A;
    private const uint WmDpiChanged = 0x02E0;
    private const nint SpiSetWorkArea = 0x002F;

    private const uint WmTimeChange = 0x001E;
    private const uint WmFontChange = 0x001D;
    private const uint WmPowerBroadcast = 0x0218;
    private const uint WmDeviceChange = 0x0219;
    private const nint PbtResumeSuspend = 0x0007;
    private const nint PbtResumeAutomatic = 0x0012;
    private const nint DbtDeviceArrival = 0x8000;
    private const nint DbtDeviceRemoveComplete = 0x8004;

    private static CommandWindow? _instance;
    private static ushort _atom;

    /// <summary>
    /// The "you are already running" message. Registered by name, so both processes resolve it to
    /// the same number without sharing anything else.
    /// </summary>
    private static uint _activateMessage;

    /// <summary>
    /// Explorer's "the taskbar was (re)created" broadcast. After an Explorer restart the taskbar comes
    /// back with its own size and position, and nothing else announces the new work area reliably.
    /// </summary>
    private static uint _taskbarCreatedMessage;

    private nint _hwnd;

    /// <summary>[S-65] [S-1998] The hotkeys this window holds, so <see cref="Dispose"/> lets go of exactly those.</summary>
    private readonly List<ShellHotkey> _hotkeys = [];

    /// <summary>[S-1011] Registered for session notifications, so <see cref="Dispose"/> unregisters.</summary>
    private bool _sessionRegistered;

    /// <summary>[S-1389] The shell's Recycle Bin notifications to this window; null when the shell refused them.</summary>
    private RecycleBinNotifications? _recycleBin;
    private bool _disposed;

    /// <summary>[S-65] Raised on Ctrl+Alt+W: edit mode on or off.</summary>
    public event Action? EditModeToggleRequested;

    /// <summary>[S-66] Raised when the user starts Themia again: edit mode, with the settings menu open on the main display.</summary>
    public event Action? SettingsRequested;

    /// <summary>[S-1998] Raised on Ctrl+Alt+Shift+W: edit mode, with the add menu open.</summary>
    public event Action? AddWidgetRequested;

    /// <summary>
    /// [S-848] A display was added, removed, re-arranged, re-sized or re-scaled, or the work area
    /// changed (the taskbar moved, was resized, or auto-hide was switched). Raised on the UI thread,
    /// once per message: the receiver coalesces.
    /// <para>
    /// This window hears them because it is a real top-level window: <c>WM_DISPLAYCHANGE</c> and
    /// <c>WM_SETTINGCHANGE</c> are broadcast to every top-level window, shown or not, and never to a
    /// message-only one.
    /// </para>
    /// </summary>
    public event Action? DisplaysChanged;

    /// <summary>
    /// [S-4655] A volume arrived or left, or a network drive was mapped or dropped: Windows broadcasts
    /// <c>WM_DEVICECHANGE</c> (DBT_DEVICEARRIVAL / DBT_DEVICEREMOVECOMPLETE) to every top-level window. UI thread.
    /// </summary>
    public event Action? VolumesChanged;

    /// <summary>[S-1389] [S-4863] The shell said the Recycle Bin changed (an item in or out, emptied). UI thread.</summary>
    public event Action? RecycleBinChanged;

    /// <summary>[S-4623] Windows' clock or time zone changed (<c>WM_TIMECHANGE</c>). UI thread.</summary>
    public event Action? TimeChanged;

    /// <summary>[S-6443] A font was installed or removed (<c>WM_FONTCHANGE</c>, broadcast to every top-level window). UI thread.</summary>
    public event Action? FontsChanged;

    /// <summary>[S-4623] The PC woke from sleep or hibernation (<c>WM_POWERBROADCAST</c> resume). UI thread.</summary>
    public event Action? Resumed;

    /// <summary>[S-1011] The session was unlocked (only that change: not lock, logon or a remote connect). On the UI thread.</summary>
    public event Action? SessionUnlocked;

    /// <summary>
    /// Hand the request to a copy that is already running and report that we should stand down.
    /// <para>
    /// Called before anything is built, because the alternative is two processes drawing two widget
    /// layers over one wallpaper, with nothing on screen to say which is which and no tray icon
    /// left to tell them apart by.
    /// </para>
    /// <para>
    /// [S-66] The message says how this copy was started (<see cref="SecondStart.Encode"/>), so the
    /// running copy opens its settings menu for a start by hand and does nothing for one at sign-in.
    /// A copy the user just started may take the foreground, and hands that to the running copy,
    /// whose menu then gets the keys typed into it.
    /// </para>
    /// </summary>
    public static bool TrySignalRunningInstance(LaunchSource source)
    {
        var existing = Win32.FindWindow(ClassName, null);
        if (existing == 0)
        {
            return false;
        }

        var message = ActivateMessage();
        if (message == 0)
        {
            // We know something is already running but cannot reach it. Starting a second desktop
            // layer on top of it is the worse of the two outcomes.
            Log.Error(Scope, "another copy is running but the activation message could not be registered; not starting a second");
            return true;
        }

        if (source == LaunchSource.Manual)
        {
            Win32.GetWindowThreadProcessId(existing, out var running);
            if (running == 0 || !Win32.AllowSetForegroundWindow(running))
            {
                // Not fatal: the menu still opens, the keys typed next just stay where they were.
                Log.Info(Scope, $"could not hand the foreground to the running copy (win32 {Marshal.GetLastWin32Error()})");
            }
        }

        if (!Win32.PostMessage(existing, message, SecondStart.Encode(source), 0))
        {
            Log.Error(Scope, $"could not signal the running copy (win32 {Marshal.GetLastWin32Error()}); not starting a second");
        }

        return true;
    }

    /// <summary>
    /// Whether a copy of Themia is running, without telling it anything: for the Debug state tools
    /// ([S-1814]) that must not write a state file the running copy owns.
    /// </summary>
    public static bool IsAnotherInstanceRunning() => Win32.FindWindow(ClassName, null) != 0;

    private static uint ActivateMessage()
    {
        if (_activateMessage == 0)
        {
            _activateMessage = Win32.RegisterWindowMessage("ThemiaActivate");
        }

        return _activateMessage;
    }

    /// <summary>Create the window and claim the hotkey. Returns false (logged) rather than throwing.</summary>
    public bool Create()
    {
        if (_instance is not null && !ReferenceEquals(_instance, this))
        {
            Log.Error(Scope, "a command window already exists; refusing to create a second");
            return false;
        }

        _instance = this;

        var hInstance = Win32.GetModuleHandle(null);

        if (_atom == 0)
        {
            fixed (char* className = ClassName)
            {
                var wc = new Win32.WndClassEx
                {
                    Size = (uint)sizeof(Win32.WndClassEx),
                    WndProc = (nint)(delegate* unmanaged<nint, uint, nint, nint, nint>)&StaticWndProc,
                    Instance = hInstance,
                    ClassName = (nint)className,
                };

                _atom = Win32.RegisterClassEx(ref wc);
            }

            if (_atom == 0)
            {
                Log.Error(Scope, $"RegisterClassEx failed (win32 {Marshal.GetLastWin32Error()})");
                return false;
            }
        }

        // A real top-level window rather than HWND_MESSAGE: the next launch finds this one with
        // FindWindow, which does not see message-only windows. It is never shown.
        _hwnd = Win32.CreateWindowEx(0, ClassName, "Themia", WsPopup, 0, 0, 0, 0, 0, 0, hInstance, 0);
        if (_hwnd == 0)
        {
            Log.Error(Scope, $"CreateWindowEx failed (win32 {Marshal.GetLastWin32Error()})");
            return false;
        }

        if (ActivateMessage() == 0)
        {
            Log.Warn(Scope, "could not register the activation message; launching Themia again will not reach this copy");
        }

        _taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
        if (_taskbarCreatedMessage == 0)
        {
            Log.Warn(Scope, "could not register TaskbarCreated; an Explorer restart may leave widgets placed for the old taskbar");
        }

        RegisterHotkeys();

        // [S-1011] The unlock replays the intro. Without it the desktop simply comes back still.
        _sessionRegistered = SessionNotifications.Register(_hwnd, SessionNotifications.ForThisSession);
        if (!_sessionRegistered)
        {
            Log.Warn(Scope, $"could not listen for the session unlock (win32 {Marshal.GetLastWin32Error()}); the unlock animation will not play");
        }

        // [S-1389] Logged by Register when the shell refuses: the bin is then asked only when a widget shows it.
        _recycleBin = RecycleBinNotifications.Register(_hwnd);

        return true;
    }

    /// <summary>
    /// [S-65] [S-1998] Claim every hotkey in <see cref="ShellHotkeys.All"/>, each NOREPEAT because a
    /// held key would otherwise fire it dozens of times a second. One that another program owns is
    /// logged and skipped: the others, and every other way into edit mode, still work.
    /// </summary>
    private void RegisterHotkeys()
    {
        foreach (var binding in ShellHotkeys.All)
        {
            if (Win32.RegisterHotKey(_hwnd, (int)binding.Hotkey, (uint)binding.Modifiers, binding.VirtualKey))
            {
                _hotkeys.Add(binding.Hotkey);
                Log.Info(Scope, $"{binding.Keys}: {binding.Hotkey}");
                continue;
            }

            // Another program owns the combination. Double-clicking a widget, launching Themia
            // again and the edit bar's Add widget all still work, so this is a degraded app rather
            // than an unusable one.
            Log.Error(
                Scope,
                $"{binding.Keys} is taken by another program (win32 {Marshal.GetLastWin32Error()}); " +
                "edit mode can still be reached by double-clicking a widget or launching Themia again");
        }
    }

    [UnmanagedCallersOnly]
    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        // A WndProc is called by Windows itself. Letting a managed exception unwind out of here is
        // undefined behaviour, so every path is wrapped and the message falls through to DefWindowProc.
        try
        {
            var self = _instance;
            if (self is not null && self._hwnd == hwnd)
            {
                if (msg == Win32.WmHotkey && ShellHotkeys.FromId(wParam) is { } hotkey)
                {
                    (hotkey == ShellHotkey.AddWidget ? self.AddWidgetRequested : self.EditModeToggleRequested)?.Invoke();
                    return 0;
                }

                if (_activateMessage != 0 && msg == _activateMessage)
                {
                    switch (SecondStart.Decide(wParam))
                    {
                        case SecondStartAction.OpenSettings:
                            self.SettingsRequested?.Invoke();
                            break;
                        case SecondStartAction.Ignore:
                            Log.Info(Scope, "started again at sign-in while running; nothing to do");
                            break;
                        default:
                            Log.Warn(Scope, $"an activation message carried {wParam}, which no copy of Themia sends; ignored");
                            break;
                    }

                    return 0;
                }

                if (msg == RecycleBinNotifications.Message)
                {
                    self.RecycleBinChanged?.Invoke();
                    return 0;
                }

                if (SessionNotifications.IsUnlock(msg, wParam))
                {
                    self.SessionUnlocked?.Invoke();
                    return 0;
                }

                // Not handled either: DefWindowProc answers each as usual (TRUE for a device query).
                if (msg == WmDeviceChange && wParam is DbtDeviceArrival or DbtDeviceRemoveComplete)
                {
                    self.VolumesChanged?.Invoke();
                }
                else if (msg == WmTimeChange)
                {
                    self.TimeChanged?.Invoke();
                }
                else if (msg == WmFontChange)
                {
                    self.FontsChanged?.Invoke();
                }
                else if (msg == WmPowerBroadcast && wParam is PbtResumeAutomatic or PbtResumeSuspend)
                {
                    self.Resumed?.Invoke();
                }

                if (msg == WmDisplayChange
                    || msg == WmDpiChanged
                    || (msg == WmSettingChange && wParam == SpiSetWorkArea)
                    || (_taskbarCreatedMessage != 0 && msg == _taskbarCreatedMessage))
                {
                    // Not handled: DefWindowProc still sees it, as every other window's does.
                    self.DisplaysChanged?.Invoke();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the command window procedure threw handling message 0x{msg:X}", ex);
        }

        return Win32.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // A hotkey outliving its window is a combination nothing can claim until the session
        // ends, including the copy the user starts next.
        foreach (var hotkey in _hotkeys)
        {
            if (!Win32.UnregisterHotKey(_hwnd, (int)hotkey))
            {
                Log.Warn(Scope, $"UnregisterHotKey({hotkey}) failed (win32 {Marshal.GetLastWin32Error()})");
            }
        }

        _hotkeys.Clear();

        if (_sessionRegistered)
        {
            if (!SessionNotifications.Unregister(_hwnd))
            {
                Log.Warn(Scope, "WTSUnRegisterSessionNotification failed");
            }

            _sessionRegistered = false;
        }

        _recycleBin?.Dispose();
        _recycleBin = null;

        if (_hwnd != 0)
        {
            Win32.DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }
}
