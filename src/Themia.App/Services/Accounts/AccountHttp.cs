using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Themia.App.Services.Net;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Net;
using Themia.Core.Widgets;

namespace Themia.App.Services.Accounts;

/// <summary>What an account request came back with. Only ever built for a 2xx answer by the service.</summary>
/// <param name="Status">The HTTP status (207 for a DAV multistatus).</param>
/// <param name="Body">The response text, UTF-8 decoded.</param>
/// <param name="FinalUrl">Where the answer actually came from, after redirects — the base for DAV hrefs.</param>
/// <param name="ContentType">The media type, when the server named one.</param>
/// <param name="ETag">The entity tag, for a caller that writes back conditionally.</param>
internal sealed record AccountReply(int Status, string Body, Uri FinalUrl, string? ContentType, string? ETag)
{
    private static readonly IReadOnlyDictionary<string, string> NoHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsSuccess => Status is >= 200 and < 300;

    /// <summary>
    /// [S-625] 304: the caller's own <c>If-None-Match</c> / <c>If-Modified-Since</c> matched and the
    /// copy it holds is current. Only ever the answer to a conditional request the caller made.
    /// </summary>
    public bool IsNotModified => Status == 304;

    /// <summary>
    /// The response headers, keys compared case-insensitively, repeated headers joined with
    /// <c>", "</c> — for a caller that paces itself on them (<c>X-Poll-Interval</c>,
    /// <c>Last-Modified</c>, rate-limit budgets).
    /// </summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = NoHeaders;
}

/// <summary>
/// A request as it will be sent, kept so it can be sent again: after a retry, after a redirect, after
/// a token refresh. An <see cref="HttpRequestMessage"/> cannot be sent twice, so this holds what is
/// needed to build a fresh one each time — the body buffered once.
/// </summary>
internal sealed class RequestTemplate
{
    public RequestTemplate(HttpMethod method, Uri url, byte[]? body = null, string? contentType = null)
    {
        Method = method;
        Url = url;
        Body = body;
        ContentType = contentType;
    }

    public HttpMethod Method { get; }

    public Uri Url { get; }

    public byte[]? Body { get; }

    public string? ContentType { get; }

    /// <summary>Extra headers the caller wants (Accept, Prefer, Depth, a provider's own).</summary>
    public List<(string Name, string Value)> Headers { get; } = [];

    /// <summary>[S-9905] An entity tag the caller holds: the server answers 304 when it still matches.</summary>
    public string? IfNoneMatch { get; set; }

    /// <summary>A form post, the shape every token endpoint takes.</summary>
    public static RequestTemplate Form(Uri url, IEnumerable<KeyValuePair<string, string>> fields)
    {
        var body = string.Join('&', fields.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));
        var template = new RequestTemplate(HttpMethod.Post, url, Encoding.UTF8.GetBytes(body), "application/x-www-form-urlencoded");
        template.Headers.Add(("Accept", "application/json"));
        return template;
    }

    /// <summary>
    /// Whether sending it twice does nothing twice. Only these are retried on a throttle; a POST that
    /// created a task must not create two.
    /// </summary>
    public bool IsIdempotent => Method == HttpMethod.Get || Method == HttpMethod.Head || Method == HttpMethod.Delete
        || Method == HttpMethod.Put || Method.Method is "PROPFIND" or "REPORT";

    internal HttpRequestMessage Build(Uri url)
    {
        var request = new HttpRequestMessage(Method, url);

        if (Body is not null)
        {
            request.Content = new ByteArrayContent(Body);
            if (ContentType is not null)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ContentType);
            }
        }

        if (IfNoneMatch is { } tag && EntityTagHeaderValue.TryParse(tag, out var entity))
        {
            request.Headers.IfNoneMatch.Add(entity);
        }

        foreach (var (name, value) in Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return request;
    }
}

/// <summary>
/// [S-526] The one HTTP path every account request takes: sign-in endpoints, token refresh, Graph,
/// Google and DAV.
/// <para>
/// One client for all of it (pooled connections, a real user agent, a connect timeout well short of
/// the request deadline), and redirects followed by hand: the framework's own following drops the
/// <c>Authorization</c> header and turns a PROPFIND into a GET, which broke DAV discovery in the
/// classic app until it did the same. Credentials follow a redirect only where
/// <see cref="RedirectRules"/> says they may.
/// </para>
/// <para>
/// A 429 or 503 on a repeatable request is asked again, waiting exactly as long as the server's
/// <c>Retry-After</c> says (capped), at most three times — server-directed, not a guess. Every
/// failure leaves here as a typed <see cref="AccountException"/>; nothing returns a half answer.
/// </para>
/// </summary>
internal static class AccountHttp
{
    private const string Scope = "accounts.http";

    /// <summary>The whole-request deadline. Applied per request, so a slow page cannot borrow from the next.</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>What is read into memory at most. A mailbox page or a calendar REPORT is far smaller.</summary>
    private const int MaxBytes = 16 * 1024 * 1024;

    /// <summary>
    /// [S-960]/[S-965] The guarded client: every connection — Microsoft, Google, and the DAV
    /// servers a user typed in — resolves, is judged and connects through
    /// <see cref="Web.NetGuard"/>, so a server on this machine or the local network (a NAS) is reached
    /// exactly when the user typed that server's address ([S-3843]). The deadline is per request (a
    /// linked token), so the client itself never times a request out on its own schedule. A DAV
    /// server's digest challenge is not answered; basic auth is what the classic app sent.
    /// </summary>
    private static readonly GuardedClients Clients = GuardedHttp.CreatePair(TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

    /// <summary>
    /// Send a request, following redirects and retrying throttles, and return whatever status it
    /// ended on. Throws only for a transport failure (no network, timeout, refused redirect).
    /// </summary>
    /// <param name="template">The request.</param>
    /// <param name="credentials">What kind of proof <paramref name="authorize"/> adds, for the redirect rule.</param>
    /// <param name="authorize">Adds the proof to a request; called again for every send.</param>
    /// <param name="grant">[S-3843] The leave the account's typed server earned, or null.</param>
    /// <param name="retryThrottles">
    /// False for a kit service's request ([S-9920]): its bucket's block is the one retry rule, so a
    /// throttle comes back at once with its headers instead of being waited out here.
    /// </param>
    public static async Task<AccountReply> SendAsync(
        RequestTemplate template,
        CredentialKind credentials,
        Action<HttpRequestMessage>? authorize,
        CancellationToken cancellation,
        LocalGrant? grant = null,
        bool retryThrottles = true)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Url.Scheme != Uri.UriSchemeHttps && template.Url.Scheme != Uri.UriSchemeHttp)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Net_RefusedScheme.Format(("scheme", template.Url.Scheme)));
        }

        var retries = 0;

        while (true)
        {
            var reply = await FollowAsync(template, credentials, authorize, grant, cancellation).ConfigureAwait(false);

            if (!retryThrottles || !RetryRules.ShouldRetry(reply.Reply.Status, retries, template.IsIdempotent))
            {
                return reply.Reply;
            }

            var wait = RetryRules.Delay(retries, reply.RetryAfter);
            retries++;
            Log.Info(Scope, $"{template.Url.Host} answered {reply.Reply.Status}; asking again in {wait.TotalSeconds:F0}s ({retries}/{RetryRules.MaxRetries})");
            await Task.Delay(wait, cancellation).ConfigureAwait(false);
        }
    }

    private static async Task<(AccountReply Reply, TimeSpan? RetryAfter)> FollowAsync(
        RequestTemplate template,
        CredentialKind credentials,
        Action<HttpRequestMessage>? authorize,
        LocalGrant? grant,
        CancellationToken cancellation)
    {
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
            deadline.CancelAfter(RequestTimeout);

            try
            {
                using var response = await Clients
                    .SendAsync(request, grant, HttpCompletionOption.ResponseHeadersRead, deadline.Token)
                    .ConfigureAwait(false);

                var status = (int)response.StatusCode;

                if (RedirectRules.IsRedirect(status) && response.Headers.Location is { } location)
                {
                    var next = location.IsAbsoluteUri ? location : new Uri(url, location);

                    if (!RedirectRules.MayFollow(url, next))
                    {
                        throw new AccountException(AccountError.Refused,
                            $"{url.Host} redirected to an address Themia will not follow ({next.Scheme}://{next.Host}).", status);
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

                var body = await ReadAsync(response, deadline.Token).ConfigureAwait(false);
                var retryAfter = response.Headers.TryGetValues("Retry-After", out var values)
                    ? RetryRules.ParseRetryAfter(values.FirstOrDefault(), DateTimeOffset.UtcNow)
                    : null;

                return (new AccountReply(
                    status,
                    body,
                    url,
                    response.Content.Headers.ContentType?.MediaType,
                    response.Headers.ETag?.Tag)
                {
                    Headers = HeadersOf(response),
                }, retryAfter);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException ex)
            {
                throw new AccountException(AccountError.TimedOut, $"{url.Host} did not answer within {RequestTimeout.TotalSeconds:F0} seconds.", 0, ex);
            }
            catch (HttpRequestException ex)
            {
                throw Transport(url, ex);
            }
            catch (IOException ex)
            {
                throw new AccountException(AccountError.Unreachable, Str.Shell_Net_Dropped.Format(("host", url.Host)), 0, ex);
            }
        }

        throw new AccountException(AccountError.BadResponse, $"{template.Url.Host} redirected more than {RedirectRules.MaxHops} times.");
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

    private static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellation)
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

            if (buffer.Length + read > MaxBytes)
            {
                throw new AccountException(AccountError.BadResponse, Str.Shell_Net_TooLarge.Format(("size", Formatting.Bytes(MaxBytes))));
            }

            buffer.Write(chunk, 0, read);
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false)
            .GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static AccountException Transport(Uri url, HttpRequestException ex)
    {
        if (Web.NetRefusedException.Find(ex) is { } refused)
        {
            // The guard said no; its sentence says why.
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
}
