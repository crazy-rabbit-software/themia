using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>What the display configuration API says about one GDI display.</summary>
/// <param name="FriendlyName">The monitor's product name, or empty when it reports none.</param>
/// <param name="DevicePath">The monitor's device interface path, or empty when it has none ([S-842]).</param>
/// <param name="BuiltIn">[S-4441] A laptop's or tablet's own panel (internal, LVDS or embedded DisplayPort), whose brightness is Windows' power policy's.</param>
internal readonly record struct DisplayDescriptor(string FriendlyName, string DevicePath, bool BuiltIn = false);

/// <summary>
/// The name a person would recognise for a display, and the path that identifies it.
/// <para>
/// <c>\\.\DISPLAY1</c> identifies nothing: on a two-monitor desk it is a coin flip which is which,
/// and the number is assigned by Windows rather than by anything the user can see. The display
/// configuration API knows the monitor's actual product name — the string printed on the bezel —
/// and its device interface path, which is what Themia keys every per-display setting by ([S-842]).
/// </para>
/// </summary>
internal static partial class DisplayNames
{
    private const string Scope = "displays";

    private const uint QdcOnlyActivePaths = 0x00000002;
    private const int DeviceInfoGetSourceName = 1;
    private const int DeviceInfoGetTargetName = 2;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    /// <summary>
    /// How often a topology change between sizing and querying is retried. Each retry re-sizes the
    /// buffers for the new topology, so this only bounds a display that keeps changing.
    /// </summary>
    private const int QueryAttempts = 4;

    /// <summary>
    /// Map GDI device name (<c>\\.\DISPLAY1</c>) to the monitor's product name and device path.
    /// <para>
    /// Returns an empty map rather than throwing: a failure here degrades every display to its
    /// ordinal ([S-843]), which is logged, and must never stop the surfaces being built.
    /// </para>
    /// </summary>
    public static Dictionary<string, DisplayDescriptor> Describe()
    {
        var names = new Dictionary<string, DisplayDescriptor>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (!LayoutMatchesWindows() || Query() is not (var paths, var pathCount))
            {
                return names;
            }

            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];

                // Two lookups per path: the source says which \\.\DISPLAYn this is, the target says
                // which monitor it is. Neither alone is enough to build the mapping.
                var source = new SourceDeviceName
                {
                    Header = Header(DeviceInfoGetSourceName, Marshal.SizeOf<SourceDeviceName>(), path.SourceInfo.AdapterId, path.SourceInfo.Id),
                };

                if (DisplayConfigGetDeviceInfo(ref source) is var sourceResult and not ErrorSuccess)
                {
                    Log.Warn(Scope, $"no source name for display path {i} (win32 {sourceResult})");
                    continue;
                }

                var target = new TargetDeviceName
                {
                    Header = Header(DeviceInfoGetTargetName, Marshal.SizeOf<TargetDeviceName>(), path.TargetInfo.AdapterId, path.TargetInfo.Id),
                };

                if (DisplayConfigGetDeviceInfo(ref target) is var targetResult and not ErrorSuccess)
                {
                    Log.Warn(Scope, $"no monitor name for display path {i} (win32 {targetResult})");
                    continue;
                }

                var gdi = source.ViewGdiDeviceName;

                // A cloned source drives two monitors; the first path names it, as Windows lists it.
                if (string.IsNullOrWhiteSpace(gdi) || names.ContainsKey(gdi))
                {
                    continue;
                }

                names[gdi] = new DisplayDescriptor(target.MonitorFriendlyDeviceName.Trim(), target.MonitorDevicePath.Trim(), IsBuiltIn(target.OutputTechnology));
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read display names; falling back to device names", ex);
        }

        return names;
    }

    /// <summary>
    /// The active paths, re-sized and re-queried when the topology changes between the two calls
    /// (<c>ERROR_INSUFFICIENT_BUFFER</c>), as the API documents. Null, logged, on any other failure.
    /// </summary>
    private static (DisplayConfigPathInfo[] Paths, uint Count)? Query()
    {
        for (var attempt = 0; attempt < QueryAttempts; attempt++)
        {
            if (GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out var pathCount, out var modeCount) is var sized and not ErrorSuccess)
            {
                Log.Warn(Scope, $"GetDisplayConfigBufferSizes failed (win32 {sized}); displays fall back to their ordinal names");
                return null;
            }

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];

            var queried = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, 0);
            if (queried == ErrorSuccess)
            {
                return (paths, pathCount);
            }

            if (queried != ErrorInsufficientBuffer)
            {
                Log.Warn(Scope, $"QueryDisplayConfig failed (win32 {queried}); displays fall back to their ordinal names");
                return null;
            }
        }

        Log.Warn(Scope, "the display topology kept changing while it was read; displays fall back to their ordinal names");
        return null;
    }

    /// <summary>
    /// The structs below must match the Windows SDK byte for byte: a header size Windows does not
    /// expect is refused with <c>ERROR_INVALID_PARAMETER</c>, and a wrong path stride reads every path
    /// after the first at the wrong offset. Either silently costs every display its device path
    /// ([S-843]); a 64-bit LUID padding every struct that holds one once did exactly that, so the
    /// sizes are checked and a mismatch is logged as the error it is.
    /// </summary>
    private static unsafe bool LayoutMatchesWindows()
    {
        if (sizeof(DisplayConfigPathInfo) == 72
            && sizeof(DisplayConfigModeInfo) == 64
            && sizeof(DeviceInfoHeader) == 20
            && sizeof(SourceDeviceName) == 84
            && sizeof(TargetDeviceName) == 420)
        {
            return true;
        }

        Log.Error(Scope, $"DISPLAYCONFIG struct layout does not match Windows (path {sizeof(DisplayConfigPathInfo)}, " +
            $"mode {sizeof(DisplayConfigModeInfo)}, header {sizeof(DeviceInfoHeader)}, source {sizeof(SourceDeviceName)}, " +
            $"target {sizeof(TargetDeviceName)}); displays fall back to their ordinal names");
        return false;
    }

    /// <summary>DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL, _LVDS, _DISPLAYPORT_EMBEDDED, _UDI_EMBEDDED.</summary>
    private static bool IsBuiltIn(uint technology) => technology is 0x80000000 or 6 or 11 or 13;

    private static DeviceInfoHeader Header(int type, int size, Luid adapterId, uint id) => new()
    {
        Type = type,
        Size = (uint)size,
        AdapterId = adapterId,
        Id = id,
    };

    [LibraryImport("user32.dll")]
    private static partial int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [LibraryImport("user32.dll")]
    private static partial int QueryDisplayConfig(
        uint flags,
        ref uint pathCount,
        [Out] DisplayConfigPathInfo[] paths,
        ref uint modeCount,
        [Out] DisplayConfigModeInfo[] modes,
        nint currentTopologyId);

    [LibraryImport("user32.dll")]
    private static partial int DisplayConfigGetDeviceInfo(ref SourceDeviceName request);

    [LibraryImport("user32.dll")]
    private static partial int DisplayConfigGetDeviceInfo(ref TargetDeviceName request);

    /// <summary>
    /// <c>LUID</c>: two 32-bit halves, so 4-byte aligned. Never a <c>ulong</c>: that is 8-byte aligned
    /// and pads every DISPLAYCONFIG struct holding one away from the SDK layout.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct SourceDeviceName
    {
        public DeviceInfoHeader Header;
        public fixed char GdiDeviceName[32];

        public readonly string ViewGdiDeviceName
        {
            get
            {
                fixed (char* name = GdiDeviceName)
                {
                    return ReadFixed(name, 32);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;
        public fixed char FriendlyName[64];
        public fixed char DevicePath[128];

        public readonly string MonitorFriendlyDeviceName
        {
            get
            {
                fixed (char* name = FriendlyName)
                {
                    return ReadFixed(name, 64);
                }
            }
        }

        public readonly string MonitorDevicePath
        {
            get
            {
                fixed (char* path = DevicePath)
                {
                    return ReadFixed(path, 128);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigPathInfo
    {
        public PathSourceInfo SourceInfo;
        public PathTargetInfo TargetInfo;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public uint Rotation;
        public uint Scaling;
        public uint RefreshRateNumerator;
        public uint RefreshRateDenominator;
        public uint ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    /// <summary>Opaque here: the mode array must be passed through, but nothing in it is read.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DisplayConfigModeInfo
    {
        public uint InfoType;
        public uint Id;
        public Luid AdapterId;
        public ModeUnion Mode;
    }

    /// <summary>The 48-byte mode union. Its largest member holds 64-bit fields, hence the 8-byte alignment.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    private struct ModeUnion
    {
        [FieldOffset(0)]
        public ulong Reserved;
    }

    private static unsafe string ReadFixed(char* buffer, int capacity)
    {
        var length = 0;
        while (length < capacity && buffer[length] != '\0')
        {
            length++;
        }

        return new string(buffer, 0, length);
    }
}
