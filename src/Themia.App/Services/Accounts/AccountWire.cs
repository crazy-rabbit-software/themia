using System.Text.Json.Serialization;

namespace Themia.App.Services.Accounts;

/// <summary>
/// A token endpoint's answer to a refresh, success or error; the error fields are read whatever the
/// status was. An authorization-code exchange is read by Core's <c>CodeExchange</c> instead.
/// </summary>
internal sealed class TokenReply
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

/// <summary>Source-generated JSON for the sign-in endpoints. Native AOT has no reflection serializer.</summary>
[JsonSourceGenerationOptions(NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(TokenReply))]
internal sealed partial class AccountWireContext : JsonSerializerContext;
