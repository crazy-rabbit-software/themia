using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Themia.App.Services.Net;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Net;

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

    /// <summary>The body as it came, for a caller that reads bytes (a kit service's reply).</summary>
    public byte[] Raw { get; init; } = [];
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
/// [S-526] The accounts' policy over the one send (<see cref="HttpCore"/>): sign-in endpoints, token refresh, Graph,
/// Google and DAV. A 30-second deadline per hop and 16 MB at most; the guarded clients reach a server on this machine
/// or the local network exactly when the user typed that server's address ([S-3843]). A DAV server's digest challenge
/// is not answered; basic auth is what the classic app sent.
/// <para>
/// A 429 or 503 on a repeatable request is asked again, waiting exactly as long as the server's
/// <c>Retry-After</c> says (capped), at most three times — server-directed, not a guess. Every
/// failure leaves here as a typed <see cref="AccountException"/>; nothing returns a half answer.
/// </para>
/// </summary>
internal static class AccountHttp
{
    private const string Scope = "accounts.http";

    /// <summary>[S-960]/[S-965] Every connection guarded; the deadline is per hop, so the clients never time out on their own.</summary>
    private static readonly HttpPolicy Policy = new(
        GuardedHttp.CreatePair(TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan),
        Local: null,
        Deadline: TimeSpan.FromSeconds(30),
        MaxBytes: 16 * 1024 * 1024);

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

        var retries = 0;

        while (true)
        {
            var answer = await HttpCore.SendAsync(Policy, template, credentials, authorize, grant, cancellation).ConfigureAwait(false);

            if (!retryThrottles || !RetryRules.ShouldRetry(answer.Status, retries, template.IsIdempotent))
            {
                return Reply(answer);
            }

            var wait = RetryRules.Delay(retries, answer.RetryAfter);
            retries++;
            Log.Info(Scope, $"{template.Url.Host} answered {answer.Status}; asking again in {wait.TotalSeconds:F0}s ({retries}/{RetryRules.MaxRetries})");
            await Task.Delay(wait, cancellation).ConfigureAwait(false);
        }
    }

    private static AccountReply Reply(HttpAnswer answer)
        => new(answer.Status, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(answer.Body), answer.FinalUrl, answer.ContentType, answer.ETag)
        {
            Headers = answer.Headers,
            Raw = answer.Body,
        };
}
