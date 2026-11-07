using System.Globalization;
using System.Security.Cryptography;

namespace Themia.Core.Mcp;

/// <summary>
/// [S-9977] Where a client reaches this install's server: its loopback port and its bearer token.
/// The token is a secret: it is never logged, never in the settings file or a backup, and shown only
/// inside what ⚙'s copy lines put on the clipboard.
/// </summary>
public sealed record McpAddress(int Port, string Token)
{
    /// <summary>The URL a client is given.</summary>
    public string Url => $"http://127.0.0.1:{Port.ToString(CultureInfo.InvariantCulture)}{McpHttp.Path}";

    /// <summary>The address with the token left out, for what is shown on screen and in the log.</summary>
    public override string ToString() => Url;
}

/// <summary>[S-9977] [S-9978] The token's rules and the setups ⚙ copies.</summary>
public static class McpAccess
{
    /// <summary>The token's length in random bytes (256 bits).</summary>
    public const int TokenBytes = 32;

    /// <summary>[S-9977] A new token: 32 random bytes from the system's generator, base64url without padding (43 characters).</summary>
    public static string NewToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>[S-9977] Whether a stored token has the shape this install writes: 43 base64url characters.</summary>
    public static bool IsToken(string? token)
        => token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>[S-9977] Whether a stored port can be listened on by a user: 1024 to 65535.</summary>
    public static bool IsPort(int port) => port is >= 1024 and <= 65535;

    /// <summary>[S-9978] The command that adds the server to Claude Code, for this user.</summary>
    public static string ClaudeCodeCommand(McpAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return $"claude mcp add --transport http --scope user themia {address.Url} --header \"Authorization: Bearer {address.Token}\"";
    }

    /// <summary>
    /// [S-9978] The <c>mcpServers</c> entry other clients' configuration files take (an HTTP server
    /// with its header), as JSON.
    /// </summary>
    public static string JsonConfig(McpAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var bytes = JsonRpc.Write(w =>
        {
            w.WriteStartObject("mcpServers");
            w.WriteStartObject("themia");
            w.WriteString("type", "http");
            w.WriteString("url", address.Url);
            w.WriteStartObject("headers");
            w.WriteString("Authorization", "Bearer " + address.Token);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        });
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
