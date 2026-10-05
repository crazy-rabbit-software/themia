using System.Globalization;
using System.Text.Json;
using Themia.Core.Localization;

namespace Themia.Core.Accounts;

/// <summary>What came back to the loopback port.</summary>
public enum RedirectKind
{
    /// <summary>Not a sign-in at all — the browser's own <c>/favicon.ico</c>, a stray probe. Keep listening.</summary>
    Unrelated,

    /// <summary>An authorization code for the state this sign-in issued.</summary>
    Code,

    /// <summary>The provider refused (the user pressed Cancel, consent was withheld).</summary>
    ProviderError,

    /// <summary>A sign-in, but not the one this app started: refused.</summary>
    StateMismatch,
}

/// <param name="Kind">What it was.</param>
/// <param name="Code">The authorization code, for <see cref="RedirectKind.Code"/>.</param>
/// <param name="Error">The provider's error and description, for <see cref="RedirectKind.ProviderError"/>.</param>
public readonly record struct OAuthRedirect(RedirectKind Kind, string? Code, string? Error)
{
    /// <summary>
    /// [S-513] Read the redirect from the first line of the request the browser sent to the loopback
    /// port — <c>GET /?code=…&amp;state=… HTTP/1.1</c>. [S-1781] Microsoft's and Google's come back
    /// through this one reader.
    /// <para>
    /// The state is checked before anything else is believed, error included: anything on this
    /// machine can connect to the port, and only the browser this app opened knows the state. A
    /// malformed pair (a stray <c>&amp;</c>, a key without a value) is skipped rather than throwing
    /// the whole redirect away.
    /// </para>
    /// </summary>
    public static OAuthRedirect Parse(string? requestLine, string expectedState)
    {
        var target = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: >= 2 } parts
            ? parts[1]
            : null;

        var mark = target?.IndexOf('?', StringComparison.Ordinal) ?? -1;
        if (target is null || mark < 0)
        {
            return new OAuthRedirect(RedirectKind.Unrelated, null, null);
        }

        string? code = null, state = null, error = null, description = null;

        foreach (var pair in target[(mark + 1)..].Split('&'))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                continue;
            }

            string value;
            try
            {
                value = Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                continue;
            }

            switch (pair[..equals])
            {
                case "code": code = value; break;
                case "state": state = value; break;
                case "error": error = value; break;
                case "error_description": description = value; break;
            }
        }

        if (code is null && error is null)
        {
            return new OAuthRedirect(RedirectKind.Unrelated, null, null);
        }

        if (!Pkce.StateMatches(expectedState, state))
        {
            return new OAuthRedirect(RedirectKind.StateMismatch, null, null);
        }

        if (error is not null)
        {
            return new OAuthRedirect(RedirectKind.ProviderError, null,
                error == "access_denied" ? Str.Shell_Accounts_SignInCancelled.Text() : description ?? error);
        }

        return string.IsNullOrEmpty(code)
            ? new OAuthRedirect(RedirectKind.ProviderError, null, Str.Shell_Accounts_NoAuthorizationCode.Text())
            : new OAuthRedirect(RedirectKind.Code, code, null);
    }
}

/// <summary>The tokens an authorization-code exchange handed out.</summary>
/// <param name="AccessToken">Never empty.</param>
/// <param name="RefreshToken">Null when the provider gave none.</param>
/// <param name="ExpiresIn">Seconds, as the provider said; <see cref="TokenExpiry"/> judges it.</param>
public sealed record CodeGrant(string AccessToken, string? RefreshToken, long? ExpiresIn);

/// <summary>What a token endpoint answered an authorization-code exchange: a grant, or why not.</summary>
/// <param name="Grant">The tokens; null when the exchange failed.</param>
/// <param name="Failure">The provider's own words (one line) or the HTTP status; null on success.</param>
public readonly record struct CodeExchange(CodeGrant? Grant, string? Failure)
{
    /// <summary>
    /// [S-1782] Read a token endpoint's answer. Only a 2xx carrying a non-empty <c>access_token</c>
    /// is a grant; anything else fails with what the provider said (<c>error_description</c>, cut to
    /// its first line — Microsoft appends trace and correlation ids) or, failing that, the status.
    /// <c>expires_in</c> is read as a number or a numeric string; a non-string token field is absent.
    /// </summary>
    public static CodeExchange Parse(int status, string? body)
    {
        var success = status is >= 200 and < 300;

        if (success && ReadGrant(body) is { } grant)
        {
            return new CodeExchange(grant, null);
        }

        return new CodeExchange(null, ErrorBody.Message(body) ?? $"HTTP {status.ToString(CultureInfo.InvariantCulture)}");
    }

    private static CodeGrant? ReadGrant(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object || Text(root, "access_token") is not { Length: > 0 } access)
            {
                return null;
            }

            return new CodeGrant(access, Text(root, "refresh_token") is { Length: > 0 } refresh ? refresh : null, Seconds(root, "expires_in"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? Seconds(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var text) => text,
            _ => null,
        };
    }
}
