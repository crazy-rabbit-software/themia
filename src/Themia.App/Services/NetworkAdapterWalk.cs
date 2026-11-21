using System.Net.NetworkInformation;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-14961] [S-14962] This PC's network adapters as the Adapter line lists them: Windows' adapters by their connection
/// names ("Wi-Fi", "Ethernet 2"), up or not, without what the throughput never counts (<see cref="NetworkAdapterRules.Offered"/>);
/// and Windows' notice that they changed (an adapter came, went, connected or dropped: its addresses changed with it).
/// Walked when a menu is built, not polled.
/// </summary>
internal static class NetworkAdapterWalk
{
    private const string Scope = "network-adapter";

    /// <summary>Every adapter the line offers, now. Empty (logged) when Windows would not list them.</summary>
    public static IReadOnlyList<NetworkAdapterEntry> Now()
    {
        try
        {
            var all = NetworkInterface.GetAllNetworkInterfaces();
            var names = all.Select(n => n.Name).ToList();
            var adapters = new List<NetworkAdapterEntry>(all.Length);
            foreach (var nic in all)
            {
                if (nic.OperationalStatus == OperationalStatus.NotPresent
                    || !NetworkAdapterRules.Offered(nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel, nic.Name, nic.Description, names))
                {
                    continue;
                }

                adapters.Add(new NetworkAdapterEntry(nic.Id, nic.Name, NetworkRules.MediumOf((int)nic.NetworkInterfaceType), nic.OperationalStatus == OperationalStatus.Up));
            }

            return NetworkAdapterRules.Ordered(adapters);
        }
        catch (NetworkInformationException ex)
        {
            Log.Warn(Scope, "could not list the network adapters", ex);
            return [];
        }
        catch (PlatformNotSupportedException ex)
        {
            Log.Warn(Scope, "network adapters unsupported", ex);
            return [];
        }
    }

    /// <summary>
    /// [S-14962] Run <paramref name="changed"/> whenever Windows says an address changed, until disposed. The notice comes on
    /// a system thread: nothing may escape it.
    /// </summary>
    public static IDisposable Watch(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);

        void OnChanged(object? sender, EventArgs args)
        {
            try
            {
                changed();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "following the network adapters failed", ex);
            }
        }

        NetworkChange.NetworkAddressChanged += OnChanged;
        return new SharedLease(() => NetworkChange.NetworkAddressChanged -= OnChanged);
    }
}
