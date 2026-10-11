using System.Runtime.InteropServices;
using Themia.App.Interop;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1130] The machine's memory, from <c>GlobalMemoryStatusEx</c>, read by the system hub once per
/// due tick for every memory widget ([S-2501]).
/// </summary>
internal static class MemoryCounters
{
    /// <summary>One snapshot, or null when Windows would not say how much memory there is.</summary>
    public static MemorySnapshot? Read()
    {
        var status = new Win32.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Win32.MemoryStatusEx>() };

        if (!Win32.GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            return null;
        }

        return new MemorySnapshot(status.TotalPhys, status.AvailPhys);
    }
}
