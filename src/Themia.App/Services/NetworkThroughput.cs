using System.Net.NetworkInformation;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-1190] Throughput across the machine's adapters, per adapter and in total, with session totals —
/// from the BCL's interface statistics (the same counters as <c>GetIfTable2</c>, with no P/Invoke
/// surface to keep AOT-safe).
/// <para>
/// Read by the system hub alone, once per due tick for every network widget ([S-2501]), so no tile
/// measures a rate over the few milliseconds since another one's read, and the session totals are the
/// process's, not a tile's. Single-threaded: every read happens on the UI thread's tick.
/// </para>
/// </summary>
internal static class NetworkThroughput
{
    private const string Scope = "metrics";

    private static readonly ThroughputTracker Tracker = new();

    /// <summary>
    /// This tick's sample: totals, each counted adapter busiest first, and the session totals. Null
    /// when Windows would not report the statistics; an empty adapter list is "no connection".
    /// </summary>
    public static ThroughputSample? Read()
    {
        var now = Environment.TickCount64;
        var counters = new List<InterfaceCounters>(4);

        try
        {
            var all = NetworkInterface.GetAllNetworkInterfaces();
            var names = all.Select(n => n.Name).ToList();

            foreach (var nic in all)
            {
                // [S-1192] Loopback and tunnels are the machine talking to itself; a capture mirror
                // or a filter binding re-reports the adapter beneath it.
                if (!NetworkRules.Counts(
                        nic.OperationalStatus == OperationalStatus.Up,
                        nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel,
                        nic.Name,
                        nic.Description)
                    || NetworkRules.IsFilterBinding(nic.Name, names))
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                counters.Add(new InterfaceCounters(nic.Id, nic.Name, stats.BytesReceived, stats.BytesSent, NetworkRules.MediumOf((int)nic.NetworkInterfaceType), nic.Description, Math.Max(0, nic.Speed)));
            }
        }
        catch (NetworkInformationException ex)
        {
            Log.Warn(Scope, "could not read interface statistics", ex);
            return null;
        }
        catch (PlatformNotSupportedException ex)
        {
            Log.Warn(Scope, "interface statistics unsupported", ex);
            return null;
        }

        // Monotonic, not DateTime.Now: a clock correction between two samples would divide a real
        // byte delta by a nonsense interval.
        return Tracker.Update(counters, now);
    }
}
