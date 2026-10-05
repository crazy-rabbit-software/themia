using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Net;
using Themia.Core.Widgets;

namespace Themia.App.Services.Net;

/// <summary>
/// [S-960] The one way this app builds an HTTP client. <c>WebFetch</c> (feeds, favicons, weather,
/// quotes, the photo of the day, the public address, the hardware monitor) and <c>AccountHttp</c>
/// (sign-in, token refresh, Graph, Google, DAV) each hold a client from
/// here, so every request they make connects through the same guarded callback, carries the same
/// user agent and meets the same TLS floor.
/// <para>
/// Redirects are never followed by the stack: both callers follow them by hand, so each hop is a new
/// request that goes through <see cref="GuardedHandler"/> and, on a new connection, the callback —
/// a public address answering 302 to <c>http://192.168.1.1/</c> meets the guard like any other.
/// </para>
/// </summary>
internal static class GuardedHttp
{
    /// <summary>
    /// [S-968] One honest, contactable identity for every outbound request (classic PX-10), so one
    /// address never sees two different clients depending on which code path asked.
    /// </summary>
    public const string UserAgent = "Themia/1.0 (Windows desktop widgets; +https://themia.app)";

    /// <summary>
    /// [S-968] TLS 1.2 or newer, for HTTP and for mail alike. Windows 11 already negotiates nothing
    /// older by default; naming the floor keeps a machine with old protocols re-enabled from
    /// handing a token or a password to a downgraded connection.
    /// </summary>
    public const SslProtocols TlsFloor = SslProtocols.Tls12 | SslProtocols.Tls13;

    /// <summary>A client whose every connection is guarded for <paramref name="reach"/>.</summary>
    /// <param name="reach">Where the client may go. <see cref="NetReach.ThisMachine"/> is never proxied.</param>
    /// <param name="connectTimeout">How long a dead host may take to be called unreachable.</param>
    /// <param name="requestTimeout">The client's own deadline (infinite when the caller sets one per request).</param>
    public static HttpClient Create(NetReach reach, TimeSpan connectTimeout, TimeSpan requestTimeout)
    {
        var sockets = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = connectTimeout,

            // A process that lives for weeks would otherwise pin a connection to an IP address that
            // DNS stopped pointing at days ago. A new connection is a new guarded resolve.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

            AllowAutoRedirect = false,
            PreAuthenticate = false,

            // The Windows proxy settings (HttpClient.DefaultProxy) for the internet; a loopback
            // service is never sent to a proxy.
            UseProxy = reach == NetReach.Internet,

            SslOptions = new SslClientAuthenticationOptions { EnabledSslProtocols = TlsFloor },
            ConnectCallback = (context, token) => ConnectAsync(context, reach, token),
        };

        var client = new HttpClient(new GuardedHandler(reach, sockets)) { Timeout = requestTimeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    /// <summary>
    /// [S-962]/[S-963] Every connection the HTTP stack opens. To the request's own host: resolved,
    /// judged and connected by <see cref="NetGuard.OpenSocketAsync"/>, so the checked address is the
    /// connected one. To a proxy: the proxy is not judged (PX-07), the destination behind it is.
    /// </summary>
    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, NetReach reach, CancellationToken token)
    {
        var endpoint = context.DnsEndPoint;
        var destination = context.InitialRequestMessage.RequestUri;

        if (reach == NetReach.Internet
            && destination is not null
            && ConnectRules.IsProxyHop(destination.IdnHost, destination.Port, endpoint.Host, endpoint.Port))
        {
            var verdict = await NetGuard.CheckDestinationAsync(destination, proxied: true, token).ConfigureAwait(false);
            if (!verdict.Allowed)
            {
                throw new NetRefusedException(destination.IdnHost, verdict.Reason ?? Str.Shell_Net_HostRefused.Format(("host", destination.IdnHost)));
            }

            var proxy = await NetGuard.OpenProxySocketAsync(endpoint.Host, endpoint.Port, token).ConfigureAwait(false);
            return new NetworkStream(proxy, ownsSocket: true);
        }

        var socket = await NetGuard.OpenSocketAsync(endpoint.Host, endpoint.Port, reach, token).ConfigureAwait(false);
        return new NetworkStream(socket, ownsSocket: true);
    }
}

/// <summary>
/// [S-963] The per-request half of the guard. A proxied plain-http request rides the proxy's pooled
/// connection, which was opened (and judged) for some earlier destination, so the connect callback is
/// not where its destination can be checked: it is checked here, on every request, before the stack
/// sees it. Also the last word on the scheme — only http and https leave this app.
/// </summary>
internal sealed class GuardedHandler(NetReach reach, HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var uri = request.RequestUri;

        if (uri is null || !uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            var reason = Str.Shell_Net_HttpOnly.Text();
            throw new HttpRequestException(reason, new NetRefusedException(uri?.ToString() ?? string.Empty, reason));
        }

        if (reach == NetReach.Internet && NetGuard.ProxyFor(uri) is not null)
        {
            var verdict = await NetGuard.CheckDestinationAsync(uri, proxied: true, cancellationToken).ConfigureAwait(false);
            if (!verdict.Allowed)
            {
                var reason = verdict.Reason ?? Str.Shell_Net_HostRefused.Format(("host", uri.IdnHost));
                throw new HttpRequestException(reason, new NetRefusedException(uri.IdnHost, reason));
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
