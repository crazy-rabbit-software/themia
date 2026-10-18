using System.Net.Http;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.App.Services.Accounts;

/// <summary>[S-9910] Signing in: one method per sign-in member, and the host's password forms, each verifying before anything is saved.</summary>
internal sealed partial class AccountService
{
    /// <summary>How long a browser sign-in may take before the wait is given up.</summary>
    private static readonly TimeSpan BrowserDeadline = TimeSpan.FromMinutes(5);

    // -----------------------------------------------------------------------------------------
    // The browser flow (OAuthPkceLoopback): Microsoft, Google, any provider that offers it
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-536] [S-9917] Why <paramref name="provider"/>'s browser sign-in is unavailable in this build,
    /// or null when it is available. A member whose secret is compiled in from the build
    /// (<c>THEMIA_CLIENT_SECRETS</c>, the provider's key) and a build without it refuses before the
    /// browser opens rather than at the exchange.
    /// </summary>
    public static string? UnavailableReason(ProviderId provider)
    {
        var module = ProviderCatalog.Of(provider);
        return module.SignIn.Find<OAuthPkceLoopback>() is { ClientSecret: ClientSecretRule.BuildTime } && SecretOf(module) is null
            ? Str.Shell_Accounts_SecretMissing.Format(("provider", module.Label.Text()))
            : null;
    }

    /// <summary>The provider's build-time secret, or null when the build holds none for it.</summary>
    private static string? SecretOf(IProviderModule module)
        => module.BuildSecret is { } key ? BuildSecrets.ClientSecret(key) : null;

    private static string? SecretOrThrow(IProviderModule module, ServiceAuth member)
    {
        if (member is not OAuthPkceLoopback { ClientSecret: ClientSecretRule.BuildTime })
        {
            return null;
        }

        return SecretOf(module) ?? throw new AccountException(AccountError.Refused, Str.Shell_Accounts_SecretMissing.Format(("provider", module.Label.Text())));
    }

    /// <summary>
    /// [S-1780] [S-9910] Sign in to a provider in the user's browser, as its browser member says: open
    /// its page with a PKCE challenge and a fresh state, wait for it to come back to the loopback
    /// ([S-527], [S-9913]), exchange the code with the verifier, prove who signed in ([S-535],
    /// [S-9916]), and save the account. One path for every provider; what differs is the member's data.
    /// </summary>
    /// <param name="openBrowser">Opens a URL in the system browser; returns false when it could not.</param>
    public async Task<AccountInfo> SignInWithBrowserAsync(ProviderId provider, Func<Uri, bool> openBrowser, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(openBrowser);

        var module = ProviderCatalog.Of(provider);
        var member = module.SignIn.Find<OAuthPkceLoopback>()
            ?? throw new ArgumentOutOfRangeException(nameof(provider), provider, "no browser sign-in");
        var secret = SecretOrThrow(module, member);

        using var loopback = new Loopback(member.Loopback.Ports);
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        var challenge = Pkce.Challenge(verifier);

        var redirect = member.Loopback.RedirectUri(loopback.Port);
        var authorize = OAuthRequests.AuthorizeUrl(member, redirect, state, challenge);

        if (!openBrowser(authorize))
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_BrowserNotOpened.Text());
        }

        string code;
        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
        {
            deadline.CancelAfter(BrowserDeadline);
            try
            {
                code = await loopback.WaitForCodeAsync(state, deadline.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
            {
                throw new AccountException(AccountError.TimedOut, Str.Shell_Accounts_BrowserTimeout.Text());
            }
        }

        // The redirect URI goes again, exactly as sent: providers compare it with the first one. A
        // public client gets no secret; the verifier is the proof.
        var exchange = RequestTemplate.Form(member.TokenUrl, OAuthRequests.Exchange(member, code, verifier, redirect, secret));
        var reply = await AccountHttp.SendAsync(exchange, CredentialKind.None, null, cancellation).ConfigureAwait(false);

        var answer = CodeExchange.Parse(reply.Status, reply.Body);

        if (answer.Grant is not { } grant)
        {
            throw new AccountException(AccountError.Refused,
                Str.Shell_Accounts_ProviderRefusedReason.Format(("provider", provider.Label()), ("reason", answer.Failure ?? $"HTTP {reply.Status}")), reply.Status);
        }

        var who = member.Identity is { } endpoint
            ? await WhoAsync(endpoint, grant.AccessToken, member.Scheme, provider, cancellation).ConfigureAwait(false)
            : throw new AccountException(AccountError.BadResponse, Str.Shell_Accounts_WhoUnknown.Format(("provider", provider.Label())));

        return await UpsertAsync(new Account
        {
            Id = who.Id,
            Provider = provider,
            Address = who.Address,
            DisplayName = who.Name,
            SealedAccessToken = Dpapi.Seal(grant.AccessToken),
            SealedRefreshToken = Dpapi.SealOrNull(grant.RefreshToken),
            ExpiresAt = TokenExpiry.FromExpiresIn(grant.ExpiresIn, Now),
            AddedAt = Now,
        }, cancellation).ConfigureAwait(false);
    }

    /// <summary>[S-535] [S-9916] Who a new token belongs to, read from the member's identity endpoint.</summary>
    private static async Task<ServiceIdentity> WhoAsync(IdentityEndpoint endpoint, string token, TokenScheme scheme, ProviderId provider, CancellationToken cancellation)
    {
        var request = new RequestTemplate(HttpMethod.Get, endpoint.Url);
        request.Headers.Add(("Accept", "application/json"));

        var reply = await AccountHttp.SendAsync(request, CredentialKind.Bearer, r => SetToken(r, scheme, token), cancellation)
            .ConfigureAwait(false);

        if (!reply.IsSuccess)
        {
            throw new AccountException(AccountError.BadResponse,
                Str.Shell_Accounts_WhoUnknownReason.Format(("provider", provider.Label()), ("reason", ErrorBody.Message(reply.Body) ?? $"HTTP {reply.Status}")), reply.Status);
        }

        return IdentityAnswers.Read(endpoint, reply.Body)
            ?? throw new AccountException(AccountError.BadResponse, Str.Shell_Accounts_WhoUnknown.Format(("provider", provider.Label())));
    }

    /// <summary>[S-9911] A token on a request, in its member's scheme.</summary>
    private static void SetToken(HttpRequestMessage request, TokenScheme scheme, string token)
    {
        if (scheme == TokenScheme.Raw)
        {
            request.Headers.TryAddWithoutValidation("Authorization", token);
        }
        else
        {
            request.Headers.Authorization = new("Bearer", token);
        }
    }

    // -----------------------------------------------------------------------------------------
    // The device code (OAuthDeviceCode) and the pasted token (PastedToken): kit services
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-9919] Sign in with a device code (RFC 8628): ask the provider for a code, hand it to
    /// <paramref name="show"/> (the page shows it and offers the provider's page), poll at the
    /// interval the provider names (five seconds more after every <c>slow_down</c>) until the user
    /// allows Themia, declines, or the code runs out; then prove who signed in ([S-9916]) and save.
    /// No secret exists in this flow.
    /// </summary>
    public async Task<AccountInfo> SignInWithDeviceCodeAsync(ProviderId provider, Action<DeviceCodeSession> show, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(show);

        var module = ProviderCatalog.Of(provider);
        var member = module.SignIn.Find<OAuthDeviceCode>()
            ?? throw new ArgumentOutOfRangeException(nameof(provider), provider, "no device-code sign-in");
        var descriptor = module.Service
            ?? throw new AccountException(AccountError.Refused, Str.Shell_Accounts_WhoUnknown.Format(("provider", provider.Label())));

        var start = await AccountHttp.SendAsync(RequestTemplate.Form(member.DeviceUrl, OAuthRequests.DeviceAuthorization(member)), CredentialKind.None, null, cancellation)
            .ConfigureAwait(false);

        var grant = DeviceGrant.Parse(start.Status, start.Body)
            ?? throw new AccountException(AccountError.Refused,
                Str.SignIn_DeviceUnavailable.Format(("provider", provider.Label()), ("reason", ErrorBody.Message(start.Body) ?? $"HTTP {start.Status}")), start.Status);

        var session = new DeviceCodeSession(grant.UserCode, grant.VerificationUri, grant.IntervalSeconds, grant.ExpiresInSeconds, Now);
        show(session);

        var step = new DeviceStep(DevicePoll.Pending, session.Interval, null);
        while (true)
        {
            // Server-directed: RFC 8628 says how long to wait, and the code's own lifetime ends it.
            await Task.Delay(step.Delay, cancellation).ConfigureAwait(false);

            var poll = await AccountHttp.SendAsync(RequestTemplate.Form(member.TokenUrl, OAuthRequests.DevicePoll(member, grant.DeviceCode)), CredentialKind.None, null, cancellation)
                .ConfigureAwait(false);

            var answer = CodeExchange.Parse(poll.Status, poll.Body);
            var outcome = DeviceCodeSession.Classify(ErrorBody.OAuthCode(poll.Body), answer.Grant is not null);
            step = session.Next(outcome, Now, ErrorBody.Message(poll.Body));

            if (outcome == DevicePoll.Success && answer.Grant is { } token)
            {
                var who = await ServiceWhoAsync(descriptor, token.AccessToken, member.Scheme, provider, cancellation).ConfigureAwait(false);
                return await UpsertAsync(new Account
                {
                    Id = AccountIds.Service(provider, who.Id),
                    Provider = provider,
                    Address = who.Address,
                    DisplayName = who.Name,
                    SealedAccessToken = Dpapi.Seal(token.AccessToken),
                    SealedRefreshToken = Dpapi.SealOrNull(token.RefreshToken),
                    ExpiresAt = token.ExpiresIn is null && token.RefreshToken is null ? null : TokenExpiry.FromExpiresIn(token.ExpiresIn, Now),
                    AddedAt = Now,
                    SignedInWith = SignInKind.DeviceCode,
                }, cancellation).ConfigureAwait(false);
            }

            if (step.Done)
            {
                throw new AccountException(outcome == DevicePoll.Expired ? AccountError.TimedOut : AccountError.Refused, step.Message ?? Str.Shell_Accounts_SignInFailedNoReason.Text(), poll.Status);
            }
        }
    }

    /// <summary>
    /// [S-9915] Add an account from a pasted token: its member's shape first, then the provider's
    /// identity request with it ([S-9916]); only a token that passes both is sealed and kept. It is
    /// never logged, and never renewed (<see cref="AccountAuth.Token"/>).
    /// </summary>
    public async Task<AccountInfo> AddPastedTokenAsync(ProviderId provider, string pasted, CancellationToken cancellation = default)
    {
        var module = ProviderCatalog.Of(provider);
        var member = module.SignIn.Find<PastedToken>()
            ?? throw new ArgumentOutOfRangeException(nameof(provider), provider, "no pasted-token sign-in");
        var descriptor = module.Service
            ?? throw new AccountException(AccountError.Refused, Str.Shell_Accounts_WhoUnknown.Format(("provider", provider.Label())));

        var token = member.Shape.Accept(pasted)
            ?? throw new AccountException(AccountError.Refused, Str.SignIn_TokenShape.Format(("provider", provider.Label())));

        var who = await ServiceWhoAsync(descriptor, token, member.Scheme, provider, cancellation).ConfigureAwait(false);

        return await UpsertAsync(new Account
        {
            Id = AccountIds.Service(provider, who.Id),
            Provider = provider,
            Address = who.Address,
            DisplayName = who.Name,
            SealedAccessToken = Dpapi.Seal(token),
            AddedAt = Now,
            SignedInWith = SignInKind.PastedToken,
        }, cancellation).ConfigureAwait(false);
    }

    /// <summary>[S-9916] Who a kit service's token belongs to: its descriptor's identity request, classified as every reply is.</summary>
    private static async Task<ServiceIdentity> ServiceWhoAsync(ServiceDescriptor descriptor, string token, TokenScheme scheme, ProviderId provider, CancellationToken cancellation)
    {
        var reply = await SendWithTokenAsync(descriptor, descriptor.Identity.Build(), token, scheme, cancellation).ConfigureAwait(false);

        var envelope = descriptor.Transport == TransportKind.GraphQL ? GraphQLEnvelope.Read(reply.Body.Span, string.Empty) : null;
        var verdict = ServiceFailures.Classify(descriptor, reply, Now, graphQL: envelope);
        if (!verdict.Ok)
        {
            throw new AccountException(verdict.Status == FetchStatus.SignInRequired ? AccountError.SignInRequired : AccountError.Refused,
                Str.Shell_Accounts_WhoUnknownReason.Format(("provider", provider.Label()), ("reason", FetchState.Message(verdict.Status))), reply.Status);
        }

        var body = envelope is { } graph ? graph.Data : reply.Body;
        return descriptor.Identity.Answer(body, out var problem)
            ?? throw new AccountException(AccountError.BadResponse,
                Str.Shell_Accounts_WhoUnknownReason.Format(("provider", provider.Label()), ("reason", problem ?? "no account named")));
    }

    // -----------------------------------------------------------------------------------------
    // IMAP, CalDAV, CardDAV: verified before saving
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Add a mail server after proving the login ([S-528]). <paramref name="address"/> may be empty:
    /// the user name is used when it is an address, else <c>user@host</c>, as the classic app did.
    /// </summary>
    public async Task<AccountInfo> AddImapAsync(
        string? address,
        string host,
        int port,
        string username,
        string password,
        string? nickname,
        CancellationToken cancellation = default)
    {
        var cleanHost = host?.Trim() ?? string.Empty;
        var cleanUser = username?.Trim() ?? string.Empty;

        if (cleanHost.Length == 0 || cleanHost.Contains("://", StringComparison.Ordinal) || cleanHost.Any(char.IsWhiteSpace)
            || Uri.CheckHostName(cleanHost) == UriHostNameType.Unknown)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_HostName.Text());
        }

        if (port is < 1 or > 65535)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_PortRange.Text());
        }

        if (cleanUser.Length == 0 || string.IsNullOrEmpty(password))
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_EnterCredentials.Text());
        }

        await ImapLogin.VerifyAsync(new ImapCredentials(cleanHost, port, cleanUser, password), cancellation).ConfigureAwait(false);

        var email = address?.Trim() is { Length: > 0 } typed
            ? typed
            : cleanUser.Contains('@', StringComparison.Ordinal) ? cleanUser : $"{cleanUser}@{cleanHost}";

        return await UpsertAsync(new Account
        {
            Id = AccountIds.Imap(cleanUser, cleanHost),
            Provider = HostProviders.Imap,
            Address = email,
            DisplayName = email,
            Nickname = AccountRules.CleanNickname(nickname),
            Host = cleanHost,
            Port = port,
            Username = cleanUser,
            SealedPassword = Dpapi.Seal(password),
            AddedAt = Now,
        }, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// Add a CalDAV or CardDAV account: resolve what was typed to a collection, proving the
    /// credentials on the way ([S-517]), and save the collection's address.
    /// </summary>
    public async Task<AccountInfo> AddDavAsync(
        ProviderId provider,
        string url,
        string username,
        string password,
        string? nickname,
        CancellationToken cancellation = default)
    {
        if (!(provider == HostProviders.CalDav || provider == HostProviders.CardDav))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "not a DAV provider");
        }

        var entered = DavUrls.FromTyped(url)
            ?? throw new AccountException(AccountError.Refused, "Enter the server's web address, such as https://cloud.example.com.");

        var user = username?.Trim() ?? string.Empty;
        if (user.Length == 0 || string.IsNullOrEmpty(password))
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Accounts_EnterCredentials.Text());
        }

        var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user}:{password}"));
        var kind = provider == HostProviders.CalDav ? DavKind.Calendar : DavKind.Contacts;

        // [S-3843] The server the user typed may be on their network (a NAS); discovery's redirects to
        // another host may not.
        var typedServer = Themia.Core.Net.LocalGrants.ForTyped(entered);

        var found = await DavDiscovery.DiscoverAsync(entered, kind, async (target, body, depth, token) =>
        {
            var reply = await AccountHttp.SendAsync(DavRequest("PROPFIND", target, body, depth), CredentialKind.Basic,
                r => r.Headers.Authorization = new("Basic", basic), token, typedServer).ConfigureAwait(false);
            return new DavReply(reply.Status, reply.Body, reply.FinalUrl);
        }, cancellation).ConfigureAwait(false);

        if (found is not { Outcome: DavDiscoveryOutcome.Found, Collection: { } collection })
        {
            throw new AccountException(found.Outcome switch
            {
                DavDiscoveryOutcome.AuthFailed => AccountError.SignInRequired,
                DavDiscoveryOutcome.NothingFound => AccountError.NotFound,
                _ => AccountError.BadResponse,
            }, found.Message, found.Status);
        }

        return await UpsertAsync(new Account
        {
            Id = AccountIds.Dav(provider, user, collection),
            Provider = provider,
            Address = user,
            DisplayName = user,
            Nickname = AccountRules.CleanNickname(nickname),
            Username = user,
            ServerUrl = collection.AbsoluteUri,
            SealedPassword = Dpapi.Seal(password),
            AddedAt = Now,
        }, cancellation).ConfigureAwait(false);
    }
}
