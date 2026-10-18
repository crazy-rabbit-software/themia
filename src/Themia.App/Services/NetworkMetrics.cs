using System.Net.NetworkInformation;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// Round-trip probes for the ping meter. (Throughput is [S-1190] <see cref="NetworkThroughput"/>.)
/// <para>
/// The BCL's <c>Ping</c>, not a raw ICMP socket: the socket would need a P/Invoke surface to keep
/// AOT-safe and would buy nothing a widget can show.
/// </para>
/// </summary>
internal static class NetworkMetrics
{
    private const string Scope = "metrics";

    // A latency bar needs a ceiling to be a bar at all, and round-trips have no natural maximum.
    // 300 ms is the point past which a link feels broken rather than slow, so it is where the bar
    // fills. Deliberately not the link speed: an adapter reports its negotiated line rate, which
    // says nothing about how long a packet takes to come back, and a 1 Gb NIC would pin every
    // round-trip at the bottom of the bar forever.
    internal const double LatencyCeilingMs = 300;

    private static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Round-trip time to <paramref name="host"/>.
    /// <para>
    /// Nothing here logs. A host that is down answers the same way every ten seconds for as long as
    /// it stays down, so logging in here would be a log line per tick forever; the caller knows
    /// whether this probe said something the last one did not, and writes the line then. See
    /// <see cref="PingOutcome"/>.
    /// </para>
    /// </summary>
    public static async Task<PingOutcome> PingAsync(string host, CancellationToken ct)
    {
        var target = host?.Trim() ?? string.Empty;

        if (target.Length == 0)
        {
            return new PingOutcome(MeterReading.Unavailable(Str.Shell_Ping_NoHost.Text()), "no host configured");
        }

        // The persisted value gets the same check the settings box applies, because a hand-edited
        // state file never went through the box. See HostAddress.
        if (!HostAddress.IsPlausible(target))
        {
            return new PingOutcome(Unreachable(target, Str.Shell_Ping_NotHost.Text()), $"refused to probe {target}: not a plausible host");
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(target, PingTimeout, cancellationToken: ct).ConfigureAwait(false);

            if (reply.Status != IPStatus.Success)
            {
                // A timeout or an unreachable host is the normal answer to a ping, not a fault. It
                // is a state the tile shows, and one log line when it starts.
                var timedOut = reply.Status == IPStatus.TimedOut;

                return new PingOutcome(
                    Unreachable(target, timedOut ? Str.Shell_Ping_TimedOut.Text() : Str.Shell_Ping_Unreachable.Text()),
                    $"{target}: {reply.Status}");
            }

            var ms = reply.RoundtripTime;

            return new PingOutcome(
                new MeterReading(Math.Clamp(ms / LatencyCeilingMs, 0, 1), Formatting.Latency(ms), true, Value: ms),
                null);
        }
        catch (OperationCanceledException)
        {
            // ignore: the tick that asked for this reading is gone; there is nobody to show it to.
            throw;
        }
        catch (PingException ex)
        {
            return new PingOutcome(Unreachable(target, Str.Shell_Ping_Unreachable.Text()), $"could not ping {target}: {ex.Message}");
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            // Name resolution failures arrive here rather than as a reply status.
            return new PingOutcome(Unreachable(target, Str.Shell_Ping_UnknownHost.Text()), $"could not resolve {target}: {ex.Message}");
        }
    }

    /// <summary>A failed probe, its reason naming the host: "--" over nothing does not say *what* timed out.</summary>
    private static MeterReading Unreachable(string target, string reason)
        => MeterReading.Unavailable($"{target} · {reason}");
}
