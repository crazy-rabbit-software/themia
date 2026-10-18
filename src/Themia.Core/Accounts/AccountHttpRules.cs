using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Themia.Core.Widgets;

namespace Themia.Core.Accounts;

/// <summary>[S-9901] Recording an account failure on a schedule.</summary>
public static class AccountFailures
{
    /// <summary>
    /// The schedule's <see cref="Sync.SyncSchedule.Failed"/> with the failure's own state and, for a
    /// throttle, the provider's own retry time: nothing asks again before <see cref="AccountException.RetryAt"/>.
    /// </summary>
    public static void Failed(this Sync.SyncSchedule schedule, AccountException failure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(failure);
        schedule.Failed(failure.ToFetchStatus(), failure.Message, now, failure.RetryAt);
    }
}

/// <summary>[S-518] Retrying a throttled request: how often, and how long to wait.</summary>
public static class RetryRules
{
    /// <summary>At most this many retries after the first attempt.</summary>
    public const int MaxRetries = 3;

    /// <summary>
    /// The longest any one wait may be, whatever the server asks. A garbage or hostile
    /// <c>Retry-After</c> must not hang a widget's refresh for an hour.
    /// </summary>
    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether a response is worth asking again. Only 429 and 503 — the two statuses that mean "not
    /// now" rather than "no" — and only for a request that can be repeated without doing anything
    /// twice.
    /// </summary>
    public static bool ShouldRetry(int status, int retriesSoFar, bool idempotent)
        => idempotent && retriesSoFar < MaxRetries && status is 429 or 503;

    /// <summary>
    /// How long to wait before retry number <paramref name="retriesSoFar"/> + 1: what the server said,
    /// else 1 s, 2 s, 4 s; never more than <see cref="MaxWait"/>.
    /// </summary>
    public static TimeSpan Delay(int retriesSoFar, TimeSpan? retryAfter)
    {
        var wait = retryAfter ?? TimeSpan.FromSeconds(1 << Math.Clamp(retriesSoFar, 0, 5));
        return wait < TimeSpan.Zero ? TimeSpan.Zero : wait > MaxWait ? MaxWait : wait;
    }

    /// <summary>
    /// A <c>Retry-After</c> header: delta seconds (what Graph and Google send) or an HTTP date. Null
    /// when absent or unreadable, so the caller falls back to its own backoff.
    /// </summary>
    public static TimeSpan? ParseRetryAfter(string? header, DateTimeOffset now) => RetryAfterHeader.Parse(header, now);
}

/// <summary>How a request proves who it is, for deciding where that proof may follow a redirect.</summary>
public enum CredentialKind
{
    None,

    /// <summary>A bearer token: a key to the whole account.</summary>
    Bearer,

    /// <summary>A DAV user name and password.</summary>
    Basic,
}

/// <summary>
/// [S-519] Following redirects by hand, because the framework's own following drops the
/// <c>Authorization</c> header and turns a PROPFIND into something else.
/// </summary>
public static class RedirectRules
{
    public const int MaxHops = 5;

    public static bool IsRedirect(int status) => status is 301 or 302 or 303 or 307 or 308;

    /// <summary>
    /// Whether to follow at all: only to http(s), never from https down to http, and never from a
    /// public host to this machine or the local network — a redirect is the server's word, and a
    /// server must not be able to point this app's credentials at a router's admin page.
    /// </summary>
    public static bool MayFollow(Uri from, Uri to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (to.Scheme != Uri.UriSchemeHttps && to.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        if (from.Scheme == Uri.UriSchemeHttps && to.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        return IsLocal(from) || !IsLocal(to);
    }

    /// <summary>
    /// Whether the credentials go along. Same origin: always. A bearer token never leaves its origin —
    /// OneDrive's download redirect is to a pre-signed URL that needs none, and a token sent there
    /// would be a token handed to a storage host. A DAV password may follow an https-to-https hop to
    /// another host, because that is how iCloud and others shard their DAV service, and refusing it
    /// would make discovery fail for them.
    /// </summary>
    public static bool MayCarryCredentials(Uri from, Uri to, CredentialKind kind)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (SameOrigin(from, to))
        {
            return true;
        }

        return kind == CredentialKind.Basic && from.Scheme == Uri.UriSchemeHttps && to.Scheme == Uri.UriSchemeHttps;
    }

    /// <summary>
    /// Whether a paging link may be followed with the same token: https, and the same host the first
    /// page came from. A <c>@odata.nextLink</c> is data from the network, and the token goes with it.
    /// </summary>
    public static bool IsSafeNextLink(Uri first, Uri next)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(next);
        return next.IsAbsoluteUri && next.Scheme == Uri.UriSchemeHttps && SameOrigin(first, next);
    }

    public static bool SameOrigin(Uri a, Uri b)
        => string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase)
            && a.Port == b.Port;

    /// <summary>
    /// [S-966] A literal address the network guard classes as local (loopback, private, link-local,
    /// reserved — <see cref="NetworkGuard.IsBlocked"/>, the one table of address classes), or a name
    /// that says it is local ([S-3841]: <c>localhost</c>, <c>nas</c>, <c>nas.local</c>).
    /// </summary>
    public static bool IsLocal(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsLoopback || Net.LocalGrants.IsLocalName(uri.IdnHost))
        {
            return true;
        }

        return NetworkGuard.TryLiteral(uri.IdnHost, out var address) && NetworkGuard.IsBlocked(address);
    }
}

/// <summary>[S-520] The provider's own words out of an error body, where it gave any.</summary>
public static class ErrorBody
{
    /// <summary>
    /// The message in the shapes these providers use: <c>{"error":{"message":…}}</c> (Graph, Google,
    /// Anthropic), <c>{"error":"…","error_description":"…"}</c> (OAuth token endpoints) and
    /// <c>{"message":…}</c> (GitHub). Null when the body is not one of them. Cut to one line and a
    /// readable length, since it ends up in a settings page.
    /// </summary>
    public static string? Message(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? found = null;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var nested) && nested.ValueKind == JsonValueKind.String)
                {
                    found = nested.GetString();
                }
                else if (error.ValueKind == JsonValueKind.String)
                {
                    found = root.TryGetProperty("error_description", out var description) && description.ValueKind == JsonValueKind.String
                        ? description.GetString()
                        : error.GetString();
                }
            }

            if (found is null && root.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
            {
                found = message.GetString();
            }

            return Tidy(found);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The OAuth <c>error</c> code of a token-endpoint body (<c>invalid_grant</c> and friends).</summary>
    public static string? OAuthCode(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.String
                    ? error.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Tidy(string? text)
    {
        var line = text?.Split('\n')[0].Trim();
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        return line.Length <= 300 ? line : line[..300] + "…";
    }
}
