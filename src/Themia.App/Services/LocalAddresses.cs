using System.Net.NetworkInformation;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2419] Each adapter's local address, by adapter id, for the network details widget.
/// <para>
/// Addresses change when a network does, not every second, so the walk (one
/// <c>GetIPProperties</c> per adapter) is made at most every <see cref="RefreshMs"/> and at once
/// after Windows says the addresses changed. Read on the UI thread's tick.
/// </para>
/// </summary>
internal static class LocalAddresses
{
    private const string Scope = "metrics";

    private const long RefreshMs = 60_000;

    private static IReadOnlyDictionary<string, string> _addresses = new Dictionary<string, string>();
    private static long? _readAt;
    private static int _leases;

    // Set on the system's thread pool; read on the tick.
    private static volatile bool _changed;

    public static IDisposable Acquire()
    {
        if (_leases++ == 0)
        {
            NetworkChange.NetworkAddressChanged += OnChanged;
            _readAt = null;
        }

        return new SharedLease(Release);
    }

    /// <summary>The addresses as of the last walk; walks again when that is stale.</summary>
    public static IReadOnlyDictionary<string, string> Current()
    {
        var now = Environment.TickCount64;

        if (_changed || _readAt is not { } at || now - at >= RefreshMs || now < at)
        {
            _changed = false;
            _readAt = now;
            _addresses = Walk();
        }

        return _addresses;
    }

    private static Dictionary<string, string> Walk()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                var address = NetworkDetailRules.LocalAddress(nic.GetIPProperties().UnicastAddresses.Select(u => u.Address));

                if (address is not null)
                {
                    result[nic.Id] = address;
                }
            }
        }
        catch (NetworkInformationException ex)
        {
            Log.Warn(Scope, "could not read the adapters' addresses", ex);
        }
        catch (PlatformNotSupportedException ex)
        {
            Log.Warn(Scope, "adapter addresses unsupported", ex);
        }

        return result;
    }

    private static void OnChanged(object? sender, EventArgs args) => _changed = true;

    private static void Release()
    {
        _leases = Math.Max(0, _leases - 1);

        if (_leases == 0)
        {
            NetworkChange.NetworkAddressChanged -= OnChanged;
            _addresses = new Dictionary<string, string>();
        }
    }
}
