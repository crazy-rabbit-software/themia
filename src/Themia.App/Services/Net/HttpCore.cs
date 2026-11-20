using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Net;
using Themia.Core.Widgets;

namespace Themia.App.Services.Net;

/// <summary>What one request ended on, whatever its status: the body as read, where it came from after redirects, and its headers.</summary>
/// <param name="Status">The HTTP status of the last hop.</param>
/// <param name="Body">The body, read to the policy's cap.</param>
/// <param name="FinalUrl">Where the answer came from after redirects: the base for DAV hrefs.</param>
/// <param name="ContentType">The media type, when the server named one.</param>
/// <param name="ETag">The entity tag, when the server sent one.</param>
/// <param name="RetryAfter">The server's <c>Retry-After</c>, read as a wait from now; null when absent or unreadable.</param>
internal sealed record HttpAnswer(int Status, byte[] Body, Uri FinalUrl, string? ContentType, string? ETag, TimeSpan? RetryAfter)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>Every response and content header by case-insensitive name, repeats joined with <c>", "</c>.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// How one caller's requests go out: its guarded clients (one pair per caller, [S-3840]), the deadline of each hop and
/// the most it reads into memory. The web's and the accounts' are two of these, over one <see cref="HttpCore"/>.
/// </summary>
/// <param name="Clients">The caller's two guarded clients, for the internet.</param>
/// <param name="Local">[S-967] The loopback-only client, for a service on this computer; null when the caller has none.</param>
/// <param name="Deadline">Each hop's whole-request deadline, so a slow page cannot borrow from the next.</param>
/// <param name="MaxBytes">The ceiling on a body, applied while reading, never to a <c>Content-Length</c> a host may lie about.</param>
internal sealed record HttpPolicy(GuardedClients Clients, HttpClient? Local, TimeSpan Deadline, int MaxBytes);

/// <summary>
/// [S-526] [S-966] The one send of the app's HTTP: the scheme checked, every hop a new request through the guard
/// (redirects followed by hand: the framework's own following drops <c>Authorization</c>, turns a PROPFIND into a GET,
/// and would let a public host's 302 into the local network past a check made on the first address only), which hops
/// are followed and where credentials may go by <see cref="RedirectRules"/>, the body read to the policy's cap, the
/// server's <c>Retry-After</c> read, and every transport failure classified once into a typed
/// <see cref="AccountException"/> whose sentence says what happened. It never retries and never sleeps: a throttle
/// comes back at once, with its time, for the caller's schedule to wait out ([S-2694]).
/// <para>Cancellation by the caller propagates; the deadline's is a timeout.</para>
/// </summary>
internal static class HttpCore
{
    /// <summary>
    /// Send <paramref name="template"/> and return whatever status it ended on. Throws an <see cref="AccountException"/>
    /// only for a transport failure: no network, a timeout, a refused address or redirect, a body over the cap.
    /// </summary>
    /// <param name="credentials">What kind of proof <paramref name="authorize"/> adds, for the redirect rule.</param>
    /// <param name="authorize">Adds the proof to a request; called again for every hop it may follow to.</param>
    /// <param name="grant">[S-3840] The leave a typed local address earned, or null.</param>
    /// <param name="reach">Where the address may go; <see cref="NetReach.ThisMachine"/> takes the policy's loopback client.</param>
    /// <param name="maxBytes">A cap below the policy's (an icon), or null.</param>
    public static async Task<HttpAnswer> SendAsync(
        HttpPolicy policy,
        RequestTemplate template,
        CredentialKind credentials,
        Action<HttpRequestMessage>? authorize,
        LocalGrant? grant,
        CancellationToken cancellation,
        NetReach reach = NetReach.Internet,
        int? maxBytes = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(template);

        // The last line of defence for the trust boundary: every caller builds its address from checked pieces, but
        // this is the one place every request goes through.
        if (template.Url.Scheme != Uri.UriSchemeHttps && template.Url.Scheme != Uri.UriSchemeHttp)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Net_RefusedScheme.Format(("scheme", template.Url.Scheme)));
        }

        var cap = Math.Clamp(maxBytes ?? policy.MaxBytes, 1, policy.MaxBytes);
        var url = template.Url;
        var carry = true;

        for (var hop = 0; hop <= RedirectRules.MaxHops; hop++)
        {
            using var request = template.Build(url);
            if (carry)
            {
                authorize?.Invoke(request);
            }

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            deadline.CancelAfter(policy.Deadline);

            try
            {
                using var response = await (reach == NetReach.ThisMachine && policy.Local is { } local
                        ? local.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                        : policy.Clients.SendAsync(request, grant, HttpCompletionOption.ResponseHeadersRead, deadline.Token))
                    .ConfigureAwait(false);

                var status = (int)response.StatusCode;

                if (RedirectRules.IsRedirect(status) && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(url, location);

                    if (!RedirectRules.MayFollow(url, next))
                    {
                        throw new AccountException(AccountError.Refused,
                            $"{url.Host} redirected to an address Themia will not follow ({next.Scheme}://{next.IdnHost}).", status);
                    }

                    // A 303 means "go and GET that"; the others keep the method and body.
                    if (status == 303 && template.Method != HttpMethod.Get)
                    {
                        throw new AccountException(AccountError.BadResponse, $"{url.Host} answered a {template.Method} with 303.", status);
                    }

                    carry = carry && RedirectRules.MayCarryCredentials(url, next, credentials);
                    url = next;
                    continue;
                }

                var body = await ReadAsync(response, cap, deadline.Token).ConfigureAwait(false);
                var retryAfter = response.Headers.TryGetValues("Retry-After", out var values)
                    ? RetryRules.ParseRetryAfter(values.FirstOrDefault(), DateTimeOffset.UtcNow)
                    : null;

                return new HttpAnswer(status, body, url, response.Content.Headers.ContentType?.MediaType, response.Headers.ETag?.Tag, retryAfter)
                {
                    Headers = HeadersOf(response),
                };
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                throw new AccountException(AccountError.TimedOut, $"{url.Host} did not answer within {policy.Deadline.TotalSeconds:F0} seconds.", 0, ex);
            }
            catch (HttpRequestException ex)
            {
                throw Classify(url, ex);
            }
            catch (IOException ex)
            {
                // A connection dropped mid-body.
                throw new AccountException(AccountError.Unreachable, Str.Shell_Net_Dropped.Format(("host", url.Host)), 0, ex);
            }
        }

        throw new AccountException(AccountError.BadResponse, $"{template.Url.Host} redirected more than {RedirectRules.MaxHops} times.");
    }

    /// <summary>
    /// The one classifier of a transport failure: the guard's refusal (its sentence says why), no network at all, a TLS
    /// failure, a host not found, a network down, a socket timeout, anything else unreachable. "Offline" is the laptop's
    /// state and the widget is blameless; "unreachable" means the network is up and that host is not.
    /// </summary>
    public static AccountException Classify(Uri url, HttpRequestException ex)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(ex);

        if (Web.NetRefusedException.Find(ex) is { } refused)
        {
            return new AccountException(AccountError.Refused, refused.Message, 0, ex);
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return new AccountException(AccountError.Offline, Str.Shell_Net_Offline.Text(), 0, ex);
        }

        if (ex.InnerException is AuthenticationException)
        {
            return new AccountException(AccountError.Unreachable, Str.Shell_Net_TlsFailed.Format(("host", url.Host)), 0, ex);
        }

        if (ex.InnerException is SocketException socket)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                    => new AccountException(AccountError.Unreachable, $"{url.Host} could not be found.", 0, ex),
                SocketError.NetworkDown or SocketError.NetworkUnreachable
                    => new AccountException(AccountError.Offline, Str.Shell_Net_Offline.Text(), 0, ex),
                SocketError.TimedOut
                    => new AccountException(AccountError.TimedOut, $"{url.Host} did not answer.", 0, ex),
                _ => new AccountException(AccountError.Unreachable, Str.Shell_Net_CouldNotConnect.Format(("host", url.Host)), 0, ex),
            };
        }

        return new AccountException(AccountError.Unreachable, Str.Shell_Net_CouldNotReach.Format(("host", url.Host)), 0, ex);
    }

    /// <summary>Every response and content header, by case-insensitive name, repeats joined.</summary>
    private static Dictionary<string, string> HeadersOf(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, values) in response.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        foreach (var (name, values) in response.Content.Headers.NonValidated)
        {
            headers[name] = string.Join(", ", values);
        }

        return headers;
    }

    /// <summary>
    /// Read the body, refusing to grow past <paramref name="maxBytes"/>: bounded while reading, so a host that streams
    /// forever is stopped at the cap, not at the deadline with all it sent in memory.
    /// </summary>
    private static async Task<byte[]> ReadAsync(HttpResponseMessage response, int maxBytes, CancellationToken cancellation)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];

        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellation).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > maxBytes)
            {
                throw new AccountException(AccountError.BadResponse, Str.Shell_Net_TooLarge.Format(("size", Formatting.Bytes(maxBytes))));
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }
}
