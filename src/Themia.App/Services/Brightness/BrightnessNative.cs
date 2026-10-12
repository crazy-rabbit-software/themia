using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.App.Interop;

namespace Themia.App.Services.Brightness;

/// <summary>
/// [S-4441] The entry points the brightness slider calls: the Monitor Configuration API (dxva2) for a
/// monitor over DDC/CI, and the power policy (powrprof) for a built-in panel. <see cref="LibraryImportAttribute"/>
/// only (Native AOT); no WMI.
/// </summary>
internal static unsafe partial class BrightnessNative
{
    /// <summary>GUID_VIDEO_SUBGROUP.</summary>
    public static readonly Guid VideoSubgroup = new("7516b95f-f776-4464-8c53-06167f40cc99");

    /// <summary>GUID_DEVICE_POWER_POLICY_VIDEO_BRIGHTNESS: the panel's brightness in the power plan, 0..100.</summary>
    public static readonly Guid VideoBrightness = new("aded5e82-b909-4619-9949-f5d71dac0bcb");

    /// <summary>GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS: the panel's brightness now, sent at registration and on every change.</summary>
    public static readonly Guid CurrentMonitorBrightness = new("8ffee2c6-2d01-46be-adb9-398addc5b4ff");

    public const uint MonitorDefaultToNearest = 2;

    /// <summary>MC_CAPS_BRIGHTNESS.</summary>
    public const uint CapsBrightness = 0x00000002;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PhysicalMonitor
    {
        public nint Handle;
        public fixed char Description[128];
    }

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, out uint count);

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, PhysicalMonitor* monitors);

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyPhysicalMonitor(nint monitor);

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorCapabilities(SafePhysicalMonitor monitor, out uint capabilities, out uint colorTemperatures);

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorBrightness(SafePhysicalMonitor monitor, out uint minimum, out uint current, out uint maximum);

    [LibraryImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetMonitorBrightness(SafePhysicalMonitor monitor, uint brightness);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerGetActiveScheme(nint user, Guid** scheme);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadACValueIndex(nint root, Guid* scheme, Guid* subgroup, Guid* setting, uint* value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadDCValueIndex(nint root, Guid* scheme, Guid* subgroup, Guid* setting, uint* value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteACValueIndex(nint root, Guid* scheme, Guid* subgroup, Guid* setting, uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteDCValueIndex(nint root, Guid* scheme, Guid* subgroup, Guid* setting, uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerSetActiveScheme(nint user, Guid* scheme);

    [LibraryImport("kernel32.dll")]
    public static partial nint LocalFree(nint memory);

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromWindow(nint window, uint flags);

    /// <summary>The GDI name (<c>\\.\DISPLAY1</c>) of the display a window is on; null where it cannot say.</summary>
    public static string? DeviceOf(nint window)
    {
        if (window == 0)
        {
            return null;
        }

        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return null;
        }

        var info = new Win32.MonitorInfoEx { Size = sizeof(Win32.MonitorInfoEx) };
        return Win32.GetMonitorInfo(monitor, ref info) && !string.IsNullOrEmpty(info.DeviceName) ? info.DeviceName : null;
    }

    /// <summary>The power plan in force; null where Windows would not say. The GUID is copied and Windows' block freed.</summary>
    public static Guid? ActiveScheme()
    {
        Guid* scheme = null;
        if (PowerGetActiveScheme(0, &scheme) != 0 || scheme is null)
        {
            return null;
        }

        try
        {
            return *scheme;
        }
        finally
        {
            _ = LocalFree((nint)scheme);
        }
    }

    /// <summary>On mains power, or without a battery: the plan's AC value is the one in force.</summary>
    public static bool OnMains() => !GetSystemPowerStatus(out var status) || status.AcLineStatus != 0;
}

/// <summary>A physical monitor handle from <c>GetPhysicalMonitorsFromHMONITOR</c>, destroyed on every path.</summary>
internal sealed class SafePhysicalMonitor : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafePhysicalMonitor()
        : base(ownsHandle: true)
    {
    }

    public SafePhysicalMonitor(nint handle)
        : base(ownsHandle: true) => SetHandle(handle);

    protected override bool ReleaseHandle() => BrightnessNative.DestroyPhysicalMonitor(handle);
}
