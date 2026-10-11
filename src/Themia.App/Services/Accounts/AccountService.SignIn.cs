using System.Net.Http;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.App.Services.Accounts;

/// <summary>Signing in: one method pair per provider, each verifying before anything is saved.</summary>
internal sealed partial class AccountService
{
    /// <summary>How long a browser sign-in may take before the wait is given up.</summary>
    private static readonly TimeSpan BrowserDeadline = TimeSpan.FromMinutes(5);

    // -----------------------------------------------------------------------------------------
    // Microsoft and Google: browser, loopback, PKCE
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-536] Why Google sign-in is unavailable in this build, or null when it is available. The
    /// Desktop client's secret is compiled in from <c>THEMIA_GOOGLE_CLIENT_SECRET</c> and never kept
    /// in source; a build without it refuses before the browser opens rather than at the exchange.
    /// </summary>
    public static string? GoogleUnavailableReason => BuildSecrets.GoogleClientSecret.Length > 0
        ? null
        : Str.Shell_Accounts_GoogleMissing.Text();

    private static string GoogleSecretOrThrow()
        => GoogleUnavailableReason is { } reason
            ? throw new AccountException(AccountError.Refused, reason)
            : BuildSecrets.GoogleClientSecret;

    /// <summary>
    /// [S-1780] Sign in to Microsoft or Google in the user's browser: open the provider's page with a
    /// PKCE challenge and a fresh state, wait for it to come back to the loopback port ([S-527]),
    /// exchange the code with the verifier, prove who signed in ([S-535]), and save the account. One
    /// path for both; they differ only in the addresses, Google's client secret and the "who" call.
    /// </summary>
    /// <param name="openBrowser">Opens a URL in the system browser; returns false when it could not.</param>
    public async Task<AccountInfo> SignInWithBrowserAsync(AccountProvider provider, Func<Uri, bool> openBrowser, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(openBrowser);

        if (provider is not (AccountProvider.Microsoft or AccountProvider.Google))
        {
            throw new ArgumentOutOfRangeException(nameof(provider), provider, "no browser sign-in");
        }

        var microsoft = provider == AccountProvider.Microsoft;
        var secret = microsoft ? null : GoogleSecretOrThrow();

        using var loopback = new Loopback();
        var verifier = Pkce.CreateVerifier();
        var state = Pkce.CreateState();
        var challenge = Pkce.Challenge(verifier);

        var redirect = microsoft
            ? OAuthClients.Microsoft.RedirectUri(loopback.Port)
            : OAuthClients.Google.RedirectUri(loopback.Port);

        var authorize = microsoft
            ? OAuthClients.Microsoft.AuthorizeUrl(redirect, state, challenge)
            : OAuthClients.Google.AuthorizeUrl(redirect, state, challenge);

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

        // The redirect URI goes again, exactly as sent: both providers compare it with the first one.
        // Microsoft is a public client and gets no secret; the verifier is the proof.
        var exchange = microsoft
            ? RequestTemplate.Form(OAuthClients.Microsoft.TokenUrl,
            [
                new("grant_type", "authorization_code"),
                new("code", code),
                new("client_id", OAuthClients.Microsoft.ClientId),
                new("code_verifier", verifier),
                new("redirect_uri", redirect),
                new("scope", OAuthClients.Microsoft.Scope),
            ])
            : RequestTemplate.Form(OAuthClients.Google.TokenUrl,
            [
                new("grant_type", "authorization_code"),
                new("code", code),
                new("client_id", OAuthClients.Google.ClientId),
                new("client_secret", secret!),
                new("code_verifier", verifier),
                new("redirect_uri", redirect),
            ]);

        var reply = await AccountHttp.SendAsync(exchange, CredentialKind.None, null, cancellation).ConfigureAwait(false);

        var answer = CodeExchange.Parse(reply.Status, reply.Body);

        if (answer.Grant is not { } grant)
        {
            throw new AccountException(AccountError.Refused,
                Str.Shell_Accounts_ProviderRefusedReason.Format(("provider", provider.Label()), ("reason", answer.Failure ?? $"HTTP {reply.Status}")), reply.Status);
        }

        return microsoft
            ? await SaveMicrosoftAsync(grant, cancellation).ConfigureAwait(false)
            : await SaveGoogleAsync(grant, cancellation).ConfigureAwait(false);
    }

    /// <summary>Graph <c>/me</c> names the account; the Graph user id is its id.</summary>
    private async Task<AccountInfo> SaveMicrosoftAsync(CodeGrant grant, CancellationToken cancellation)
    {
        var me = await GetAnonymousJsonAsync(OAuthClients.Microsoft.MeUrl, grant.AccessToken, AccountWireContext.Default.GraphMe, "Microsoft", cancellation)
            .ConfigureAwait(false);

        if (me.Id is not { Length: > 0 } id)
        {
            throw new AccountException(AccountError.BadResponse, Str.Shell_Accounts_WhoUnknown.Format(("provider", "Microsoft")));
        }

        var address = me.Mail is { Length: > 0 } mail ? mail : me.UserPrincipalName ?? id;

        return await UpsertAsync(new Account
        {
            Id = AccountIds.Microsoft(id),
            Provider = AccountProvider.Microsoft,
            Address = address,
            DisplayName = me.DisplayName ?? address,
            SealedAccessToken = Dpapi.Seal(grant.AccessToken),
            SealedRefreshToken = Dpapi.SealOrNull(grant.RefreshToken),
            ExpiresAt = TokenExpiry.FromExpiresIn(grant.ExpiresIn, Now),
            AddedAt = Now,
        }, cancellation).ConfigureAwait(false);
    }

    /// <summary>OpenID userinfo names the account; the subject is its id.</summary>
    private async Task<AccountInfo> SaveGoogleAsync(CodeGrant grant, CancellationToken cancellation)
    {
        var me = await GetAnonymousJsonAsync(OAuthClients.Google.UserInfoUrl, grant.AccessToken, AccountWireContext.Default.GoogleUserInfo, "Google", cancellation)
            .ConfigureAwait(false);

        if (me.Subject is not { Length: > 0 } subject)
        {
            throw new AccountException(AccountError.BadResponse, Str.Shell_Accounts_WhoUnknown.Format(("provider", "Google")));
        }

        var address = me.Email ?? subject;

        return await UpsertAsync(new Account
        {
            Id = AccountIds.Google(subject),
            Provider = AccountProvider.Google,
            Address = address,
            DisplayName = me.Name ?? address,
            SealedAccessToken = Dpapi.Seal(grant.AccessToken),
            SealedRefreshToken = Dpapi.SealOrNull(grant.RefreshToken),
            ExpiresAt = TokenExpiry.FromExpiresIn(grant.ExpiresIn, Now),
            AddedAt = Now,
        }, cancellation).ConfigureAwait(false);
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
            Provider = AccountProvider.Imap,
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
        AccountProvider provider,
        string url,
        string username,
        string password,
        string? nickname,
        CancellationToken cancellation = default)
    {
        if (provider is not (AccountProvider.CalDav or AccountProvider.CardDav))
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
        var kind = provider == AccountProvider.CalDav ? DavKind.Calendar : DavKind.Contacts;

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

    // -----------------------------------------------------------------------------------------

    /// <summary>A GET with a token that is not (yet) an account's: the "who signed in" call.</summary>
    private static async Task<T> GetAnonymousJsonAsync<T>(Uri url, string token, JsonTypeInfo<T> contract, string provider, CancellationToken cancellation)
        where T : class
    {
        var request = new RequestTemplate(HttpMethod.Get, url);
        request.Headers.Add(("Accept", "application/json"));

        var reply = await AccountHttp.SendAsync(request, CredentialKind.Bearer, r => r.Headers.Authorization = new("Bearer", token), cancellation)
            .ConfigureAwait(false);

        return reply.IsSuccess && Parse(reply.Body, contract) is { } value
            ? value
            : throw new AccountException(AccountError.BadResponse,
                Str.Shell_Accounts_WhoUnknownReason.Format(("provider", provider), ("reason", ErrorBody.Message(reply.Body) ?? $"HTTP {reply.Status}")), reply.Status);
    }
}
