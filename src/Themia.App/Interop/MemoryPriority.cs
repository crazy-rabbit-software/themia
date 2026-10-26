using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// [S-6612] The process's memory priority: below normal, so that under memory pressure Windows
/// repurposes Themia's trimmed pages before those of the apps the user works in. Not low: a widget
/// surface is on screen and redraws every second (the clock, the meters), so pages taken from it
/// before a background indexer's cache would come back as hard faults on the very next tick.
/// </summary>
internal static unsafe partial class MemoryPriority
{
    private const string Scope = "memory";

    /// <summary>PROCESS_INFORMATION_CLASS ProcessMemoryPriority.</summary>
    private const int ProcessMemoryPriority = 0;

    /// <summary>MEMORY_PRIORITY_BELOW_NORMAL (normal is 5, low 2).</summary>
    public const uint BelowNormal = 4;

    /// <summary>Lower this process's memory priority. A refusal is logged and changes nothing else: never fatal.</summary>
    public static void Lower()
    {
        var info = new MemoryPriorityInformation { MemoryPriority = BelowNormal };

        if (!SetProcessInformation(GetCurrentProcess(), ProcessMemoryPriority, &info, (uint)sizeof(MemoryPriorityInformation)))
        {
            Log.Warn(Scope, $"could not lower the memory priority (error {Marshal.GetLastPInvokeError()}); Windows trims Themia like any other app");
        }
    }

    /// <summary>This process's memory priority (1 very low .. 5 normal), or null when Windows will not say.</summary>
    public static uint? Current()
    {
        var info = default(MemoryPriorityInformation);

        if (!GetProcessInformation(GetCurrentProcess(), ProcessMemoryPriority, &info, (uint)sizeof(MemoryPriorityInformation)))
        {
            Log.Debug(Scope, $"could not read the memory priority (error {Marshal.GetLastPInvokeError()})");
            return null;
        }

        return info.MemoryPriority;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryPriorityInformation
    {
        public uint MemoryPriority;
    }

    /// <summary>The pseudo-handle of this process: nothing to close.</summary>
    [LibraryImport("kernel32.dll")]
    private static partial nint GetCurrentProcess();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetProcessInformation(nint process, int infoClass, MemoryPriorityInformation* info, uint size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetProcessInformation(nint process, int infoClass, MemoryPriorityInformation* info, uint size);
}
