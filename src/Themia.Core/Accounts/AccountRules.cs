using Themia.Sdk;
using Themia.Sdk.Services;
namespace Themia.Core.Accounts;

/// <summary>
/// [S-504] The boundary between <c>accounts.json</c> and the app: a stored entry becomes a whole
/// <see cref="Account"/> or nothing.
/// <para>
/// What is repaired and what is dropped follows one rule. A field the account can live without — a
/// nickname of spaces, a missing IMAP port — is repaired to the value the
/// provider would have used. A field that names <i>where</i> the account is — the IMAP host, the DAV
/// collection, the provider itself — cannot be guessed, and an account without it is dropped. Either
/// way it is said out loud in the repair list, never silently.
/// </para>
/// <para>
/// A missing secret is neither: an OAuth account without a refresh token is a real state ("sign in
/// again"), so it is kept, and the service reports it.
/// </para>
/// </summary>
public static class AccountRules
{
    /// <summary>The IMAP port the classic app assumed: implicit TLS.</summary>
    public const int DefaultImapPort = 993;

    /// <summary>Validate a whole document, dropping and repairing per entry.</summary>
    public static IReadOnlyList<Account> Validate(AccountDocument document, DateTimeOffset now, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(repairs);

        var accounts = new List<Account>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var stored in document.Accounts ?? [])
        {
            if (stored is null)
            {
                repairs.Add("dropped an empty account entry");
                continue;
            }

            if (Validate(stored, now, repairs) is not { } account)
            {
                continue;
            }

            if (!seen.Add(account.Id))
            {
                // The first one wins: it is the one that was there longest, and two entries with one
                // id cannot both be addressed by a widget that names the id.
                repairs.Add($"dropped a second account with id '{account.Id}'");
                continue;
            }

            accounts.Add(account);
        }

        return accounts;
    }

    /// <summary>One entry, or null with the reason added to <paramref name="repairs"/>.</summary>
    public static Account? Validate(StoredAccount stored, DateTimeOffset now, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(stored);
        ArgumentNullException.ThrowIfNull(repairs);

        var id = Clean(stored.Id);
        if (id is null)
        {
            repairs.Add("dropped an account with no id");
            return null;
        }

        if (!ProviderCatalog.TryParse(stored.Provider, out var provider))
        {
            repairs.Add($"dropped account '{id}': unknown provider '{stored.Provider}'");
            return null;
        }

        var address = Clean(stored.Address) ?? Clean(stored.Username);
        if (address is null)
        {
            repairs.Add($"dropped account '{id}': no address");
            return null;
        }

        var account = new Account
        {
            Id = id,
            Provider = provider,
            Address = address,
            DisplayName = Clean(stored.DisplayName) ?? string.Empty,
            Nickname = Clean(stored.Nickname),
            SealedAccessToken = Clean(stored.AccessToken),
            SealedRefreshToken = Clean(stored.RefreshToken),
            SealedPassword = Clean(stored.Password),
            ExpiresAt = stored.ExpiresAt,
            AddedAt = stored.AddedAt ?? now,
            SignedInWith = SignInOf(id, provider, stored.SignIn, repairs),
        };

        switch (provider)
        {
            case var pid when pid == HostProviders.Imap:
            {
                var host = Clean(stored.Host);
                var user = Clean(stored.Username);
                if (host is null || user is null)
                {
                    repairs.Add($"dropped IMAP account '{id}': no {(host is null ? "server" : "user name")}");
                    return null;
                }

                var port = stored.Port ?? 0;
                if (port is < 1 or > 65535)
                {
                    repairs.Add($"IMAP account '{id}' had port {stored.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"}; using {DefaultImapPort}");
                    port = DefaultImapPort;
                }

                return account with { Host = host, Port = port, Username = user };
            }

            case var pid when pid == HostProviders.CalDav || pid == HostProviders.CardDav:
            {
                var url = Clean(stored.ServerUrl);
                var user = Clean(stored.Username);
                if (!IsHttpUrl(url) || user is null)
                {
                    repairs.Add($"dropped {provider.Label()} account '{id}': no {(user is null ? "user name" : "usable server address")}");
                    return null;
                }

                return account with { ServerUrl = url, Username = user };
            }

            default:
                return account;
        }
    }

    /// <summary>
    /// [S-9919] The sign-in kind as stored: one of the provider's own members, or nothing. A kind the
    /// provider does not offer (a hand edit, a member the build no longer has) is dropped and
    /// logged; the account keeps its tokens and signs in again if they no longer work.
    /// </summary>
    private static SignInKind? SignInOf(string id, ProviderId provider, string? stored, List<string> repairs)
    {
        if (Clean(stored) is not { } text)
        {
            return null;
        }

        if (SignInKinds.TryParse(text, out var kind) && ProviderCatalog.Of(provider).SignIn.Members.Any(m => SignInKinds.Of(m) == kind))
        {
            return kind;
        }

        repairs.Add($"account '{id}' named a sign-in '{text}' its provider does not offer; dropped it");
        return null;
    }

    /// <summary>[S-9919] How an account proves itself: a pasted token never renews; else as its provider's accounts do.</summary>
    public static AccountAuth AuthOf(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        return account.SignedInWith == SignInKind.PastedToken ? AccountAuth.Token : account.Provider.Auth();
    }

    /// <summary>[S-9911] The sign-in member that signed <paramref name="account"/> in, or null for a password account.</summary>
    public static Sdk.Services.ServiceAuth? MemberOf(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var members = ProviderCatalog.Of(account.Provider).SignIn.Members;
        return account.SignedInWith is { } kind
            ? members.FirstOrDefault(m => SignInKinds.Of(m) == kind)
            : members.FirstOrDefault();
    }

    /// <summary>The document for a list of accounts, ready to serialise.</summary>
    public static AccountDocument ToDocument(IEnumerable<Account> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        return new AccountDocument
        {
            SchemaVersion = AccountDocument.CurrentSchemaVersion,
            Accounts = [.. accounts.Select(StoredAccount.From)],
        };
    }

    /// <summary>A nickname as the user typed it: trimmed, and nothing at all for whitespace.</summary>
    public static string? CleanNickname(string? nickname) => Clean(nickname);

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsHttpUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && uri.Host.Length > 0;
}

/// <summary>
/// [S-505] Account ids. Signing in to the same account again yields the same id, so it replaces the
/// old entry (keeping its nickname) instead of adding a second one, and widget configs keep naming it.
/// </summary>
public static class AccountIds
{
    /// <summary>Microsoft: the Graph user id, verbatim.</summary>
    public static string Microsoft(string graphUserId) => graphUserId;

    public static string Google(string subject) => $"google:{subject}";

    public static string Imap(string username, string host) => $"imap:{username}@{host}";

    /// <summary>[S-9919] A kit service's account: its provider and the id the service's identity request named.</summary>
    public static string Service(ProviderId provider, string serviceId) => $"{provider.Value}:{serviceId}";

    /// <summary>
    /// DAV: the user and the collection itself (authority and path), not just the server. One user
    /// may sign in to several collections of one server (Google's legacy CalDAV has an address per
    /// calendar); keyed by the server alone, each sign-in replaced the last (classic issue #133).
    /// </summary>
    public static string Dav(ProviderId provider, string username, Uri collection)
    {
        ArgumentNullException.ThrowIfNull(collection);
        var where = collection.Authority + collection.AbsolutePath.TrimEnd('/');
        return provider == HostProviders.CalDav || provider == HostProviders.CardDav
            ? $"{provider.Value}:{username}@{where}"
            : throw new ArgumentOutOfRangeException(nameof(provider), provider, "not a DAV provider");
    }
}
