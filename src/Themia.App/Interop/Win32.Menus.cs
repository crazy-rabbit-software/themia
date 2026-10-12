using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>What Windows' own menus wait for.</summary>
internal static partial class Win32
{
    private const uint SpiGetMenuShowDelay = 0x006A;

    /// <summary>Windows' default for <see cref="MenuShowDelay"/> when the setting cannot be read.</summary>
    private const uint DefaultMenuShowDelayMs = 400;

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfoUInt(uint action, uint param, out uint value, uint winIni);

    /// <summary>
    /// [S-3514] How long the pointer rests on a menu row before its submenu opens (SPI_GETMENUSHOWDELAY),
    /// so a submenu of ours opens as Windows' own do — and as fast, for someone who set it to 0.
    /// </summary>
    public static TimeSpan MenuShowDelay()
    {
        if (SystemParametersInfoUInt(SpiGetMenuShowDelay, 0, out var ms, 0))
        {
            return TimeSpan.FromMilliseconds(ms);
        }

        Log.Warn("win32", $"SPI_GETMENUSHOWDELAY failed ({Marshal.GetLastPInvokeError()}); using {DefaultMenuShowDelayMs} ms");
        return TimeSpan.FromMilliseconds(DefaultMenuShowDelayMs);
    }
}
