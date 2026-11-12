using Themia.Core.SystemData;

namespace Themia.Core.Widgets;

/// <summary>[S-1173] [S-11000] A ping atom's config: the host it probes (empty: the default host).</summary>
public interface IPingHost
{
    string Host { get; set; }
}

/// <summary>[S-2508] [S-11000] The ping atoms' one rule for the host they probe and its series, so the reading and the history chart one prober.</summary>
public static class PingHosts
{
    /// <summary>The host actually probed: never empty, so the widget always has something to show.</summary>
    public static string Effective(IPingHost ping)
    {
        ArgumentNullException.ThrowIfNull(ping);
        return string.IsNullOrWhiteSpace(ping.Host) ? HostAddress.DefaultPingHost : ping.Host.Trim();
    }

    /// <summary>[S-2513] Probes of its host and their history.</summary>
    public static SystemInterests Wants(IPingHost ping) => SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, Effective(ping)));
}
