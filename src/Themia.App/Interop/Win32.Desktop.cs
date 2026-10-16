using System.Runtime.InteropServices;

namespace Themia.App.Interop;

/// <summary>
/// The Win32 surface desktop integration needs ([S-920..S-939]): the shell's attention state, the
/// shell hook, the desktop icon view, input injection for dismissing Start, and the COM entry points
/// for talking to the desktop's folder view.
/// </summary>
internal static unsafe partial class Win32
{
    internal const uint WmCommand = 0x0111;
    internal const uint WmDisplayChangeMessage = 0x007E;

    internal const uint EventObjectShow = 0x8002;
    internal const uint EventObjectHide = 0x8003;

    internal const uint InputKeyboard = 1;
    internal const uint KeyEventKeyUp = 0x0002;
    internal const ushort VkEscape = 0x1B;

    internal const uint SmtoAbortIfHung = 0x0002;
    internal const uint SmtoBlock = 0x0001;

    internal const uint CoInitMultithreaded = 0x0;
    internal const uint ClsCtxLocalServer = 0x4;
    internal const int RpcEChangedMode = unchecked((int)0x80010106);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    /// <summary>The <c>INPUT</c> union, sized by its largest member (the mouse) as Windows expects.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    /// <summary>A <c>VARIANT</c>: 16 bytes on 32-bit, 24 on 64-bit, because of its record arm.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Variant
    {
        public ushort Vt;
        public ushort Reserved1;
        public ushort Reserved2;
        public ushort Reserved3;
        public nint Value;
        public nint Record;
    }

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hWnd, out uint processId);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindWindowEx(nint parent, nint childAfter, string? className, string? windowName);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterShellHookWindow(nint hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeregisterShellHookWindow(nint hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint SendInput(uint count, Input* inputs, int size);

    /// <summary>The window the shell registered as the desktop: Explorer's <c>Progman</c>, or 0 when no shell runs.</summary>
    [LibraryImport("user32.dll")]
    internal static partial nint GetShellWindow();

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
    internal static partial nint SendMessageTimeout(nint hWnd, uint msg, nint wParam, nint lParam, uint flags, uint timeout, out nint result);

    /// <summary>The shell's attention state. Returns an HRESULT; the state is a <c>QUERY_USER_NOTIFICATION_STATE</c>.</summary>
    [LibraryImport("shell32.dll")]
    internal static partial int SHQueryUserNotificationState(out int state);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    internal static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);
}
