using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2408] [S-2409] The machine-wide counts and memory figures behind the CPU and memory details:
/// one <c>GetPerformanceInfo</c> call (commit, cache, kernel pools, processes, threads, handles) and,
/// for the page file, <c>NtQuerySystemInformation(SystemPageFileInformation)</c>. Both answer at once
/// and need no rights, so they run on the tick; widgets reading in the same tick share one read.
/// </summary>
internal static unsafe partial class PerformanceInfo
{
    private const string Scope = "memory";

    /// <summary>SYSTEM_INFORMATION_CLASS SystemPageFileInformation.</summary>
    private const int SystemPageFileInformation = 18;

    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    private static readonly SampleCache<Snapshot> Cache = new();
    private static string? _fault;

    /// <summary>The counts and memory figures, or null when Windows would not say.</summary>
    public static PerformanceFigures? Read() => Take().Figures;

    /// <summary>[S-2409] The page files, or null when they could not be read (no page file is zeros).</summary>
    public static PagefileUsage? Pagefile() => Take().Pagefile;

    private static Snapshot Take()
    {
        var now = Environment.TickCount64;

        if (Cache.TryReuse(now, out var recent))
        {
            return recent;
        }

        var info = new PerformanceInformation { Size = (uint)sizeof(PerformanceInformation) };
        PerformanceFigures? figures = null;
        ulong pageSize = 4096;

        if (GetPerformanceInfo(&info, info.Size))
        {
            pageSize = info.PageSize > 0 ? info.PageSize : 4096;
            figures = new PerformanceFigures(
                MemoryDetailRules.Bytes(info.CommitTotal, pageSize),
                MemoryDetailRules.Bytes(info.CommitLimit, pageSize),
                MemoryDetailRules.Bytes(info.PhysicalTotal, pageSize),
                MemoryDetailRules.Bytes(info.PhysicalAvailable, pageSize),
                MemoryDetailRules.Bytes(info.SystemCache, pageSize),
                MemoryDetailRules.Bytes(info.KernelPaged, pageSize),
                MemoryDetailRules.Bytes(info.KernelNonpaged, pageSize),
                info.HandleCount,
                info.ProcessCount,
                info.ThreadCount);
            Note(null);
        }
        else
        {
            Note($"GetPerformanceInfo failed: {Marshal.GetLastPInvokeError()}");
        }

        var snapshot = new Snapshot(figures, ReadPagefile(pageSize));
        Cache.Store(now, snapshot);
        return snapshot;
    }

    private static PagefileUsage? ReadPagefile(ulong pageSize)
    {
        var size = 1024;

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var buffer = new byte[size];
            int status;
            uint returned;

            fixed (byte* data = buffer)
            {
                status = NtQuerySystemInformation(SystemPageFileInformation, data, (uint)size, out returned);
            }

            if (status == 0)
            {
                return MemoryDetailRules.ParsePagefiles(buffer.AsSpan(0, (int)Math.Min(returned, (uint)size)), pageSize);
            }

            if (status != StatusInfoLengthMismatch)
            {
                Note($"NtQuerySystemInformation(page file) returned 0x{status:X8}");
                return null;
            }

            size = (int)Math.Max(returned, (uint)size * 2);
        }

        return null;
    }

    /// <summary>Log a failure once, when it starts, not on every tick it lasts.</summary>
    private static void Note(string? fault)
    {
        if (fault is null || fault == _fault)
        {
            return;
        }

        _fault = fault;
        Log.Warn(Scope, fault);
    }

    private readonly record struct Snapshot(PerformanceFigures? Figures, PagefileUsage? Pagefile);

    [LibraryImport("kernel32.dll", EntryPoint = "K32GetPerformanceInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetPerformanceInfo(PerformanceInformation* information, uint size);

    [LibraryImport("ntdll")]
    private static partial int NtQuerySystemInformation(int informationClass, byte* information, uint length, out uint returnLength);

    /// <summary>PERFORMANCE_INFORMATION; every SIZE_T is a page count except <see cref="PageSize"/>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInformation
    {
        public uint Size;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }
}
