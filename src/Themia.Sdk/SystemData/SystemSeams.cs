using Themia.App.Services;

namespace Themia.Sdk;

/// <summary>
/// [S-2431] The graphics adapters a settings page lists (the GPU widgets' adapter choice): the host's
/// DXGI enumeration, installed by the host once at start, as the place lookup is.
/// </summary>
public static class GpuAdapters
{
    private static Func<IReadOnlyList<GpuAdapter>>? _list;

    /// <summary>Every adapter, in DXGI's order (a position is the persisted choice).</summary>
    public static IReadOnlyList<GpuAdapter> List()
        => (Volatile.Read(ref _list) ?? throw new InvalidOperationException("no adapter list installed: the host installs it before any settings page is shown"))();

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

    /// <summary>The full path of the process's executable, or null where Windows will not say.</summary>
    public static string? ExePath(uint pid)
        => (Volatile.Read(ref _exePath) ?? throw new InvalidOperationException("no process paths installed: the host installs them before any widget is drawn"))(pid);

    /// <summary>The host's query. Once per process.</summary>
    internal static void Install(Func<uint, string?> exePath)
    {
        ArgumentNullException.ThrowIfNull(exePath);
        if (Interlocked.CompareExchange(ref _exePath, exePath, null) is not null)
        {
            throw new InvalidOperationException("the process paths are installed once per process");
        }
    }
}
