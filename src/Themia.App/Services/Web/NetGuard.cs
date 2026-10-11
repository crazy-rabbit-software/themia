using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Net;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>What the guard made of one address: allowed, or the status and sentence to refuse it with.</summary>
internal readonly record struct GuardResult(bool Allowed, FetchStatus Status, string? Reason)
{
    public static GuardResult Pass { get; } = new(true, FetchStatus.Ok, null);
}

/// <summary>
/// [S-1301]/[S-960] A connection the guard refused. Raised by the one connect path
/// (<see cref="NetGuard.OpenSocketAsync"/>, the HTTP connect callback and the per-request check), so
/// every caller can tell "the guard said no, and here is the sentence" from a network failure — the
/// HTTP stack hands it back as the inner exception of an <see cref="HttpRequestException"/>.
/// </summary>
internal sealed class NetRefusedException : IOException
{
    public NetRefusedException(string host, string reason)
        : base(reason)
    {
        Host = host;
    }

    public NetRefusedException()
        : this(string.Empty, "refused by the network guard")
    {
    }

    public NetRefusedException(string message, Exception inner)
        : base(message, inner)
    {
        Host = string.Empty;
    }

    public string Host { get; }

    /// <summary>The refusal anywhere in <paramref name="error"/>'s chain of inner exceptions, or null.</summary>
    public static NetRefusedException? Find(Exception? error)
    {
        for (var e = error; e is not null; e = e.InnerException)
        {
            if (e is NetRefusedException refused)
            {
                return refused;
            }
        }

        return null;
    }
}

/// <summary>
/// [S-1301]/[S-960] The app side of <see cref="NetworkGuard"/>, and the one place this app opens an
/// outbound connection: it resolves the name, judges every address, and connects only to the
/// addresses it judged — so the address that was checked is the address that is connected to, and a
/// name that re-resolves to 127.0.0.1 between a check and a connect (DNS rebinding) has nothing to
/// slip through. It also asks the system proxy. The local network is reached only through a
/// <see cref="LocalGrant"/> the caller hands in for an address the user typed ([S-3840]).
/// <para>
/// Every HTTP client the app makes comes from <c>GuardedHttp</c> (web, feeds, favicons, weather,
/// quotes, accounts, Graph, Google, DAV), and every raw mail connection
/// from <c>GuardedTcp</c>; both connect through <see cref="OpenSocketAsync"/>. The camera stream
/// is guarded too ([S-3840]): a LAN camera is reached through the address typed for it.
/// </para>
/// </summary>
internal static class NetGuard
{
    private const string Scope = "netguard";

    /// <summary>
    /// [S-963] The destination check for a request whose connection the app does not make to the
    /// destination itself: through a proxy (classic NG-07 — an IP literal, or a name that resolves
    /// here, is checked; a name only the proxy can resolve is the proxy's call).
    /// </summary>
    /// <param name="grant">[S-3840] The request's leave for its own typed LAN host, or null.</param>
    public static async Task<GuardResult> CheckDestinationAsync(Uri uri, bool proxied, LocalGrant? grant, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new GuardResult(false, FetchStatus.Refused, Str.Shell_Net_HttpOnly.Text());
        }

        var host = uri.IdnHost;
        IReadOnlyList<IPAddress>? resolved = null;

        if (!NetworkGuard.TryLiteral(host, out _))
        {
            try
            {
                resolved = await Dns.GetHostAddressesAsync(host, token).ConfigureAwait(false);
            }
            catch (SocketException ex)
            {
                // Not resolvable here. Whether that refuses the request depends on the proxy.
                Log.Debug(Scope, $"could not resolve {host}: {ex.SocketErrorCode}");
            }
            catch (ArgumentException ex)
            {
                Log.Debug(Scope, $"not a host name: {ex.Message}");
            }
        }

        var (verdict, culprit) = NetworkGuard.Judge(host, resolved, proxied, grant);

        switch (verdict)
        {
            case GuardVerdict.Blocked:
                Log.Info(Scope, $"refused {host}: it resolves to {culprit}");
                return new GuardResult(false, FetchStatus.Refused, NetworkGuard.Explain(verdict, host));

            case GuardVerdict.Unresolvable:
                return new GuardResult(false, FetchStatus.Unreachable, NetworkGuard.Explain(verdict, host));

            default:
                return GuardResult.Pass;
        }
    }

    /// <summary>
    /// [S-962] Open a TCP connection to <paramref name="host"/>:<paramref name="port"/> directly:
    /// resolve once, judge every address for <paramref name="reach"/>, and connect only to those
    /// addresses. A name that does not resolve throws the resolver's <see cref="SocketException"/>
    /// (HostNotFound), as a plain connect would; a refused one throws <see cref="NetRefusedException"/>.
    /// </summary>
    /// <param name="grant">[S-3840] The connection's leave for its own typed LAN host, or null.</param>
    public static async Task<Socket> OpenSocketAsync(string host, int port, NetReach reach, LocalGrant? grant, CancellationToken token)
    {
        var bare = (host ?? string.Empty).Trim().TrimStart('[').TrimEnd(']');

        IPAddress[] addresses = NetworkGuard.TryLiteral(bare, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(bare, token).ConfigureAwait(false);

        var (verdict, culprit) = NetworkGuard.Judge(bare, addresses, proxied: false, grant, reach);

        if (verdict == GuardVerdict.Unresolvable)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        if (verdict == GuardVerdict.Blocked)
        {
            Log.Info(Scope, $"refused a connection to {bare}: it resolves to {culprit}");
            throw new NetRefusedException(bare, reach == NetReach.ThisMachine
                ? $"{bare} is not this computer"
                : NetworkGuard.Explain(verdict, bare));
        }

        return await ConnectAsync(port, token, socket => socket.ConnectAsync(addresses, port, token)).ConfigureAwait(false);
    }

    /// <summary>
    /// [PX-07] Open a TCP connection to the system proxy. The proxy is infrastructure, not a
    /// destination — corporate proxies live on 10.x — so its address is never judged; the
    /// destination behind it is checked with <see cref="CheckDestinationAsync"/> instead.
    /// </summary>
    public static Task<Socket> OpenProxySocketAsync(string host, int port, CancellationToken token)
        => ConnectAsync(port, token, socket => socket.ConnectAsync(new DnsEndPoint(host, port), token));

    /// <summary>
    /// [S-963] The proxy Windows would send <paramref name="uri"/> through, or null for direct. Read
    /// from <see cref="HttpClient.DefaultProxy"/> — the current user's Windows proxy settings,
    /// automatic (WPAD, setup script) and manual, with the bypass list — the same answer the HTTP
    /// stack uses. Any failure to read it is direct (classic PX-05: a setting that cannot be read
    /// must not fail a request).
    /// </summary>
    public static Uri? ProxyFor(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        try
        {
            var proxy = HttpClient.DefaultProxy;

            if (proxy.IsBypassed(uri))
            {
                return null;
            }

            var via = proxy.GetProxy(uri);
            return via is null || via == uri ? null : via;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or PlatformNotSupportedException)
        {
            Log.Debug(Scope, $"could not ask the system proxy: {ex.Message}");
            return null;
        }
    }

    private static async Task<Socket> ConnectAsync(int port, CancellationToken token, Func<Socket, ValueTask> connect)
    {
        if (port is < 1 or > 65535)
        {
            throw new NetRefusedException(string.Empty, $"port {port} is not a port");
        }

        token.ThrowIfCancellationRequested();

        // Dual-mode, so one socket reaches whichever family the name resolved to.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await connect(socket).ConfigureAwait(false);
            return socket;
        }
        catch (Exception)
        {
            // The socket is ours until it is handed back; a failed connect must not leak it.
            socket.Dispose();
            throw;
        }
    }
}
