using Themia.Core.Widgets;

namespace Themia.Sdk;

/// <summary>
/// [S-14961] [S-14962] The network adapters the Adapter line lists (<see cref="NetworkAdapterRules.Offered"/>), and when
/// they change: the host's walk of Windows' adapters (the machine a render reads, the sample PC's under
/// <c>--machine sample</c>), installed by the host once at start, as the drive list is.
/// </summary>
public static class AdapterList
{
    private static Func<IReadOnlyList<NetworkAdapterEntry>>? _now;
    private static Func<Action, IDisposable>? _watch;

    /// <summary>Every adapter the line offers, now, in its order (<see cref="NetworkAdapterRules.Ordered"/>).</summary>
    public static IReadOnlyList<NetworkAdapterEntry> Now()
        => (Volatile.Read(ref _now) ?? throw new InvalidOperationException("no adapter list installed: the host installs it before any menu is shown"))();

    /// <summary>
    /// [S-14962] Be told when Windows' adapters may have changed (one came, went, connected or dropped): <paramref name="changed"/>
    /// runs on any thread, possibly more than once, until the watch is disposed.
    /// </summary>
    public static IDisposable Watch(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        return (Volatile.Read(ref _watch) ?? throw new InvalidOperationException("no adapter list installed: the host installs it before any menu is shown"))(changed);
    }

    /// <summary>The host's walk and its change notice. Once per process.</summary>
    internal static void Install(Func<IReadOnlyList<NetworkAdapterEntry>> now, Func<Action, IDisposable> watch)
    {
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(watch);
        if (Interlocked.CompareExchange(ref _now, now, null) is not null)
        {
            throw new InvalidOperationException("the adapter list is installed once per process");
        }

        Volatile.Write(ref _watch, watch);
    }
}
