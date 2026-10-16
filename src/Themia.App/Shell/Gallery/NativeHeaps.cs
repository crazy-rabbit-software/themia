#if DEBUG
using System.Runtime.InteropServices;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-6601] Debug only: where the process's native memory is, for the switch check. The process
/// heaps by committed size (<c>HeapSummary</c>), and the private memory outside them and outside the
/// managed heap, in large blocks (the media pipeline's and the compositor's buffers live there).
/// </summary>
internal static unsafe partial class NativeHeaps
{
    private const uint MemCommit = 0x1000;
    private const uint MemPrivate = 0x20000;

    /// <summary>The heaps' committed and allocated megabytes, the largest first, and the private bytes in blocks of 1 MB and more that no heap owns.</summary>
    public static string Describe()
    {
        var handles = new nint[256];
        var count = (int)GetProcessHeaps((uint)handles.Length, handles);
        var heaps = new List<(nint Handle, long Committed, long Allocated)>();

        for (var i = 0; i < Math.Min(count, handles.Length); i++)
        {
            var summary = new HeapSummaryInfo { Size = (uint)sizeof(HeapSummaryInfo) };
            if (HeapSummary(handles[i], 0, ref summary))
            {
                heaps.Add((handles[i], (long)summary.Committed, (long)summary.Allocated));
            }
        }

        var heapTotal = heaps.Sum(h => h.Committed);
        var inUse = heaps.Sum(h => h.Allocated);
        var mb = 1024.0 * 1024.0;
        var top = string.Join(" ", heaps.OrderByDescending(h => h.Committed).Take(4).Select(h => $"{h.Committed / mb:0}"));
        return $"heaps {heaps.Count} {heapTotal / mb:0} MB, {inUse / mb:0} in use (largest {top})  big private blocks {BigPrivateBlocks() / mb:0} MB";
    }

    /// <summary>Private committed memory in allocations of 32 MB reserved, the size the media and graphics buffers come in.</summary>
    private static long BigPrivateBlocks()
    {
        var process = GetCurrentProcess();
        nint address = 0;
        long total = 0;
        var info = default(MemoryInfo);

        while (VirtualQueryEx(process, address, out info, (nint)sizeof(MemoryInfo)) != 0)
        {
            if (info.State == MemCommit && info.Type == MemPrivate && Reserved(process, info.AllocationBase) == 32L * 1024 * 1024)
            {
                total += info.RegionSize;
            }

            var next = info.BaseAddress + info.RegionSize;
            if (next <= address)
            {
                break;
            }

            address = next;
        }

        return total;
    }

    /// <summary>How large the reservation starting at <paramref name="baseAddress"/> is.</summary>
    private static long Reserved(nint process, nint baseAddress)
    {
        long size = 0;
        var at = baseAddress;

        while (VirtualQueryEx(process, at, out var info, (nint)sizeof(MemoryInfo)) != 0 && info.AllocationBase == baseAddress)
        {
            size += info.RegionSize;
            at = info.BaseAddress + info.RegionSize;
        }

        return size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HeapSummaryInfo
    {
        public uint Size;
        public nuint Allocated;
        public nuint Committed;
        public nuint Reserved;
        public nuint MaxReserve;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [LibraryImport("kernel32.dll")]
    private static partial uint GetProcessHeaps(uint numberOfHeaps, [Out] nint[] heaps);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool HeapSummary(nint heap, uint flags, ref HeapSummaryInfo summary);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll")]
    private static partial nint VirtualQueryEx(nint process, nint address, out MemoryInfo buffer, nint length);
}
#endif
