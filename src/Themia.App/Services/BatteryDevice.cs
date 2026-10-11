using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2423] What the battery itself reports — design and full-charge capacity, remaining energy,
/// charge rate, voltage, cycle count, temperature, chemistry and maker — straight from the battery
/// class driver's IOCTLs.
/// <para>
/// Not WMI, which is where the original read these (<c>root\WMI BatteryStaticData</c> and friends):
/// <c>System.Management</c> is not AOT-safe, and the WMI classes are themselves a wrapper over these
/// same IOCTLs. <c>SetupDi</c> finds each battery's device interface, <c>IOCTL_BATTERY_QUERY_TAG</c>
/// names the battery inside it, and <c>_QUERY_INFORMATION</c> / <c>_QUERY_STATUS</c> return the
/// structures <see cref="BatteryDetailRules"/> parses. Every call returns at once (a zero wait), so the
/// read runs on the tick.
/// </para>
/// <para>
/// Shared by every battery tile: one read every couple of seconds at most, and the device list is
/// re-enumerated once a minute (or when a battery stops answering) rather than every sample.
/// </para>
/// </summary>
internal static unsafe partial class BatteryDevice
{
    private const string Scope = "battery";

    /// <summary>GUID_DEVCLASS_BATTERY / GUID_DEVICE_BATTERY.</summary>
    private static readonly Guid BatteryInterface = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");

    private const uint DigcfPresent = 0x2;
    private const uint DigcfDeviceInterface = 0x10;

    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint ShareReadWrite = 0x3;
    private const uint OpenExisting = 3;
    private const uint AttributeNormal = 0x80;

    // CTL_CODE(FILE_DEVICE_BATTERY, 0x10 / 0x11 / 0x13, METHOD_BUFFERED, FILE_READ_ACCESS).
    private const uint IoctlQueryTag = 0x294040;
    private const uint IoctlQueryInformation = 0x294044;
    private const uint IoctlQueryStatus = 0x29404C;

    // BATTERY_QUERY_INFORMATION_LEVEL.
    private const int LevelInformation = 0;
    private const int LevelTemperature = 2;
    private const int LevelManufacturer = 6;

    /// <summary>How long one read is shared between tiles and ticks.</summary>
    private const long ReuseMs = 2_000;

    /// <summary>How long the list of battery devices is trusted.</summary>
    private const long DevicesMs = 60_000;

    /// <summary>Two batteries is common (ThinkPads); more is a misbehaving driver.</summary>
    private const int MaxBatteries = 4;

    private static IReadOnlyList<string> _paths = [];
    private static long? _pathsAt;
    private static BatteryPack? _last;
    private static long? _lastAt;
    private static string? _lastFailure;

    /// <summary>
    /// The batteries, summed (<see cref="BatteryDetailRules.Combine"/>), or null when no battery device
    /// answered — a desktop, or firmware that exposes the charge to Windows but not the details.
    /// </summary>
    public static BatteryPack? Read()
    {
        var now = Environment.TickCount64;

        if (_lastAt is { } at && now - at < ReuseMs && now >= at)
        {
            return _last;
        }

        if (_pathsAt is not { } listed || now - listed >= DevicesMs || now < listed)
        {
            _paths = Enumerate();
            _pathsAt = now;
        }

        var batteries = new List<(BatteryInformation?, BatteryState?, double?, string?)>(_paths.Count);
        var failed = false;

        foreach (var path in _paths)
        {
            if (ReadOne(path) is { } one)
            {
                batteries.Add(one);
            }
            else
            {
                failed = true;
            }
        }

        // A battery that stopped answering (undocked, driver reloaded) means the list is stale.
        if (failed)
        {
            _pathsAt = null;
        }

        _last = batteries.Count == 0 ? null : BatteryDetailRules.Combine(batteries);
        _lastAt = now;
        return _last;
    }

    /// <summary>Each present battery's device path.</summary>
    private static List<string> Enumerate()
    {
        var paths = new List<string>(2);
        var guid = BatteryInterface;
        var set = SetupDiGetClassDevsW(&guid, null, 0, DigcfPresent | DigcfDeviceInterface);

        if (set == -1 || set == 0)
        {
            Note($"SetupDiGetClassDevs failed: {Marshal.GetLastPInvokeError()}");
            return paths;
        }

        try
        {
            for (uint index = 0; index < MaxBatteries; index++)
            {
                var data = new DeviceInterfaceData { Size = (uint)sizeof(DeviceInterfaceData) };

                if (!SetupDiEnumDeviceInterfaces(set, null, &guid, index, &data))
                {
                    // ERROR_NO_MORE_ITEMS is the end of the list, which is the normal way out.
                    break;
                }

                uint required = 0;
                _ = SetupDiGetDeviceInterfaceDetailW(set, &data, null, 0, &required, null); // ignore: sizing call, fails by design

                if (required is < 8 or > 4096)
                {
                    continue;
                }

                var buffer = new byte[required];

                fixed (byte* detail = buffer)
                {
                    // cbSize is the size of the fixed part: a DWORD and the first WCHAR, padded to 8 on x64.
                    *(uint*)detail = IntPtr.Size == 8 ? 8u : 6u;

                    if (SetupDiGetDeviceInterfaceDetailW(set, &data, detail, required, null, null))
                    {
                        var path = new string((char*)(detail + 4), 0, (int)((required - 4) / sizeof(char))).TrimEnd('\0');
                        var end = path.IndexOf('\0', StringComparison.Ordinal);
                        paths.Add(end >= 0 ? path[..end] : path);
                    }
                }
            }
        }
        finally
        {
            _ = SetupDiDestroyDeviceInfoList(set);
        }

        return paths;
    }

    /// <summary>One battery's figures, or null when its device would not open or name a battery.</summary>
    private static (BatteryInformation?, BatteryState?, double?, string?)? ReadOne(string path)
    {
        using var device = CreateFile(path, GenericRead | GenericWrite, ShareReadWrite, 0, OpenExisting, AttributeNormal, 0);

        if (device.IsInvalid)
        {
            Note($"could not open a battery device: {Marshal.GetLastPInvokeError()}");
            return null;
        }

        uint wait = 0;
        uint tag = 0;
        uint returned = 0;

        if (!DeviceIoControl(device, IoctlQueryTag, &wait, sizeof(uint), &tag, sizeof(uint), &returned, 0) || tag == 0)
        {
            // No tag is "no battery in this slot" (an empty second bay), not a fault.
            return null;
        }

        Note(null);

        var info = stackalloc byte[BatteryDetailRules.InformationSize];
        var information = Query(device, tag, LevelInformation, info, BatteryDetailRules.InformationSize, out var infoBytes)
            ? BatteryDetailRules.ParseInformation(new ReadOnlySpan<byte>(info, (int)infoBytes))
            : null;

        uint kelvin = 0;
        var temperature = Query(device, tag, LevelTemperature, (byte*)&kelvin, sizeof(uint), out var tempBytes) && tempBytes == sizeof(uint)
            ? BatteryDetailRules.Celsius(kelvin)
            : null;

        var name = stackalloc char[64];
        var manufacturer = Query(device, tag, LevelManufacturer, (byte*)name, 64 * sizeof(char), out var nameBytes)
            ? new string(name, 0, (int)Math.Min(64, nameBytes / sizeof(char)))
            : null;

        var waitStatus = stackalloc uint[5];
        waitStatus[0] = tag;
        waitStatus[1] = 0;
        waitStatus[2] = 0;
        waitStatus[3] = 0;
        waitStatus[4] = 0;
        var status = stackalloc byte[BatteryDetailRules.StatusSize];
        var state = DeviceIoControl(device, IoctlQueryStatus, waitStatus, 5 * sizeof(uint), status, BatteryDetailRules.StatusSize, &returned, 0)
            ? BatteryDetailRules.ParseStatus(new ReadOnlySpan<byte>(status, (int)returned))
            : null;

        return (information, state, temperature, manufacturer);
    }

    /// <summary>
    /// One <c>IOCTL_BATTERY_QUERY_INFORMATION</c>. A level the firmware does not support fails with
    /// ERROR_INVALID_FUNCTION, which is the ordinary answer for temperature and maker, so a failure
    /// here is a missing figure and is not logged.
    /// </summary>
    private static bool Query(SafeFileHandle device, uint tag, int level, byte* output, int size, out uint returned)
    {
        var query = stackalloc int[3];
        query[0] = (int)tag;
        query[1] = level;
        query[2] = 0;
        uint got = 0;
        var ok = DeviceIoControl(device, IoctlQueryInformation, query, 3 * sizeof(int), output, (uint)size, &got, 0);
        returned = ok ? Math.Min(got, (uint)size) : 0;
        return ok;
    }

    /// <summary>Log a failure once, on the change, not every tick for as long as it lasts.</summary>
    private static void Note(string? failure)
    {
        if (failure == _lastFailure)
        {
            return;
        }

        _lastFailure = failure;

        if (failure is not null)
        {
            Log.Warn(Scope, failure);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInterfaceData
    {
        public uint Size;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public nint Reserved;
    }

    [LibraryImport("setupapi.dll", SetLastError = true)]
    private static partial nint SetupDiGetClassDevsW(Guid* classGuid, char* enumerator, nint parent, uint flags);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiEnumDeviceInterfaces(nint set, void* deviceInfo, Guid* interfaceGuid, uint index, DeviceInterfaceData* data);

    [LibraryImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiGetDeviceInterfaceDetailW(nint set, DeviceInterfaceData* data, void* detail, uint detailSize, uint* required, void* deviceInfo);

    [LibraryImport("setupapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetupDiDestroyDeviceInfoList(nint set);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, void* input, uint inputSize, void* output, uint outputSize, uint* returned, nint overlapped);
}
