using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.App.Services.Accounts;

/// <summary>Tokens, and every authorized request. See the summary on <see cref="AccountService"/>.</summary>
internal sealed partial class AccountService
{
    /// <summary>A ceiling on pages followed in one list call, whatever the server keeps offering.</summary>
    private const int MaxPages = 50;

    // -----------------------------------------------------------------------------------------
    // Tokens
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-530] A bearer token for an OAuth account (Microsoft, Google): the stored
    /// one while it has more than five minutes to live, else a fresh one. Refresh is single-flight per
    /// account. A password account has no token and is refused.
    /// </summary>
    /// <exception cref="AccountException">
    /// <see cref="AccountError.SignInRequired"/> when only the user can mend it; a network error otherwise.
    /// </exception>
    public async Task<string> GetAccessTokenAsync(string accountId, CancellationToken cancellation = default)
        => (await TokenAsync(Require(accountId), cancellation).ConfigureAwait(false)).Token;

    /// <summary>The token and the sealed form it came from, so a 401 can say which token it rejected.</summary>
    private async Task<(string Token, string? Sealed)> TokenAsync(Account account, CancellationToken cancellation)
    {
        var auth = AccountRules.AuthOf(account);
        if (auth == AccountAuth.Password)
        {
            throw new AccountException(AccountError.Refused, $"{account.Label} signs in with a password, not a token.");
        }

        if (auth == AccountAuth.Token)
        {
            // [S-9919] A pasted token has nothing to renew: it works until the service says it does not.
            return (OpenOrThrow(account, account.SealedAccessToken, SecretKind.Token), account.SealedAccessToken);
        }

        if (TokenExpiry.IsUsable(account.ExpiresAt, Now)
            && Dpapi.Open(account.SealedAccessToken) is { State: SecretState.Open, Value: { } token })
        {
            return (token, account.SealedAccessToken);
        }

        return await RefreshAsync(account.Id, rejected: null, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Renew an account's access token under its own gate. Whoever waited at the gate re-reads the
    /// account first: if another caller refreshed meanwhile, that token is used and no second
    /// request is made. <paramref name="rejected"/> is the sealed token a server just answered 401
    /// to, which forces a refresh even before the stored expiry — but only once, however many
    /// callers saw that same 401.
    /// </summary>
    private async Task<(string Token, string? Sealed)> RefreshAsync(string accountId, string? rejected, CancellationToken cancellation)
    {
        var gate = _refreshGates.GetOrAdd(accountId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellation).ConfigureAwait(false);

        try
        {
            var account = Require(accountId);

            if (!RefreshRules.ShouldRefresh(account.ExpiresAt, account.SealedAccessToken, rejected, Now)
                && Dpapi.Open(account.SealedAccessToken) is { State: SecretState.Open, Value: { } held })
            {
                return (held, account.SealedAccessToken);
            }

            var refresh = Dpapi.Open(account.SealedRefreshToken);
            if (refresh is not { State: SecretState.Open, Value: { } refreshToken })
            {
                var problem = refresh.State == SecretState.Absent
                    ? Str.Shell_Accounts_SignInExpired.Text()
                    : SecretProblem(SecretKind.SignIn, refresh.State);
                MarkProblem(accountId, problem);
                throw new AccountException(AccountError.SignInRequired, problem);
            }

            var reply = await AccountHttp.SendAsync(RefreshRequest(account, refreshToken), CredentialKind.None, null, cancellation)
                .ConfigureAwait(false);

            var parsed = Parse(reply.Body, AccountWireContext.Default.TokenReply);

            if (!reply.IsSuccess || parsed?.AccessToken is not { Length: > 0 } accessToken)
            {
                throw RefreshFailed(account, reply, parsed);
            }

            var sealedAccess = Dpapi.Seal(accessToken);
            var sealedRefresh = parsed.RefreshToken is { Length: > 0 } rotated ? Dpapi.Seal(rotated) : account.SealedRefreshToken;
            var expires = TokenExpiry.FromExpiresIn(parsed.ExpiresIn, Now);

            await MutateAsync(list =>
            {
                // Only the token fields of the account as it is NOW: a rename that landed during the
                // round trip survives, and an account signed out meanwhile is not brought back.
                var index = list.FindIndex(a => a.Id == accountId);
                if (index < 0)
                {
                    throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_SignedOutWhileRenewing.Text());
                }

                list[index] = list[index] with
                {
                    SealedAccessToken = sealedAccess,
                    SealedRefreshToken = sealedRefresh,
                    ExpiresAt = expires,
                };
            }, userAction: false, cancellation).ConfigureAwait(false);

            ClearProblem(accountId);
            Log.Info(Scope, $"renewed the sign-in for {account}");
            return (accessToken, sealedAccess);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>[S-9910] The renewal of an account's sign-in, built from the member that signed it in.</summary>
    private static RequestTemplate RefreshRequest(Account account, string refreshToken)
    {
        var module = ProviderCatalog.Of(account.Provider);
        ServiceAuth member = (ServiceAuth?)module.SignIn.Find<OAuthPkceLoopback>() ?? module.SignIn.Find<OAuthDeviceCode>()
            ?? throw new AccountException(AccountError.Refused, $"{account.Provider.Label()} has no sign-in to renew.");
        var tokenUrl = member switch
        {
            OAuthPkceLoopback browser => browser.TokenUrl,
            OAuthDeviceCode device => device.TokenUrl,
            _ => throw new AccountException(AccountError.Refused, $"{account.Provider.Label()} has no sign-in to renew."),
        };

        return RequestTemplate.Form(tokenUrl, OAuthRequests.Refresh(member, refreshToken, SecretOrThrow(module, member)));
    }

    /// <summary>
    /// Why a refresh failed. A grant the provider no longer honours (revoked, expired, password
    /// changed) needs the user and marks the account; anything else — a 5xx, a proxy page — is
    /// transient and leaves the account alone. Neither ever removes it.
    /// </summary>
    private AccountException RefreshFailed(Account account, AccountReply reply, TokenReply? parsed)
    {
        var code = parsed?.Error ?? ErrorBody.OAuthCode(reply.Body);
        var said = parsed?.ErrorDescription is { Length: > 0 } description
            ? description.Split('\n')[0].Trim()
            : ErrorBody.Message(reply.Body);

        if (RefreshRules.NeedsUser(reply.Status, code))
        {
            var problem = said is null
                ? Str.Shell_Accounts_SignInAgain.Format(("provider", account.Provider.Label()))
                : Str.Shell_Accounts_SignInAgainReason.Format(("provider", account.Provider.Label()), ("reason", said));
            MarkProblem(account.Id, problem);
            return new AccountException(AccountError.SignInRequired, problem, reply.Status);
        }

        // A 2xx without a token, or a 401 with an error that is not about the grant, is the
        // endpoint misbehaving rather than the user's sign-in going bad.
        var error = AccountException.FromStatus(reply.Status);
        if (reply.IsSuccess || error == AccountError.SignInRequired)
        {
            error = AccountError.BadResponse;
        }

        return new AccountException(
            error,
            Str.Shell_Accounts_RenewFailed.Format(("provider", account.Provider.Label()), ("reason", said ?? $"HTTP {reply.Status}")),
            reply.Status);
    }

    // -----------------------------------------------------------------------------------------
    // Requests
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-531] Send any request as an account. The provider's proof is added (and re-added after a
    /// redirect only where it may go), a 401 on an OAuth account is answered with one refresh and one
    /// retry, and anything but a 2xx throws a typed <see cref="AccountException"/> carrying the
    /// provider's own message.
    /// </summary>
    /// <summary>
    /// [S-3843] The leave an account's own server earned when the user typed it: a CalDAV/CardDAV
    /// collection's host, an IMAP server. Microsoft and Google are public services and earn none.
    /// </summary>
    internal static Themia.Core.Net.LocalGrant? ServerGrant(Account account) => account.Provider switch
    {
        var pid when (pid == HostProviders.CalDav || pid == HostProviders.CardDav) && Uri.TryCreate(account.ServerUrl, UriKind.Absolute, out var server)
            => Themia.Core.Net.LocalGrants.ForTyped(server),
        var pid when pid == HostProviders.Imap => Themia.Core.Net.LocalGrants.ForTypedHost(account.Host),
        _ => null,
    };

    public async Task<AccountReply> SendAsync(string accountId, RequestTemplate request, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var account = Require(accountId);
        var (kind, token, sealedToken) = await ProofAsync(account, cancellation).ConfigureAwait(false);

        var reply = await AccountHttp.SendAsync(request, kind, r => Authorize(r, account, token), cancellation, ServerGrant(account)).ConfigureAwait(false);

        if (reply.Status == 401 && AccountRules.AuthOf(account) == AccountAuth.OAuthRefresh)
        {
            // Rejected before its stored expiry (revoked, rotated elsewhere): renew once and retry once.
            var (renewed, _) = await RefreshAsync(account.Id, sealedToken, cancellation).ConfigureAwait(false);
            reply = await AccountHttp.SendAsync(request, kind, r => Authorize(r, account, renewed), cancellation, ServerGrant(account)).ConfigureAwait(false);
        }

        // A 304 answers the caller's own conditional request: its copy is current. [S-625]
        if (reply.IsSuccess || reply.IsNotModified)
        {
            return reply;
        }

        var error = AccountException.FromStatus(reply.Status);
        var said = ErrorBody.Message(reply.Body);

        // [S-9920] A throttle is a throttle with a known end, whatever status carried it, and nothing
        // should ask again before that end. The host's own providers name no budget: Retry-After only.
        var budget = RateLimitRules.Read(RateLimitScheme.RetryAfterOnly, reply.Headers, Now, reply.Status);
        if (RateLimitRules.IsThrottle(RateLimitScheme.RetryAfterOnly, reply.Status, budget))
        {
            error = AccountError.RateLimited;
        }
        var message = said is null
            ? Str.Shell_Net_Answered.Format(("host", reply.FinalUrl.Host), ("status", $"HTTP {reply.Status}"))
            : Str.Shell_Net_AnsweredReason.Format(("host", reply.FinalUrl.Host), ("status", $"HTTP {reply.Status}"), ("reason", said));

        if (error == AccountError.SignInRequired)
        {
            MarkProblem(account.Id, AccountRules.AuthOf(account) == AccountAuth.Password
                ? Str.Shell_Accounts_PasswordRefused.Text()
                : Str.Shell_Accounts_ProviderRefused.Format(("provider", account.Provider.Label())));
        }

        throw new AccountException(error, message, reply.Status)
        {
            ProviderMessage = said,
            RetryAt = error == AccountError.RateLimited ? RateLimitRules.WaitUntil(RateLimitScheme.RetryAfterOnly, reply.Status, budget, Now) : null,
        };
    }

    /// <summary>An authorized GET; the body as text.</summary>
    public async Task<string> GetStringAsync(string accountId, Uri url, CancellationToken cancellation = default, params (string Name, string Value)[] headers)
        => (await SendAsync(accountId, Get(url, headers), cancellation).ConfigureAwait(false)).Body;

    /// <summary>An authorized GET of a JSON document, parsed through a source-generated contract.</summary>
    /// <exception cref="AccountException"><see cref="AccountError.BadResponse"/> when the body is not that shape.</exception>
    public async Task<T> GetJsonAsync<T>(
        string accountId,
        Uri url,
        JsonTypeInfo<T> contract,
        CancellationToken cancellation = default,
        params (string Name, string Value)[] headers)
    {
        var reply = await SendAsync(accountId, Get(url, headers), cancellation).ConfigureAwait(false);

        return Parse(reply.Body, contract)
            ?? throw new AccountException(AccountError.BadResponse, $"{url.Host} sent something that is not the expected document.", reply.Status);
    }

    /// <summary>
    /// A POST, PATCH, PUT or DELETE with an optional JSON body. Not retried on a throttle unless the
    /// method is repeatable, so a POST that created something never creates it twice.
    /// </summary>
    public Task<AccountReply> SendJsonAsync(string accountId, HttpMethod method, Uri url, string? json, CancellationToken cancellation = default)
    {
        var template = new RequestTemplate(method, url, json is null ? null : Encoding.UTF8.GetBytes(json), json is null ? null : "application/json");
        template.Headers.Add(("Accept", "application/json"));
        return SendAsync(accountId, template, cancellation);
    }

    /// <summary>
    /// [S-532] A Microsoft Graph collection: every <c>value</c> item across pages, following
    /// <c>@odata.nextLink</c>, up to <paramref name="maxItems"/>. A next link to any host other than
    /// the first page's is not followed (the token would go with it) and is logged.
    /// </summary>
    /// <param name="item">The contract for ONE item; register just the item type in your context.</param>
    /// <param name="headers">Extra headers, e.g. <c>("Prefer", "outlook.timezone=\"UTC\"")</c>.</param>
    public Task<IReadOnlyList<T>> GetGraphPagesAsync<T>(
        string accountId,
        Uri first,
        JsonTypeInfo<T> item,
        int maxItems,
        CancellationToken cancellation = default,
        params (string Name, string Value)[] headers)
        => PagesAsync(accountId, first, "value", item, maxItems, headers, cancellation, static (_, root) =>
            root.TryGetProperty("@odata.nextLink", out var next) && next.ValueKind == JsonValueKind.String
                && Uri.TryCreate(next.GetString(), UriKind.Absolute, out var url)
                    ? url
                    : null);

    /// <summary>
    /// [S-532] A Google API list: every item under <paramref name="itemsProperty"/> (<c>items</c>,
    /// <c>messages</c>, <c>connections</c>…) across pages, following <c>nextPageToken</c> by setting
    /// <c>pageToken</c> on the first URL, up to <paramref name="maxItems"/>.
    /// </summary>
    public Task<IReadOnlyList<T>> GetGooglePagesAsync<T>(
        string accountId,
        Uri first,
        string itemsProperty,
        JsonTypeInfo<T> item,
        int maxItems,
        CancellationToken cancellation = default)
        => PagesAsync(accountId, first, itemsProperty, item, maxItems, [], cancellation, static (firstUrl, root) =>
            root.TryGetProperty("nextPageToken", out var next) && next.ValueKind == JsonValueKind.String
                && next.GetString() is { Length: > 0 } pageToken
                    ? WithQuery(firstUrl, "pageToken", pageToken)
                    : null);

    /// <summary>
    /// [S-2683] A round of a provider's change list: every changed item and every removed id across
    /// pages (Graph <c>@odata.nextLink</c>, Google <c>nextPageToken</c> on <paramref name="first"/>),
    /// and the cursor the last page carries. The cursor is null when the round stopped early — at
    /// <paramref name="maxItems"/>, at the page ceiling, or at a next page on another host — since
    /// then the changes after the stop were never seen. <paramref name="first"/> is the start of a
    /// round or a stored delta link.
    /// </summary>
    public async Task<DeltaRound<T>> GetDeltaAsync<T>(
        string accountId,
        Uri first,
        DeltaDialect dialect,
        JsonTypeInfo<T> item,
        int maxItems,
        CancellationToken cancellation = default,
        params (string Name, string Value)[] headers)
    {
        var items = new List<T>();
        var removed = new List<string>();
        string? cursor = null;
        Uri? url = first;
        var page = 0;
        var stopped = false;

        for (; url is not null && page < MaxPages; page++)
        {
            var reply = await SendAsync(accountId, Get(url, headers), cancellation).ConfigureAwait(false);

            DeltaPage<T> read;
            try
            {
                read = DeltaPages.Read(reply.Body, dialect, item);
            }
            catch (JsonException ex)
            {
                throw new AccountException(AccountError.BadResponse, $"{url.Host} sent a change page that does not parse: {ex.Message}", reply.Status, ex);
            }

            items.AddRange(read.Items);
            removed.AddRange(read.Removed);
            cursor = read.Cursor;

            if (items.Count >= maxItems)
            {
                items.RemoveRange(maxItems, items.Count - maxItems);
                stopped = true;
                url = null;
                break;
            }

            Uri? next = dialect == DeltaDialect.Graph
                ? Uri.TryCreate(read.NextLink, UriKind.Absolute, out var link) ? link : null
                : read.NextPageToken is { } token ? WithQuery(first, "pageToken", token) : null;

            if (next is not null && !RedirectRules.IsSafeNextLink(first, next))
            {
                Log.Warn(Scope, $"not following a next change page on {next.Host}: the token stays on {first.Host}");
                next = null;
                stopped = true;
            }

            url = next;
        }

        // Stopped at the bound, the page ceiling or an unsafe next page: the rest was never read.
        var complete = url is null && !stopped;
        return new DeltaRound<T>(items, removed, complete ? cursor : null, complete);
    }

    /// <summary>
    /// [S-533] A WebDAV request as a CalDAV / CardDAV account: basic auth, the <c>Depth</c> header,
    /// redirects followed with the method and body intact. <paramref name="url"/> defaults to the
    /// account's collection. The reply's <see cref="AccountReply.FinalUrl"/> is the base for hrefs.
    /// </summary>
    public Task<AccountReply> DavAsync(
        string accountId,
        string method,
        Uri? url,
        string? xmlBody,
        int depth,
        CancellationToken cancellation = default)
    {
        var account = Require(accountId);
        var target = url
            ?? (Uri.TryCreate(account.ServerUrl, UriKind.Absolute, out var collection)
                ? collection
                : throw new AccountException(AccountError.Refused, $"{account.Label} has no DAV address."));

        return SendAsync(accountId, DavRequest(method, target, xmlBody, depth), cancellation);
    }

    /// <summary>
    /// [S-534] Where and as whom an IMAP account signs in, with the password opened. Hold it for one
    /// session, not longer.
    /// </summary>
    public ImapCredentials GetImapCredentials(string accountId)
    {
        var account = Require(accountId);

        if (account.Provider != HostProviders.Imap || account.Host is null || account.Port is not { } port || account.Username is null)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_NotMailServer.Format(("name", account.Label)));
        }

        return new ImapCredentials(account.Host, port, account.Username, OpenOrThrow(account, account.SealedPassword, SecretKind.Password));
    }

    /// <summary>Tell the service an IMAP server refused the stored password, so the account says so.</summary>
    public void ReportImapLoginRefused(string accountId)
        => MarkProblem(accountId, Str.Shell_Accounts_MailPasswordRefused.Text());

    // -----------------------------------------------------------------------------------------
    // Plumbing
    // -----------------------------------------------------------------------------------------

    private async Task<(CredentialKind Kind, string? Token, string? Sealed)> ProofAsync(Account account, CancellationToken cancellation)
    {
        if (account.Provider.Auth() == AccountAuth.Password)
        {
            return (CredentialKind.Basic, OpenOrThrow(account, account.SealedPassword, SecretKind.Password), account.SealedPassword);
        }

        var (token, sealedToken) = await TokenAsync(account, cancellation).ConfigureAwait(false);
        return (CredentialKind.Bearer, token, sealedToken);
    }

    /// <summary>The proof and the provider's own required headers, onto one outgoing request.</summary>
    private static void Authorize(HttpRequestMessage request, Account account, string? secret)
    {
        switch (account.Provider)
        {
            case var pid when pid == HostProviders.Imap || pid == HostProviders.CalDav || pid == HostProviders.CardDav:
                var pair = Encoding.UTF8.GetBytes($"{account.Username}:{secret}");
                request.Headers.Authorization = new("Basic", Convert.ToBase64String(pair));
                return;
        }

        SetToken(request, AccountRules.MemberOf(account)?.Scheme ?? TokenScheme.Bearer, secret ?? string.Empty);
    }

    private string OpenOrThrow(Account account, string? sealedSecret, SecretKind what)
    {
        var opened = Dpapi.Open(sealedSecret);
        if (opened is { State: SecretState.Open, Value: { } value })
        {
            return value;
        }

        var problem = SecretProblem(what, opened.State);
        MarkProblem(account.Id, problem);
        throw new AccountException(AccountError.SignInRequired, problem);
    }

    /// <summary>What a stored secret is, for the sentence that says why it cannot be used.</summary>
    private enum SecretKind
    {
        SignIn,
        Token,
        Password,
    }

    /// <summary>Why a stored secret cannot be used, one whole sentence per kind and state.</summary>
    private static string SecretProblem(SecretKind kind, SecretState state) => (state, kind) switch
    {
        (SecretState.Absent, SecretKind.SignIn) => Str.Shell_Secret_AbsentSignIn.Text(),
        (SecretState.Absent, SecretKind.Token) => Str.Shell_Secret_AbsentToken.Text(),
        (SecretState.Absent, _) => Str.Shell_Secret_AbsentPassword.Text(),
        (SecretState.Plaintext, SecretKind.SignIn) => Str.Shell_Secret_PlainSignIn.Text(),
        (SecretState.Plaintext, SecretKind.Token) => Str.Shell_Secret_PlainToken.Text(),
        (SecretState.Plaintext, _) => Str.Shell_Secret_PlainPassword.Text(),
        (_, SecretKind.SignIn) => Str.Shell_Secret_UnreadableSignIn.Text(),
        (_, SecretKind.Token) => Str.Shell_Secret_UnreadableToken.Text(),
        _ => Str.Shell_Secret_UnreadablePassword.Text(),
    };

    private async Task<IReadOnlyList<T>> PagesAsync<T>(
        string accountId,
        Uri first,
        string itemsProperty,
        JsonTypeInfo<T> item,
        int maxItems,
        (string Name, string Value)[] headers,
        CancellationToken cancellation,
        Func<Uri, JsonElement, Uri?> nextPage)
    {
        var items = new List<T>();
        Uri? url = first;

        for (var page = 0; url is not null && page < MaxPages && items.Count < maxItems; page++)
        {
            var reply = await SendAsync(accountId, Get(url, headers), cancellation).ConfigureAwait(false);

            Uri? next;
            try
            {
                using var document = JsonDocument.Parse(reply.Body);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new AccountException(AccountError.BadResponse, $"{url.Host} sent a page that is not an object.", reply.Status);
                }

                if (root.TryGetProperty(itemsProperty, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in array.EnumerateArray())
                    {
                        if (element.Deserialize(item) is { } value)
                        {
                            items.Add(value);
                        }

                        if (items.Count >= maxItems)
                        {
                            break;
                        }
                    }
                }

                next = nextPage(first, root);
            }
            catch (JsonException ex)
            {
                throw new AccountException(AccountError.BadResponse, $"{url.Host} sent a page that does not parse: {ex.Message}", reply.Status, ex);
            }

            if (next is not null && !RedirectRules.IsSafeNextLink(first, next))
            {
                Log.Warn(Scope, $"not following a next page on {next.Host}: the token stays on {first.Host}");
                next = null;
            }

            url = next;
        }

        return items;
    }

    private static RequestTemplate Get(Uri url, (string Name, string Value)[] headers)
    {
        var template = new RequestTemplate(HttpMethod.Get, url);
        template.Headers.AddRange(headers);

        if (!headers.Any(h => string.Equals(h.Name, "Accept", StringComparison.OrdinalIgnoreCase)))
        {
            template.Headers.Add(("Accept", "application/json"));
        }

        return template;
    }

    private static RequestTemplate DavRequest(string method, Uri url, string? xmlBody, int depth)
    {
        var template = new RequestTemplate(
            new HttpMethod(method),
            url,
            xmlBody is null ? null : Encoding.UTF8.GetBytes(xmlBody),
            xmlBody is null ? null : "application/xml; charset=utf-8");

        template.Headers.Add(("Depth", depth.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return template;
    }

    private static Uri WithQuery(Uri url, string key, string value)
    {
        var builder = new UriBuilder(url);
        var pairs = builder.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith(key + "=", StringComparison.Ordinal))
            .Append($"{key}={Uri.EscapeDataString(value)}");
        builder.Query = string.Join('&', pairs);
        return builder.Uri;
    }

    private static T? Parse<T>(string body, JsonTypeInfo<T> contract)
    {
        try
        {
            return JsonSerializer.Deserialize(body, contract);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
