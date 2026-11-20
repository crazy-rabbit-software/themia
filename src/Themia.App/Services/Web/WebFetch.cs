using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.App.Services.Accounts;
using Themia.App.Services.Net;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Net;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>What one request came back as: a named status, the body if there is one, and why.</summary>
/// <param name="Status">The named outcome. <see cref="FetchStatus.Ok"/> means <paramref name="Body"/> is usable.</param>
/// <param name="Body">The response text. Empty unless the status is Ok.</param>
/// <param name="Detail">One line for the log: an HTTP status line, a socket error, a timeout.</param>
internal readonly record struct WebResponse(FetchStatus Status, string Body, string? Detail);

/// <summary>
/// The one HTTP client the whole app shares.
/// <para>
/// One, not one per widget and certainly not one per request. A fresh <see cref="HttpClient"/> per
/// call is the classic way to exhaust the machine's ephemeral ports: each one opens its own
/// connection and leaves the socket in TIME_WAIT for a couple of minutes afterwards, so a desktop
/// with four network widgets refreshing on a timer will, given a few days, stop being able to open
/// a socket at all — and the symptom is every application on the machine failing, not this one.
/// </para>
/// <para>
/// Everything a public API is entitled to expect is set here once: a real user agent rather than
/// the framework default (several of these hosts answer 403 to a blank one), compression, a
/// connect timeout well short of the request timeout, and a pooled connection lifetime so a
/// long-lived process still notices DNS moving.
/// </para>
/// </summary>
internal static class WebFetch
{
    private const string Scope = "web";

    /// <summary>
    /// [S-960] [S-967] The web's policy over the one send: the clients for everything on the internet (every
    /// connection through the guard, the local network only for the host of an address the user typed, [S-3840]),
    /// loopback only for a service on this computer (the hardware monitor, never proxied). A widget refreshes on a
    /// timer, so the honest deadline is short (20 s: a request that has not answered by then has nothing the next tick
    /// will not get sooner), and a dead host is unreachable in seconds (8 s to connect). Feeds are a few hundred
    /// kilobytes and a ten-year chart under a megabyte: past 4 MB the reply is not the document asked for.
    /// </summary>
    private static readonly HttpPolicy Policy = new(
        GuardedHttp.CreatePair(TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan),
        GuardedHttp.Create(NetReach.ThisMachine, TimeSpan.FromSeconds(8), Timeout.InfiniteTimeSpan),
        Deadline: TimeSpan.FromSeconds(20),
        MaxBytes: 4 * 1024 * 1024);

    /// <summary>
    /// Fetch a URL as text, mapping every way it can go wrong onto a named
    /// <see cref="FetchStatus"/>.
    /// <para>
    /// This never throws for a network reason. A widget's refresh path has nowhere to put an
    /// exception except a crash, and "the wifi is off" is a state of a laptop rather than a fault.
    /// Cancellation is the one thing that does propagate, because a cancelled request is one whose
    /// answer nobody is waiting for.
    /// </para>
    /// </summary>
    /// <param name="reach">
    /// [S-960] Where the address may go. Every request is guarded — each hop, the first and every
    /// redirect, connects through <see cref="NetGuard"/> — and a refusal comes back as
    /// <see cref="FetchStatus.Refused"/> with the sentence to show as the detail.
    /// </param>
    /// <param name="grant">
    /// [S-3840] For an address the user typed: <see cref="LocalGrants.ForTyped(Uri)"/> of it, which lets
    /// that one host be on the local network. Never for an address read out of fetched content.
    /// </param>
    public static async Task<WebResponse> GetAsync(Uri uri, string accept, CancellationToken token, NetReach reach = NetReach.Internet, LocalGrant? grant = null)
    {
        var (status, body, detail) = await FetchAsync(uri, accept, reach, grant, null, token).ConfigureAwait(false);

        // UTF-8 with replacement, not with an exception: a feed declaring one encoding and serving
        // another is common, the app builds with InvariantGlobalization so most legacy code pages
        // are not available to decode it with anyway, and a replacement character in one headline
        // is a far better outcome than the whole widget reporting a failure.
        return status == FetchStatus.Ok && body is not null
            ? new WebResponse(FetchStatus.Ok, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(body), null)
            : new WebResponse(status, string.Empty, detail);
    }

    /// <summary>
    /// Fetch a small binary resource (an icon), capped at <paramref name="maxBytes"/>. Same
    /// statuses and the same guard as <see cref="GetAsync"/>.
    /// </summary>
    public static Task<(FetchStatus Status, byte[]? Body, string? Detail)> GetBytesAsync(
        Uri uri,
        string accept,
        int maxBytes,
        CancellationToken token)
        => FetchAsync(uri, accept, NetReach.Internet, grant: null, maxBytes, token);

    private static async Task<(FetchStatus Status, byte[]? Body, string? Detail)> FetchAsync(
        Uri uri,
        string accept,
        NetReach reach,
        LocalGrant? grant,
        int? maxBytes,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(uri);

        var request = new RequestTemplate(HttpMethod.Get, uri);
        if (!string.IsNullOrEmpty(accept))
        {
            request.Headers.Add(("Accept", accept));
        }

        try
        {
            var answer = await HttpCore.SendAsync(Policy, request, CredentialKind.None, null, grant, token, reach, maxBytes).ConfigureAwait(false);

            if (answer.Status == 429)
            {
                // Its own status, because it is the one failure whose correct response is to stop
                // asking for a long time rather than to try again on the next tick.
                return (FetchStatus.RateLimited, null, "HTTP 429 Too Many Requests");
            }

            if (!answer.IsSuccess)
            {
                // The status line, not the body: a 403 challenge page or a 404 HTML document would
                // otherwise be handed to the parser and reported as "not a feed", which sends the
                // reader looking in the wrong place.
                return (FetchStatus.BadResponse, null, $"HTTP {answer.Status}");
            }

            return (FetchStatus.Ok, answer.Body, null);
        }
        catch (AccountException ex)
        {
            // The one classifier's verdict: a guard's or a redirect's refusal with its sentence, no network, a
            // timeout, a host that did not answer, a body over the cap.
            if (ex.Error == AccountError.Refused)
            {
                Log.Debug(Scope, $"refused: {ex.Message}");
            }

            return (ex.ToFetchStatus(), null, ex.Message);
        }
    }

    /// <summary>
    /// Fetch and deserialize through a source-generated contract.
    /// <para>
    /// The <see cref="JsonTypeInfo{T}"/> parameter is what keeps this AOT-safe: there is no
    /// overload here that takes a <see cref="Type"/> and looks it up by reflection, because that
    /// overload is trimmed away in the published build and fails at runtime rather than at build.
    /// </para>
    /// </summary>
    public static async Task<(FetchStatus Status, T? Value, string? Detail)> GetJsonAsync<T>(
        Uri uri,
        JsonTypeInfo<T> contract,
        CancellationToken token)
        where T : class
    {
        var response = await GetAsync(uri, "application/json", token).ConfigureAwait(false);

        if (response.Status != FetchStatus.Ok)
        {
            return (response.Status, null, response.Detail);
        }

        try
        {
            var value = JsonSerializer.Deserialize(response.Body, contract);

            // "null" is valid JSON and a perfectly good reply; it is not a payload.
            return value is null
                ? (FetchStatus.BadResponse, null, "the reply held no object")
                : (FetchStatus.Ok, value, null);
        }
        catch (JsonException ex)
        {
            // The service answered with something that is not its own schema — an error document,
            // an HTML interstitial, a truncated body. Named separately from "did not answer".
            return (FetchStatus.BadResponse, null, ex.Message);
        }
    }
}
