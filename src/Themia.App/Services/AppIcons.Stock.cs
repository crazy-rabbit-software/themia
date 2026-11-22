using System.Runtime.InteropServices;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.App.Interop;

namespace Themia.App.Services;

/// <summary>
/// [S-15165] The shell's stock icons (<c>SHGetStockIconInfo</c>): a kind of drive drawn the same on every PC and run, from the
/// system image list as every other icon, one extraction at a time with them ([S-1324]).
/// </summary>
internal static partial class AppIcons
{
    private const uint ShgsiSysIconIndex = 0x000004000;

    /// <summary>[S-15165] The shell's SIID of a kind of drive.</summary>
    public static int StockId(DriveStockIcon icon) => icon switch
    {
        DriveStockIcon.Fixed => 8, // SIID_DRIVEFIXED
        DriveStockIcon.Removable => 7, // SIID_DRIVEREMOVE
        DriveStockIcon.Optical => 11, // SIID_DRIVECD
        DriveStockIcon.Network => 9, // SIID_DRIVENET
        DriveStockIcon.NetworkDisconnected => 10, // SIID_DRIVENETDISABLED
        _ => 12, // SIID_DRIVERAM
    };

    /// <summary>[S-15165] The stock icon <paramref name="stockId"/> at <paramref name="size"/> (physical) pixels; null when there is none (logged).</summary>
    public static async Task<ImageSource?> LoadStockAsync(int stockId, int size)
    {
        try
        {
            await OneAtATime.WaitAsync().ConfigureAwait(true);
            Pixels? pixels;
            try
            {
                pixels = await Task.Run(() => ExtractStock(stockId, Math.Clamp(size, 16, 256))).ConfigureAwait(true);
            }
            finally
            {
                OneAtATime.Release();
            }

            return pixels is null ? null : ToBitmap(pixels);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not extract the stock icon {stockId}", ex);
            return null;
        }
    }

    private static unsafe Pixels? ExtractStock(int stockId, int size)
    {
        using var apartment = ComApi.Apartment.Enter(ComApi.MultiThreaded);
        var list = default(ComPtr);
        nint icon = 0;

        try
        {
            var info = new StockIconInfo { Size = (uint)sizeof(StockIconInfo) };
            if (SHGetStockIconInfo(stockId, ShgsiSysIconIndex, &info) < 0)
            {
                return null;
            }

            var which = size <= 20 ? ShilSmall : size <= 40 ? ShilLarge : size <= 48 ? ShilExtraLarge : ShilJumbo;
            return FromList(which, info.SysImageIndex, 0, ref list, ref icon);
        }
        finally
        {
            if (icon != 0)
            {
                DestroyIcon(icon);
            }

            list.Dispose();
        }
    }

    /// <summary>SHSTOCKICONINFO.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private unsafe struct StockIconInfo
    {
        public uint Size;
        public nint Icon;
        public int SysImageIndex;
        public int IconIndex;
        public fixed char Path[260];
    }

    [LibraryImport("shell32.dll")]
    private static unsafe partial int SHGetStockIconInfo(int stockId, uint flags, StockIconInfo* info);
}
