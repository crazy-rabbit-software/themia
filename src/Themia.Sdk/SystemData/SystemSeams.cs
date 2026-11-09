using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-2431] The graphics adapters a properties menu lists (the GPU widgets' adapter choice): the host's
/// DXGI enumeration, installed by the host once at start, as the place lookup is.
/// </summary>
public static class GpuAdapters
{
    private static Func<IReadOnlyList<GpuAdapter>>? _list;

    /// <summary>Every adapter, in DXGI's order (a position is the persisted choice).</summary>
    public static IReadOnlyList<GpuAdapter> List()
        => (Volatile.Read(ref _list) ?? throw new InvalidOperationException("no adapter list installed: the host installs it before any menu is shown"))();

    /// <summary>The host's enumeration. Once per process.</summary>
    internal static void Install(Func<IReadOnlyList<GpuAdapter>> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (Interlocked.CompareExchange(ref _list, list, null) is not null)
        {
            throw new InvalidOperationException("the adapter list is installed once per process");
        }
    }
}

/// <summary>
/// [S-2414] A process's executable, for its row's icon and name (the top processes' list): the host's
/// process query, installed by the host once at start.
/// </summary>
public static class ProcessPaths
{
    private static Func<uint, string?>? _exePath;
    private static Func<string, string?>? _displayName;

    /// <summary>The full path of the process's executable, or null where Windows will not say.</summary>
    public static string? ExePath(uint pid)
        => (Volatile.Read(ref _exePath) ?? throw new InvalidOperationException("no process paths installed: the host installs them before any widget is drawn"))(pid);

    /// <summary>[S-458] What an executable calls itself (its version resource's description), or null where it says nothing.</summary>
    public static string? DisplayName(string path)
        => (Volatile.Read(ref _displayName) ?? throw new InvalidOperationException("no process paths installed: the host installs them before any widget is drawn"))(path);

    /// <summary>The host's queries. Once per process.</summary>
    internal static void Install(Func<uint, string?> exePath, Func<string, string?> displayName)
    {
        ArgumentNullException.ThrowIfNull(exePath);
        ArgumentNullException.ThrowIfNull(displayName);
        if (Interlocked.CompareExchange(ref _exePath, exePath, null) is not null)
        {
            throw new InvalidOperationException("the process paths are installed once per process");
        }

        Volatile.Write(ref _displayName, displayName);
    }
}

/// <summary>[S-2437] What came of asking Windows to end a process.</summary>
public enum ProcessEndResult
{
    /// <summary>Windows ended it.</summary>
    Ended,

    /// <summary>It had exited already, or its pid is someone else's now: nothing was ended.</summary>
    Gone,

    /// <summary>Windows refused: a protected, elevated or system process, which an unelevated app cannot end.</summary>
    Denied,
}

/// <summary>
/// [S-2437] Ending a process (the top processes' "End task"): the host's call, installed once at
/// start as the process paths are. The host ends a process only when its pid still names the process
/// that started at <see cref="ProcessIdentity.CreateTime"/>, so a pid Windows has handed on since the
/// list was read is never ended by mistake.
/// </summary>
public static class ProcessControl
{
    private static Func<ProcessIdentity, ProcessEndResult>? _end;

    /// <summary>End <paramref name="process"/>, as Task Manager's End task does (no chance to save).</summary>
    public static ProcessEndResult End(ProcessIdentity process)
        => (Volatile.Read(ref _end) ?? throw new InvalidOperationException("no process control installed: the host installs it before any widget is drawn"))(process);

    /// <summary>The host's call. Once per process.</summary>
    internal static void Install(Func<ProcessIdentity, ProcessEndResult> end)
    {
        ArgumentNullException.ThrowIfNull(end);
        if (Interlocked.CompareExchange(ref _end, end, null) is not null)
        {
            throw new InvalidOperationException("the process control is installed once per process");
        }
    }
}
