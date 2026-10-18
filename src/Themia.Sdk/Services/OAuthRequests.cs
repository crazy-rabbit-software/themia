using System.Globalization;
using System.Text.Json;

namespace Themia.Sdk.Services;

/// <summary>
/// [S-9918] The requests of the sign-in members, built from their data alone, so one host path runs
/// every provider's browser, device-code and refresh flow (docs/WIDGET-KIT.md section 5.7). Pure.
/// </summary>
public static class OAuthRequests
{
    /// <summary>
    /// The sign-in page: <c>response_type=code</c>, the client, the redirect, the scopes, the state and
    /// the S256 challenge, then the member's own extras in order.
    /// </summary>
    public static Uri AuthorizeUrl(OAuthPkceLoopback member, string redirectUri, string state, string challenge)
    {
        ArgumentNullException.ThrowIfNull(member);

        List<KeyValuePair<string, string>> query =
        [
            new("response_type", "code"),
            new("client_id", member.ClientId),
            new("redirect_uri", redirectUri),
            new("scope", Scopes.Join(member.Scopes, member.Separator)),
            new("state", state),
            new("code_challenge", challenge),
            new("code_challenge_method", "S256"),
        ];
        query.AddRange(member.AuthorizeExtras ?? []);

        var root = member.AuthorizeUrl.GetLeftPart(UriPartial.Path);
        return new Uri(root + "?" + Encode(query));
    }

    /// <summary>The code exchange: grant, code, client, the secret when the member needs one, the verifier, the same redirect, and the scopes when it sends them.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Exchange(OAuthPkceLoopback member, string code, string verifier, string redirectUri, string? secret)
    {
        ArgumentNullException.ThrowIfNull(member);

        List<KeyValuePair<string, string>> fields =
        [
            new("grant_type", "authorization_code"),
            new("code", code),
            new("client_id", member.ClientId),
        ];

        if (member.ClientSecret == ClientSecretRule.BuildTime)
        {
            fields.Add(new("client_secret", secret ?? throw new ArgumentNullException(nameof(secret), "the member needs its build-time secret")));
        }

        fields.Add(new("code_verifier", verifier));
        fields.Add(new("redirect_uri", redirectUri));

        if (member.ScopeOnToken)
        {
            fields.Add(new("scope", Scopes.Join(member.Scopes, member.Separator)));
        }

        return fields;
    }

    /// <summary>A refresh: the client, the secret when the member needs one, the grant and the token, and the scopes when it sends them.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Refresh(ServiceAuth member, string refreshToken, string? secret)
    {
        ArgumentNullException.ThrowIfNull(member);

        var (clientId, scopes, separator, needsSecret, scopeOnToken) = member switch
        {
            OAuthPkceLoopback b => (b.ClientId, b.Scopes, b.Separator, b.ClientSecret == ClientSecretRule.BuildTime, b.ScopeOnToken),
            OAuthDeviceCode d => (d.ClientId, d.Scopes, d.Separator, false, false),
            _ => throw new ArgumentException("only an OAuth member renews a sign-in", nameof(member)),
        };

        List<KeyValuePair<string, string>> fields = [new("client_id", clientId)];
        if (needsSecret)
        {
            fields.Add(new("client_secret", secret ?? throw new ArgumentNullException(nameof(secret), "the member needs its build-time secret")));
        }

        fields.Add(new("grant_type", "refresh_token"));
        fields.Add(new("refresh_token", refreshToken));

        if (scopeOnToken)
        {
            fields.Add(new("scope", Scopes.Join(scopes, separator)));
        }

        return fields;
    }

    /// <summary>RFC 8628 §3.1: the device authorization request.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DeviceAuthorization(OAuthDeviceCode member)
    {
        ArgumentNullException.ThrowIfNull(member);

        List<KeyValuePair<string, string>> fields = [new("client_id", member.ClientId)];
        if (member.Scopes.Count > 0)
        {
            fields.Add(new("scope", Scopes.Join(member.Scopes, member.Separator)));
        }

        return fields;
    }

    /// <summary>RFC 8628 §3.4: one poll of the token endpoint.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DevicePoll(OAuthDeviceCode member, string deviceCode)
    {
        ArgumentNullException.ThrowIfNull(member);
        return
        [
            new("client_id", member.ClientId),
            new("device_code", deviceCode),
            new("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
        ];
    }

    /// <summary>The <c>Authorization</c> header's value for a token in <paramref name="scheme"/>.</summary>
    public static string AuthorizationValue(TokenScheme scheme, string token)
        => scheme == TokenScheme.Raw ? token : "Bearer " + token;

    public static string Encode(IEnumerable<KeyValuePair<string, string>> pairs)
        => string.Join('&', pairs.Select(p => string.Create(CultureInfo.InvariantCulture, $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}")));
}

/// <summary>Who signed in: the provider's own id for the account, its address, its display name.</summary>
public sealed record ServiceIdentity(string Id, string Address, string Name);

/// <summary>[S-9916] Reading an <see cref="IdentityEndpoint"/>'s answer.</summary>
public static class IdentityAnswers
{
    /// <summary>
    /// The account the answer names, or null when it names none (no object, no id). The id gets the
    /// endpoint's prefix; the address is the first non-empty address field, else the id; the name
    /// the name field, else the address.
    /// </summary>
    public static ServiceIdentity? Read(IdentityEndpoint endpoint, string? body)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || Text(root, endpoint.IdField) is not { Length: > 0 } id)
            {
                return null;
            }

            var address = endpoint.AddressFields.Select(f => Text(root, f)).FirstOrDefault(a => a is { Length: > 0 }) ?? id;
            var name = Text(root, endpoint.NameField) is { Length: > 0 } named ? named : address;
            return new ServiceIdentity(endpoint.IdPrefix + id, address, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}

/// <summary>[S-9919] A device authorization answer (RFC 8628 §3.2).</summary>
public sealed record DeviceGrant(string DeviceCode, string UserCode, Uri VerificationUri, long? IntervalSeconds, long? ExpiresInSeconds)
{
    /// <summary>
    /// The grant in a 2xx answer, or null: every field present and well formed, the page an https
    /// address (the user types the code there, so it must be the provider's own site).
    /// </summary>
    public static DeviceGrant? Parse(int status, string? body)
    {
        if (status is < 200 or >= 300 || string.IsNullOrWhiteSpace(body))
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

            var device = Text(root, "device_code");
            var user = Text(root, "user_code");
            var page = Text(root, "verification_uri") ?? Text(root, "verification_url");

            if (device is not { Length: > 0 and <= 512 } || user is not { Length: > 0 and <= 64 }
                || !Uri.TryCreate(page, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                return null;
            }

            return new DeviceGrant(device, user, uri, Number(root, "interval"), Number(root, "expires_in"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Number(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) switch
        {
            true when value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) => n,
            true when value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var t) => t,
            _ => null,
        };
}
