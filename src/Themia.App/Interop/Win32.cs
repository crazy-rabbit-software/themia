using System.Runtime.InteropServices;

namespace Themia.App.Interop;

/// <summary>
/// Raw Win32 surface. Every entry point uses <see cref="LibraryImportAttribute"/> so the marshalling
/// stubs are source generated at build time — <c>DllImport</c> builds them with runtime IL emission,
/// which does not exist under Native AOT.
/// </summary>
internal static partial class Win32
{
    // ---- window styles -------------------------------------------------------------------

    internal const int GwlExStyle = -20;
    internal const int GwlStyle = -16;
    internal const int GwlpWndProc = -4;

    internal const uint WsPopup = 0x80000000;
    internal const uint WsCaption = 0x00C00000;
    internal const uint WsThickFrame = 0x00040000;
    internal const uint WsSysMenu = 0x00080000;
    internal const uint WsMinimizeBox = 0x00020000;
    internal const uint WsMaximizeBox = 0x00010000;
    internal const uint WsClipChildren = 0x02000000;

    internal const uint WsExNoActivate = 0x08000000;
    internal const uint WsExToolWindow = 0x00000080;
    internal const uint WsExTopmost = 0x00000008;
    internal const uint WsExTransparent = 0x00000020;
    internal const uint WsExAppWindow = 0x00040000;

    // ---- SetWindowPos --------------------------------------------------------------------

    internal static readonly nint HwndTop = 0;
    internal static readonly nint HwndBottom = 1;
    internal static readonly nint HwndTopmost = -1;
    internal static readonly nint HwndNoTopmost = -2;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpNoOwnerZOrder = 0x0200;
    internal const uint SwpNoZOrder = 0x0004;

    // ---- system dispatcher queue (required to create a system Compositor) -------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct DispatcherQueueOptions
    {
        public int Size;
        public int ThreadType;
        public int ApartmentType;
    }

    internal const int DqTypeThreadCurrent = 2;
    internal const int DqTatComStA = 2;

    [LibraryImport("CoreMessaging.dll")]
    internal static partial int CreateDispatcherQueueController(DispatcherQueueOptions options, out nint instance);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsWindowVisible(nint hWnd);

    // ---- DWM ----------------------------------------------------------------------------------

    /// <summary>Windows 11 draws a 1px border around every top-level window unless told otherwise.</summary>
    internal const int DwmwaBorderColor = 34;

    /// <summary>The sentinel that means "no border at all", as opposed to a transparent one.</summary>
    internal const uint DwmwaColorNone = 0xFFFFFFFE;

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmSetWindowAttribute(nint hWnd, int attribute, in uint value, int size);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ShowWindow(nint hWnd, int nCmdShow);

    /// <summary>Show without stealing focus — the only way this app ever shows a window.</summary>
    internal const int SwShowNoActivate = 4;

    [LibraryImport("user32.dll")]
    internal static partial uint GetDpiForWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetClientRect(nint hWnd, out Rect lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetWindowRect(nint hWnd, out Rect lpRect);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool ClientToScreen(nint hWnd, ref Point lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out Point lpPoint);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetForegroundWindow(nint hWnd);

    // ---- regions (desktop click-through mask) ---------------------------------------------

    internal const int RgnOr = 2;

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial nint CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    internal static partial int CombineRgn(nint dest, nint src1, nint src2, int mode);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint hObject);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int SetWindowRgn(nint hWnd, nint hRgn, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    // ---- monitors -------------------------------------------------------------------------

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(nint hdc, nint lprcClip, nint lpfnEnum, nint dwData);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint hMonitor, ref MonitorInfoEx lpmi);

    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint hMonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>The display holding a screen point; <c>MONITOR_DEFAULTTOPRIMARY</c> (1) falls back to the primary one.</summary>
    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(Point pt, uint flags);

    // ---- message-only window ----------------------------------------------------------------

    internal const uint WmDestroy = 0x0002;
    internal const uint WmHotkey = 0x0312;

    internal static readonly nint HwndMessage = -3;

    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint CreateWindowEx(
        uint exStyle,
        string lpClassName,
        string? lpWindowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [LibraryImport("user32.dll", EntryPoint = "DestroyWindow", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DestroyWindow(nint hWnd);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static partial nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial nint SetWinEventHook(
        uint eventMin,
        uint eventMax,
        nint hmodWinEventProc,
        nint lpfnWinEventProc,
        uint idProcess,
        uint idThread,
        uint dwFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnhookWinEvent(nint hWinEventHook);

    [LibraryImport("user32.dll", EntryPoint = "GetClassNameW", SetLastError = true)]
    internal static unsafe partial int GetClassName(nint hWnd, char* lpClassName, int nMaxCount);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    internal static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial uint RegisterWindowMessage(string lpString);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    // ---- global hotkey (the keys are Core ShellHotkeys') ----------------------------------------

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static partial ushort RegisterClassEx(ref WndClassEx lpwcx);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint GetModuleHandle(string? lpModuleName);

    // ---- named pipes -------------------------------------------------------------------------

    /// <summary>
    /// The process serving a pipe. A client uses it to check it is talking to the program it thinks
    /// it is before handing over anything that implies trust.
    /// </summary>
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetNamedPipeServerProcessId(nint pipe, out uint serverProcessId);

    // ---- system metrics ----------------------------------------------------------------------

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    [LibraryImport("kernel32.dll", EntryPoint = "GlobalMemoryStatusEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

    /// <summary>A FILETIME as a pair of 32-bit halves; the tick count is the two joined.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileTime
    {
        public uint Low;
        public uint High;

        public readonly ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SystemPowerStatus
    {
        /// <summary>0 offline, 1 online, 255 unknown.</summary>
        public byte ACLineStatus;

        public byte BatteryFlag;

        /// <summary>0-100, or 255 for unknown (which is what a machine with no battery reports).</summary>
        public byte BatteryLifePercent;

        public byte SystemStatusFlag;

        /// <summary>Seconds remaining, or -1 (0xFFFFFFFF) when unknown.</summary>
        public uint BatteryLifeTime;

        public uint BatteryFullLifeTime;
    }

    // ---- structs ---------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly int Width => Right - Left;

        public readonly int Height => Bottom - Top;
    }

    /// <summary>
    /// Blittable by design: the inline character buffers are declared with <c>fixed</c> rather than
    /// <c>ByValTStr</c>, because a struct needing custom string marshalling cannot be handled by a
    /// source-generated P/Invoke (SYSLIB1051) — and source generation is what lets these calls work
    /// under Native AOT. It is also the faster path: the struct is passed straight through with no
    /// marshalling stub at all.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
        public fixed char DeviceNameBuffer[32];

        public readonly string DeviceName
        {
            get
            {
                fixed (char* name = DeviceNameBuffer)
                {
                    return ReadFixed(name, 32);
                }
            }
        }
    }

    /// <summary>Read a possibly-unterminated fixed buffer as a string.</summary>
    private static unsafe string ReadFixed(char* buffer, int capacity)
    {
        var length = 0;
        while (length < capacity && buffer[length] != '\0')
        {
            length++;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>Copy a string into a fixed buffer, truncating to leave room for the terminator.</summary>

    [StructLayout(LayoutKind.Sequential)]
    internal struct WndClassEx
    {
        public uint Size;
        public uint Style;
        public nint WndProc;
        public int ClsExtra;
        public int WndExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public nint MenuName;
        public nint ClassName;
        public nint IconSm;
    }
}
