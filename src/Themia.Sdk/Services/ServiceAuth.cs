namespace Themia.Sdk.Services;

/// <summary>
/// [S-9910] How a provider's accounts sign in: a closed set of strategies, each a sealed record of
/// data a provider fills in (addresses, client id, scopes, the loopback's shape). The host runs them
/// (<c>ServiceSignIn</c>); no provider writes sign-in code (docs/WIDGET-KIT.md section 5.7).
/// </summary>
public abstract record ServiceAuth
{
    /// <summary>How the token this member yields is sent ([S-9911]: Linear's pasted keys are not Bearer).</summary>
    public abstract TokenScheme Scheme { get; }

    private protected ServiceAuth()
    {
    }
}

/// <summary>
/// The browser flow ([S-1780]..[S-1784]): the system browser, a loopback listener, a fresh state and
/// PKCE S256, the code exchanged at <see cref="TokenUrl"/>.
/// </summary>
/// <param name="AuthorizeUrl">The provider's sign-in page (https).</param>
/// <param name="TokenUrl">Where the code and later the refresh token are exchanged (https).</param>
/// <param name="ClientId">The public client id; never a secret.</param>
/// <param name="Scopes">What is asked for, joined by <see cref="Separator"/>.</param>
/// <param name="Separator">Space (RFC 6749) or comma.</param>
/// <param name="Loopback">Where the browser comes back to.</param>
/// <param name="ClientSecret">None for a public client; <see cref="ClientSecretRule.BuildTime"/> when the provider insists on one.</param>
/// <param name="Refresh">How its refresh tokens behave.</param>
/// <param name="AuthorizeExtras">More pairs on the sign-in page (<c>prompt</c>, <c>access_type</c>), in order.</param>
/// <param name="ScopeOnToken">The scopes go to the token endpoint too, with the code and with every refresh.</param>
/// <param name="Identity">Who signed in, read once after the exchange; null when the provider's identity query names them.</param>
public sealed record OAuthPkceLoopback(
    Uri AuthorizeUrl,
    Uri TokenUrl,
    string ClientId,
    IReadOnlyList<string> Scopes,
    ScopeSeparator Separator,
    LoopbackRule Loopback,
    ClientSecretRule ClientSecret,
    RefreshRule Refresh,
    IReadOnlyList<KeyValuePair<string, string>>? AuthorizeExtras = null,
    bool ScopeOnToken = false,
    IdentityEndpoint? Identity = null,
    TokenScheme TokenAs = TokenScheme.Bearer) : ServiceAuth
{
    public override TokenScheme Scheme => TokenAs;
}

/// <summary>
/// The device authorization grant (RFC 8628): the user types a short code on the provider's page
/// while Themia polls; no browser on this PC is needed and no secret exists.
/// </summary>
/// <param name="GrantPage">Where the user widens what the account may read when the service grants per resource (a GitHub App's installation page), or null.</param>
public sealed record OAuthDeviceCode(
    Uri DeviceUrl,
    Uri TokenUrl,
    string ClientId,
    IReadOnlyList<string> Scopes,
    ScopeSeparator Separator,
    RefreshRule Refresh,
    Uri? GrantPage = null,
    TokenScheme TokenAs = TokenScheme.Bearer) : ServiceAuth
{
    public override TokenScheme Scheme => TokenAs;
}

/// <summary>
/// A token the user creates on the service's own page (<see cref="TokenPage"/>) and pastes: checked
/// against <see cref="Shape"/>, then by the provider's identity request, and only then sealed and kept.
/// </summary>
public sealed record PastedToken(Uri TokenPage, TokenShape Shape, TokenScheme TokenAs = TokenScheme.Bearer) : ServiceAuth
{
    public override TokenScheme Scheme => TokenAs;
}

/// <summary>No sign-in: only where the descriptor allows public reads ([S-9912]).</summary>
public sealed record AnonymousAuth : ServiceAuth
{
    public static AnonymousAuth Instance { get; } = new();

    public override TokenScheme Scheme => TokenScheme.Bearer;
}

/// <summary>How a token goes on a request.</summary>
public enum TokenScheme
{
    /// <summary><c>Authorization: Bearer &lt;token&gt;</c>.</summary>
    Bearer,

    /// <summary><c>Authorization: &lt;token&gt;</c> (a personal API key some services take bare).</summary>
    Raw,
}

/// <summary>How scopes are joined.</summary>
public enum ScopeSeparator
{
    Space,
    Comma,
}

/// <summary>Whether a browser flow needs a client secret.</summary>
public enum ClientSecretRule
{
    /// <summary>A public client: PKCE alone proves the exchange.</summary>
    None,

    /// <summary>
    /// The provider insists on a secret even for an installed app; it is compiled in from the build
    /// environment (<c>THEMIA_CLIENT_SECRETS</c>, the provider's <c>BuildSecret</c> key), never source.
    /// </summary>
    BuildTime,
}

/// <summary>Which loopback address a redirect names.</summary>
public enum LoopbackHost
{
    /// <summary><c>localhost</c> (the listener answers on 127.0.0.1 and ::1, [S-1784]).</summary>
    Localhost,

    /// <summary><c>127.0.0.1</c>.</summary>
    Loopback4,
}

/// <summary>
/// [S-9913] Where the browser comes back to: the host as written in the redirect, its path, and the
/// ports the listener may take: any free one (the provider ignores the port of a registered
/// loopback redirect) or the listed ones in order (the provider matches the registration exactly).
/// </summary>
public sealed record LoopbackRule(LoopbackHost Host, string Path, IReadOnlyList<int>? Ports = null)
{
    /// <summary>The listener takes any free port.</summary>
    public bool AnyPort => Ports is null or { Count: 0 };

    /// <summary>The redirect address for a listener on <paramref name="port"/>: <c>http://localhost:5050/</c>, <c>http://127.0.0.1:5050</c>.</summary>
    public string RedirectUri(int port)
    {
        var host = Host == LoopbackHost.Localhost ? "localhost" : "127.0.0.1";
        return $"http://{host}:{port.ToString(System.Globalization.CultureInfo.InvariantCulture)}{Path}";
    }
}

/// <summary>
/// [S-9914] How a provider's refresh tokens behave. A rotating one is written before the new access
/// token is used (inside the single-flight refresh, [S-530]); a provider that revokes everything on a
/// reuse outside <see cref="ReuseGrace"/> never sees the old one again after that.
/// </summary>
public sealed record RefreshRule(bool Rotates, TimeSpan ReuseGrace)
{
    /// <summary>A refresh token that stays the same, or is replaced only when the provider sends a new one.</summary>
    public static RefreshRule Stable { get; } = new(false, TimeSpan.Zero);
}

/// <summary>[S-9915] The shape a pasted token must have before it is even tried: its length, its alphabet, an optional prefix.</summary>
public sealed record TokenShape(int MinLength, int MaxLength, string Alphabet, IReadOnlyList<string>? Prefixes = null)
{
    /// <summary>Letters, digits, <c>_</c> and <c>-</c>: what every token shape of today is made of.</summary>
    public const string Word = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-";

    /// <summary>Lower-case hex.</summary>
    public const string Hex = "0123456789abcdef";

    /// <summary>The token as it would be stored (trimmed), or null when it cannot be one of this shape.</summary>
    public string? Accept(string? pasted)
    {
        var token = pasted?.Trim();
        if (string.IsNullOrEmpty(token) || token.Length < MinLength || token.Length > MaxLength)
        {
            return null;
        }

        if (Prefixes is { Count: > 0 } prefixes && !prefixes.Any(p => token.StartsWith(p, StringComparison.Ordinal)))
        {
            return null;
        }

        return token.All(c => Alphabet.Contains(c, StringComparison.Ordinal)) ? token : null;
    }
}

/// <summary>
/// [S-9916] Who signed in, for a provider whose identity is one GET with the new token: the id field,
/// the fields that may hold the address (first non-empty wins, else the id), the display name, and
/// the prefix the account id gets (<c>google:</c>), so signing in again replaces the account ([S-505]).
/// </summary>
public sealed record IdentityEndpoint(Uri Url, string IdField, IReadOnlyList<string> AddressFields, string NameField, string IdPrefix = "");

/// <summary>[S-9910] The sign-in members a provider offers, in the order the Accounts page shows them.</summary>
public sealed record ProviderSignIn(IReadOnlyList<ServiceAuth> Members)
{
    /// <summary>A provider the host signs in itself (a password form) or none at all.</summary>
    public static ProviderSignIn None { get; } = new([]);

    public T? Find<T>() where T : ServiceAuth => Members.OfType<T>().FirstOrDefault();
}

/// <summary>The scope list as a provider wants it written.</summary>
public static class Scopes
{
    public static string Join(IReadOnlyList<string> scopes, ScopeSeparator separator)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        return string.Join(separator == ScopeSeparator.Comma ? "," : " ", scopes);
    }
}
