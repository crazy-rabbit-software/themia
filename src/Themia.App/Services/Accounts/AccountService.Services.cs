using System.Net.Http;
using System.Text;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-9903] The one way a kit service's request is sent: its descriptor's pinned headers, the
/// account's token in its sign-in member's scheme (or none, anonymously), the account's own API root
/// where the descriptor allows one, no throttle retries of its own (the bucket's block is the one
/// retry rule, [S-9920]), and a 401 renewed once for an account that can renew. The reply comes back
/// whatever its status, for the service's error scheme to read ([S-9921]); only a transport failure
/// throws.
/// </summary>
internal sealed partial class AccountService
{
    /// <summary>Send a service request as <paramref name="accountId"/>, or anonymously when it is null.</summary>
    public async Task<ServiceReply> SendServiceAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(request);

        if (accountId is null)
        {
            var anonymous = Template(descriptor, request, null, ifNoneMatch);
            var plain = await AccountHttp.SendAsync(anonymous, CredentialKind.None, null, cancellation, retryThrottles: false).ConfigureAwait(false);
            return Reply(plain);
        }

        var account = Require(accountId);
        if (account.Provider != descriptor.Provider)
        {
            throw new AccountException(AccountError.Refused, $"{account} does not belong to {descriptor.Provider}");
        }

        var template = Template(descriptor, request, account, ifNoneMatch);
        var scheme = AccountRules.MemberOf(account)?.Scheme ?? TokenScheme.Bearer;
        var (token, sealedToken) = await TokenAsync(account, cancellation).ConfigureAwait(false);

        var reply = await AccountHttp.SendAsync(template, CredentialKind.Bearer, r => SetToken(r, scheme, token), cancellation, retryThrottles: false).ConfigureAwait(false);

        if (reply.Status == 401 && AccountRules.AuthOf(account) == AccountAuth.OAuthRefresh && account.SealedRefreshToken is not null)
        {
            // Rejected before its stored expiry (revoked, rotated elsewhere): renew once and retry once.
            var (renewed, _) = await RefreshAsync(account.Id, sealedToken, cancellation).ConfigureAwait(false);
            reply = await AccountHttp.SendAsync(template, CredentialKind.Bearer, r => SetToken(r, scheme, renewed), cancellation, retryThrottles: false).ConfigureAwait(false);
        }

        if (reply.Status == 401)
        {
            MarkProblem(account.Id, Str.Shell_Accounts_ProviderRefused.Format(("provider", account.Provider.Label())));
        }

        return Reply(reply);
    }

    /// <summary>A request with a token that is not (yet) an account's: the identity of a new sign-in.</summary>
    private static async Task<ServiceReply> SendWithTokenAsync(ServiceDescriptor descriptor, ServiceRequest request, string token, TokenScheme scheme, CancellationToken cancellation)
    {
        var template = Template(descriptor, request, null, null);
        var reply = await AccountHttp.SendAsync(template, CredentialKind.Bearer, r => SetToken(r, scheme, token), cancellation, retryThrottles: false).ConfigureAwait(false);
        return Reply(reply);
    }

    private static RequestTemplate Template(ServiceDescriptor descriptor, ServiceRequest request, Account? account, string? ifNoneMatch)
    {
        var address = request.Address;
        if (descriptor.Root == ApiRootRule.PerAccount && account?.ServerUrl is { } own && Uri.TryCreate(own, UriKind.Absolute, out var root))
        {
            address = ServiceRoots.Rebase(address, descriptor.ApiRoot, root)
                ?? throw new AccountException(AccountError.Refused, $"{address.Host} is not under {descriptor.ApiRoot.Host}");
        }

        if (address.Scheme != Uri.UriSchemeHttps)
        {
            throw new AccountException(AccountError.Refused, Str.Shell_Net_RefusedScheme.Format(("scheme", address.Scheme)));
        }

        var template = new RequestTemplate(
            request.Verb == HttpVerb.Post ? HttpMethod.Post : HttpMethod.Get,
            address,
            request.Body?.Utf8.ToArray(),
            request.Body?.ContentType);

        foreach (var pin in descriptor.Pinned)
        {
            template.Headers.Add((pin.Name, pin.Value));
        }

        // [S-9905] The held entity tag, sent through the typed header (a 304 then reuses the held body).
        template.IfNoneMatch = ifNoneMatch;

        if (!template.Headers.Any(h => string.Equals(h.Name, "Accept", StringComparison.OrdinalIgnoreCase)))
        {
            template.Headers.Add(("Accept", "application/json"));
        }

        return template;
    }

    private static ServiceReply Reply(AccountReply reply)
        => new(reply.Status, reply.Headers, Encoding.UTF8.GetBytes(reply.Body));
}
