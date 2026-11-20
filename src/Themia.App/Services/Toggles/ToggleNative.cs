using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] The Win32 and COM entry points the quick-setting toggles call. <see cref="LibraryImportAttribute"/>
/// only (Native AOT); COM through <see cref="Interop.ComApi"/>.
/// </summary>
internal static unsafe partial class ToggleNative
{
    /// <summary>An HSTRING the caller deletes with <see cref="WindowsDeleteString"/>; 0 where it could not be made.</summary>
    public static nint HString(string text)
    {
        nint made = 0;
        fixed (char* chars = text)
        {
            return WindowsCreateString(chars, (uint)text.Length, &made) < 0 ? 0 : made;
        }
    }

    /// <summary>An HSTRING's text (the string stays the caller's).</summary>
    public static string HStringText(nint text)
    {
        uint length = 0;
        var raw = WindowsGetStringRawBuffer(text, &length);
        return raw is null ? string.Empty : new string(raw, 0, (int)length);
    }

    [LibraryImport("combase.dll")]
    public static partial int WindowsCreateString(char* source, uint length, nint* hstring);

    [LibraryImport("combase.dll")]
    public static partial int WindowsDeleteString(nint hstring);

    [LibraryImport("combase.dll")]
    private static partial char* WindowsGetStringRawBuffer(nint hstring, uint* length);

    [LibraryImport("combase.dll")]
    public static partial int RoGetActivationFactory(nint activatableClassId, Guid* iid, nint* factory);

    // ---- libraries ---------------------------------------------------------------------------

    /// <summary>LOAD_LIBRARY_SEARCH_SYSTEM32: a Settings handler is only ever loaded from System32.</summary>
    public const uint LoadFromSystem32 = 0x800;

    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadLibraryEx(string name, nint file, uint flags);

    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    public static partial nint GetProcAddress(nint module, byte* name);

    /// <summary>GetProcAddress by ordinal: the "name" is the ordinal in the low word.</summary>
    [LibraryImport("kernel32.dll", EntryPoint = "GetProcAddress")]
    public static partial nint GetProcAddressOrdinal(nint module, nint ordinal);

    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string name);

    // ---- the handlers' message loop ----------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    public struct Msg
    {
        public nint Window;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int X;
        public int Y;
        public uint Private;
    }

    public const uint PmRemove = 0x1;
    public const uint QsAllInput = 0x04FF;
    public const uint MwmoInputAvailable = 0x4;
    public const uint Infinite = 0xFFFFFFFF;

    [LibraryImport("user32.dll", EntryPoint = "PeekMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PeekMessage(Msg* message, nint window, uint min, uint max, uint remove);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool TranslateMessage(Msg* message);

    [LibraryImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static partial nint DispatchMessage(Msg* message);

    [LibraryImport("user32.dll")]
    public static partial uint MsgWaitForMultipleObjectsEx(uint count, nint* handles, uint milliseconds, uint wakeMask, uint flags);

    // ---- rotation ----------------------------------------------------------------------------

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetAutoRotationState(uint* state);

    // ---- registry ----------------------------------------------------------------------------

    public const uint RegNotifyName = 0x1;
    public const uint RegNotifyLastSet = 0x4;
    public const uint RegNotifyThreadAgnostic = 0x10000000;

    [LibraryImport("advapi32.dll")]
    public static partial int RegNotifyChangeKeyValue(
        SafeRegistryHandle key,
        [MarshalAs(UnmanagedType.Bool)] bool subtree,
        uint filter,
        SafeWaitHandle signal,
        [MarshalAs(UnmanagedType.Bool)] bool asynchronous);

    // ---- power settings ----------------------------------------------------------------------

    public const uint DeviceNotifyCallback = 2;
    public const uint PbtPowerSettingChange = 0x8013;

    /// <summary>GUID_ENERGY_SAVER_STATUS (winnt.h, Windows 11 24H2 and later).</summary>
    public static readonly Guid EnergySaverStatus = new("550E8400-E29B-41D4-A716-446655440000");

    [StructLayout(LayoutKind.Sequential)]
    public struct SubscribeParameters
    {
        public delegate* unmanaged<void*, uint, void*, uint> Callback;
        public void* Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PowerBroadcastSetting
    {
        public Guid Setting;
        public uint Length;
        public byte Data;
    }

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerSettingRegisterNotification(Guid* setting, uint flags, nint recipient, nint* registration);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerSettingUnregisterNotification(nint registration);
}
