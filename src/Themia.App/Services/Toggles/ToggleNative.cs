using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Themia.App.Services.Toggles;

/// <summary>
/// [S-3907] The Win32 and COM entry points the quick-setting toggles call. <see cref="LibraryImportAttribute"/>
/// only (Native AOT), and COM through function-pointer vtables as in <c>CoreAudio.Native.cs</c>.
/// </summary>
internal static unsafe partial class ToggleNative
{
    public const uint ClsCtxLocalServer = 0x4;
    public const uint ClsCtxAll = 0x17;
    public const int SlotRelease = 2;
    public const int SlotQueryInterface = 0;

    /// <summary>The vtable entry <paramref name="slot"/> of a COM object.</summary>
    public static void* Slot(nint self, int slot) => (*(void***)self)[slot];

    public static void Release(nint self)
    {
        if (self != 0)
        {
            _ = ((delegate* unmanaged[Stdcall]<nint, uint>)Slot(self, SlotRelease))(self);
        }
    }

    /// <summary>QueryInterface, or 0 where the object does not have it.</summary>
    public static nint Query(nint self, Guid iid)
    {
        nint result = 0;
        return ((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Slot(self, SlotQueryInterface))(self, &iid, &result) >= 0 ? result : 0;
    }

    /// <summary>A COM object out of process (or in, with <paramref name="context"/>), or 0 with the HRESULT.</summary>
    public static nint Create(Guid clsid, Guid iid, uint context, out int result)
    {
        nint made = 0;
        result = CoCreateInstance(&clsid, 0, context, &iid, &made);
        return result >= 0 ? made : 0;
    }

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

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [LibraryImport("combase.dll")]
    public static partial int WindowsCreateString(char* source, uint length, nint* hstring);

    [LibraryImport("combase.dll")]
    public static partial int WindowsDeleteString(nint hstring);

    [LibraryImport("combase.dll")]
    private static partial char* WindowsGetStringRawBuffer(nint hstring, uint* length);

    [LibraryImport("combase.dll")]
    public static partial int RoGetActivationFactory(nint activatableClassId, Guid* iid, nint* factory);

    [LibraryImport("ole32.dll")]
    public static partial void CoTaskMemFree(void* memory);

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
