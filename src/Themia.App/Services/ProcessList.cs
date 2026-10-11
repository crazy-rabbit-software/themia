using System.Diagnostics;
using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2410] [S-2414] The running processes, for the top processes widget.
/// <para>
/// One <c>NtQuerySystemInformation(SystemProcessInformation)</c> call: the kernel writes every
/// process's times, private working set and image name into a buffer this app owns, so there is no
/// per-process handle to be refused. <c>Process.TotalProcessorTime</c> needs a handle on each process
/// and is denied for everything elevated, protected or in another session (Defender, Search, the
/// service hosts), so a list built on it could never show the real hogs. WMI and
/// <c>PerformanceCounter</c> do not survive Native AOT. The buffer layout is Core's
/// (<see cref="ProcessTable.Parse"/>), the shares are Core's (<see cref="ProcessTracker"/>).
/// </para>
/// <para>
/// Read by the system hub alone, once per due tick for every list ([S-2501]), so two lists on one
/// desktop cost one walk and do not move each other's CPU baselines. Single-threaded: every call is
/// on the UI thread's tick. The buffer is
/// pinned and kept between reads (a few hundred KB; reallocating it each tick would be the most
/// expensive thing the widget did) and dropped when the last widget lets go.
/// </para>
/// </summary>
internal static unsafe partial class ProcessList
{
    private const string Scope = "processes";

    private const int SystemProcessInformationClass = 5;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);
    private const int MaxAttempts = 6;
    private const int InitialBytes = 256 * 1024;
    private const int SlackBytes = 64 * 1024;

    private static ProcessTracker _tracker = new();

    private static int _leases;
    private static byte[]? _buffer;
    private static string? _fault;

    /// <summary>Hold the reader; the buffer and the baselines go with the last holder.</summary>
    public static IDisposable Acquire()
    {
        _leases++;
        return new SharedLease(Release);
    }

    /// <summary>Every process with its share and memory, or null when the kernel would not answer.</summary>
    public static IReadOnlyList<ProcessSample>? Read()
    {
        var now = Stopwatch.GetTimestamp();

        if (Snapshot() is not { } entries)
        {
            return null;
        }

        var ticks = (long)(now * (TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency));
        return _tracker.Update(entries, ticks, Environment.ProcessorCount);
    }

    private static List<ProcessEntry>? Snapshot()
    {
        _buffer ??= GC.AllocateUninitializedArray<byte>(InitialBytes, pinned: true);

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            int status;
            uint needed;

            fixed (byte* data = _buffer)
            {
                status = NtQuerySystemInformation(SystemProcessInformationClass, data, (uint)_buffer.Length, out needed);

                if (status == 0)
                {
                    // Inside the pin: the names the kernel wrote are found by their address in it.
                    var length = (int)Math.Min(needed == 0 ? (uint)_buffer.Length : needed, (uint)_buffer.Length);
                    Note(null);
                    return ProcessTable.Parse(_buffer.AsSpan(0, length), (ulong)data);
                }
            }

            if (status != StatusInfoLengthMismatch)
            {
                Note($"NtQuerySystemInformation(processes) returned 0x{status:X8}");
                return null;
            }

            var target = Math.Max((long)needed + SlackBytes, _buffer.Length * 2L);
            _buffer = GC.AllocateUninitializedArray<byte>((int)Math.Min(target, 64L * 1024 * 1024), pinned: true);
        }

        Note("the process snapshot kept outgrowing its buffer");
        return null;
    }

    /// <summary>Log a change in whether the snapshot works, never once per tick.</summary>
    private static void Note(string? fault)
    {
        if (fault == _fault)
        {
            return;
        }

        var recovered = fault is null;
        _fault = fault;

        if (recovered)
        {
            Log.Info(Scope, "reading processes again");
        }
        else
        {
            Log.Warn(Scope, fault!);
        }
    }

    private static void Release()
    {
        _leases = Math.Max(0, _leases - 1);

        if (_leases > 0)
        {
            return;
        }

        _buffer = null;
        _tracker = new ProcessTracker();
    }

    [LibraryImport("ntdll")]
    private static partial int NtQuerySystemInformation(int informationClass, byte* information, uint length, out uint returnLength);
}
