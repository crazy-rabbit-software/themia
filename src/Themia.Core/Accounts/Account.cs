using System.Text.Json.Serialization;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.Core.Accounts;

/// <summary>
/// One account, validated: every field its provider needs is present and well formed.
/// <para>
/// Immutable, so the service can hand the same instance to any thread and replace it whole when
/// something changes. The three secret fields are <b>sealed</b> — DPAPI envelopes produced by the
/// app (<see cref="SecretEnvelope"/>) — never the secret itself. Core never opens them; the app does,
/// at the moment a request needs one, so a plaintext token lives for one request rather than for the
/// life of the process.
/// </para>
/// </summary>
public sealed record Account
{
    /// <summary>
    /// Stable id (<see cref="AccountIds"/>), so signing the same account in again replaces it
    /// instead of adding a twin, and widget configs naming it keep pointing at it.
    /// </summary>
    public required string Id { get; init; }

    public required ProviderId Provider { get; init; }

    /// <summary>What the account is known by: an email address or a login.</summary>
    public required string Address { get; init; }

    /// <summary>The person's name as the provider gave it; may be empty.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>The user's own name for the account, or null for none.</summary>
    public string? Nickname { get; init; }

    /// <summary>IMAP server host.</summary>
    public string? Host { get; init; }

    /// <summary>IMAP server port.</summary>
    public int? Port { get; init; }

    /// <summary>Login for IMAP and DAV.</summary>
    public string? Username { get; init; }

    /// <summary>The resolved CalDAV / CardDAV collection URL.</summary>
    public string? ServerUrl { get; init; }

    /// <summary>Sealed access token (OAuth providers).</summary>
    public string? SealedAccessToken { get; init; }

    /// <summary>Sealed refresh token (Microsoft, Google).</summary>
    public string? SealedRefreshToken { get; init; }

    /// <summary>Sealed password (IMAP, CalDAV, CardDAV).</summary>
    public string? SealedPassword { get; init; }

    /// <summary>When the access token stops working; null when unknown or when it never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>When the account was first added.</summary>
    public DateTimeOffset AddedAt { get; init; }

    /// <summary>
    /// [S-9919] Which of its provider's sign-in members signed it in, when the provider offers more than
    /// one kind (a device code and a pasted token); null for the provider's only flow (Microsoft and
    /// Google's browser, the password forms).
    /// </summary>
    public SignInKind? SignedInWith { get; init; }

    /// <summary>What a list shows: the nickname, else the person's name, else the address.</summary>
    public string Label => Nickname ?? (DisplayName.Length > 0 ? DisplayName : Address);

    /// <summary>
    /// Never prints the sealed fields. A record's generated ToString would, and an account written
    /// into a log line by accident must not carry even the ciphertext of a token.
    /// </summary>
    public override string ToString() => $"{Provider.Value} account {Id}";
}

/// <summary>[S-503] <c>accounts.json</c> as it is written: a schema version and the accounts in order.</summary>
public sealed class AccountDocument
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<StoredAccount?>? Accounts { get; set; } = [];
}

/// <summary>
/// One account exactly as the file holds it. Every field is optional and the provider is a string,
/// because this is the untrusted side of the boundary: <see cref="AccountRules.Validate"/> turns it
/// into an <see cref="Account"/> or drops it, and nothing else reads it.
/// </summary>
public sealed class StoredAccount
{
    public string? Id { get; set; }

    public string? Provider { get; set; }

    public string? Address { get; set; }

    public string? DisplayName { get; set; }

    public string? Nickname { get; set; }

    public string? Host { get; set; }

    public int? Port { get; set; }

    public string? Username { get; set; }

    public string? ServerUrl { get; set; }

    public string? AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public string? Password { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? AddedAt { get; set; }

    /// <summary>[S-9919] <c>browser</c>, <c>deviceCode</c> or <c>pastedToken</c>; absent for the provider's only flow.</summary>
    public string? SignIn { get; set; }

    public static StoredAccount From(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new StoredAccount
        {
            Id = account.Id,
            Provider = account.Provider.Value,
            Address = account.Address,
            DisplayName = account.DisplayName,
            Nickname = account.Nickname,
            Host = account.Host,
            Port = account.Port,
            Username = account.Username,
            ServerUrl = account.ServerUrl,
            AccessToken = account.SealedAccessToken,
            RefreshToken = account.SealedRefreshToken,
            Password = account.SealedPassword,
            ExpiresAt = account.ExpiresAt,
            AddedAt = account.AddedAt,
            SignIn = account.SignedInWith is { } kind ? SignInKinds.Wire(kind) : null,
        };
    }
}

/// <summary>Source-generated JSON for <c>accounts.json</c>. Native AOT has no reflection serializer.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AccountDocument))]
internal sealed partial class AccountsJsonContext : JsonSerializerContext;
