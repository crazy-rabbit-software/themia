using System.Security.Cryptography;
using System.Text;

namespace Themia.Core.Mcp;

/// <summary>[S-9965] A request's line and headers, as read off the socket: header names lowercase.</summary>
public sealed record HttpRequestHead(string Method, string Target, IReadOnlyDictionary<string, string> Headers)
{
    public string? Header(string name) => Headers.GetValueOrDefault(name);
}

/// <summary>[S-9966] What the HTTP rules made of a request: its status (200 to go on), why not, and the body's length.</summary>
public readonly record struct McpHttpVerdict(int Status, string? Problem, int BodyLength)
{
    public bool Proceeds => Status == 200;
}

/// <summary>
/// [S-9965] [S-9966] The part of MCP's Streamable HTTP transport Themia speaks, as pure rules: the
/// request head parsed with hard limits, and what is refused before a body is read (the wrong host
/// or origin, the wrong token, another path or method, an oversized or chunked body). One request
/// per connection; every answer closes it.
/// </summary>
public static class McpHttp
{
    /// <summary>[S-9965] The only path served.</summary>
    public const string Path = "/mcp";

    /// <summary>[S-9965] The longest request line and headers together.</summary>
    public const int MaxHeadBytes = 8 * 1024;

    /// <summary>[S-9965] The most header lines a request may have.</summary>
    public const int MaxHeaders = 64;

    /// <summary>The headers a request may carry once only: a second is a request smuggled or confused.</summary>
    private static readonly HashSet<string> Singletons = new(StringComparer.Ordinal)
    {
        "host", "origin", "authorization", "content-length", "content-type", "transfer-encoding", "mcp-protocol-version",
    };

    /// <summary>
    /// [S-9965] Parse a request head (up to, not including, its blank line). Only printable ASCII; a
    /// request line of three parts on HTTP/1.1; headers <c>name: value</c> with a token name, no folded
    /// lines, at most <see cref="MaxHeaders"/>, a singleton header never twice. Null with the reason otherwise.
    /// </summary>
    public static HttpRequestHead? ParseHead(ReadOnlySpan<byte> head, out string? problem)
    {
        if (head.Length > MaxHeadBytes)
        {
            problem = "the request head is too large";
            return null;
        }

        foreach (var b in head)
        {
            if (b is not ((>= 0x20 and < 0x7F) or (byte)'\r' or (byte)'\n' or (byte)'\t'))
            {
                problem = "the request head is not printable ASCII";
                return null;
            }
        }

        var lines = Encoding.ASCII.GetString(head).Split("\r\n");
        var request = lines[0].Split(' ');
        if (request.Length != 3 || request[0].Length == 0 || request[1].Length == 0 || !IsToken(request[0]))
        {
            problem = "the request line is malformed";
            return null;
        }

        if (request[2] != "HTTP/1.1")
        {
            problem = "only HTTP/1.1 is spoken";
            return null;
        }

        var headers = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Length == 0)
            {
                problem = "the request head has an empty line";
                return null;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || !IsToken(line[..colon]))
            {
                problem = "a header is malformed";
                return null;
            }

            if (i > MaxHeaders)
            {
                problem = "the request has too many headers";
                return null;
            }

            var name = line[..colon].ToLowerInvariant();
            var value = line[(colon + 1)..].Trim(' ', '\t');
            if (headers.TryGetValue(name, out var earlier))
            {
                if (Singletons.Contains(name))
                {
                    problem = $"the {name} header is given twice";
                    return null;
                }

                headers[name] = earlier + ", " + value;
            }
            else
            {
                headers[name] = value;
            }
        }

        problem = null;
        return new HttpRequestHead(request[0], request[1], headers);
    }

    /// <summary>
    /// [S-9966] Whether the request may go on to its body, checked in this order: the host is this
    /// PC's loopback at <paramref name="port"/> and an origin, if sent, is too (DNS rebinding: 403);
    /// the path is <see cref="Path"/> (404); the bearer token is this install's (401); the method is
    /// POST (405); a protocol version header names one spoken (400); the body has a length (411), is
    /// not chunked (400), fits <see cref="JsonRpc.MaxBodyBytes"/> (413), is JSON (415); an Accept
    /// header takes JSON (406).
    /// </summary>
    public static McpHttpVerdict Check(HttpRequestHead head, int port, string token)
    {
        ArgumentNullException.ThrowIfNull(head);
        ArgumentException.ThrowIfNullOrEmpty(token);

        if (head.Header("host") is not { } host || !IsLoopbackAuthority(host, port))
        {
            return Refuse(403, "the host is not this PC's loopback address");
        }

        if (head.Header("origin") is { } origin && !IsLoopbackOrigin(origin, port))
        {
            return Refuse(403, "the origin is not this PC's loopback address");
        }

        if (head.Target != Path)
        {
            return Refuse(404, "nothing is served there");
        }

        if (!HoldsToken(head.Header("authorization"), token))
        {
            return Refuse(401, "the bearer token is missing or wrong");
        }

        if (head.Method != "POST")
        {
            return Refuse(405, "only POST is served (no event stream, no session to delete)");
        }

        if (head.Header("mcp-protocol-version") is { } version && !McpProtocol.Spoken.Contains(version))
        {
            return Refuse(400, "that protocol version is not spoken");
        }

        if (head.Header("transfer-encoding") is not null)
        {
            return Refuse(400, "a chunked body is not read");
        }

        if (head.Header("content-length") is not { } lengthText)
        {
            return Refuse(411, "the body needs a length");
        }

        if (lengthText.Length is 0 or > 9 || !lengthText.All(char.IsAsciiDigit))
        {
            return Refuse(400, "the body length is malformed");
        }

        var length = int.Parse(lengthText, System.Globalization.CultureInfo.InvariantCulture);
        if (length > JsonRpc.MaxBodyBytes)
        {
            return Refuse(413, "the body is too large");
        }

        if (length == 0)
        {
            return Refuse(400, "the body is empty");
        }

        if (head.Header("content-type") is not { } type || !MediaType(type).Equals("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return Refuse(415, "the body must be application/json");
        }

        if (head.Header("accept") is { } accept && !TakesJson(accept))
        {
            return Refuse(406, "the answer is application/json");
        }

        return new McpHttpVerdict(200, null, length);
    }

    /// <summary>[S-9966] A bearer token compared in constant time: how long a wrong guess takes says nothing of it.</summary>
    public static bool HoldsToken(string? authorization, string token)
    {
        const string scheme = "Bearer ";
        if (authorization is null || !authorization.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var given = Encoding.UTF8.GetBytes(authorization[scheme.Length..].Trim());
        var expected = Encoding.UTF8.GetBytes(token);
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }

    /// <summary>
    /// [S-9965] An answer: its status line, the body's type and length, never cached, the connection
    /// closed; a 401 says the scheme, a 405 the one method.
    /// </summary>
    public static byte[] Response(int status, ReadOnlySpan<byte> body, string? contentType)
    {
        var head = new StringBuilder();
        head.Append("HTTP/1.1 ").Append(status.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(' ').Append(Reason(status)).Append("\r\n");
        if (body.Length > 0 && contentType is not null)
        {
            head.Append("Content-Type: ").Append(contentType).Append("\r\n");
        }

        head.Append("Content-Length: ").Append(body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append("\r\n");
        head.Append("Cache-Control: no-store\r\n");
        head.Append("Connection: close\r\n");
        if (status == 401)
        {
            head.Append("WWW-Authenticate: Bearer realm=\"themia\"\r\n");
        }

        if (status == 405)
        {
            head.Append("Allow: POST\r\n");
        }

        head.Append("\r\n");

        var headBytes = Encoding.ASCII.GetBytes(head.ToString());
        var all = new byte[headBytes.Length + body.Length];
        headBytes.CopyTo(all, 0);
        body.CopyTo(all.AsSpan(headBytes.Length));
        return all;
    }

    /// <summary>A refusal's answer: its reason as plain text.</summary>
    public static byte[] Refusal(McpHttpVerdict verdict)
        => Response(verdict.Status, Encoding.UTF8.GetBytes(verdict.Problem ?? Reason(verdict.Status)), "text/plain; charset=utf-8");

    /// <summary>Where the head ends (the index of its blank line's CRLFCRLF), or -1.</summary>
    public static int HeadEnd(ReadOnlySpan<byte> buffer) => buffer.IndexOf("\r\n\r\n"u8);

    private static McpHttpVerdict Refuse(int status, string problem) => new(status, problem, 0);

    /// <summary><c>127.0.0.1:port</c> or <c>localhost:port</c>, exactly.</summary>
    private static bool IsLoopbackAuthority(string authority, int port)
    {
        var suffix = ":" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return authority.Equals("127.0.0.1" + suffix, StringComparison.OrdinalIgnoreCase)
            || authority.Equals("localhost" + suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLoopbackOrigin(string origin, int port)
        => origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && IsLoopbackAuthority(origin["http://".Length..], port);

    private static string MediaType(string contentType)
    {
        var semicolon = contentType.IndexOf(';', StringComparison.Ordinal);
        return (semicolon < 0 ? contentType : contentType[..semicolon]).Trim();
    }

    private static bool TakesJson(string accept)
    {
        foreach (var part in accept.Split(','))
        {
            var type = MediaType(part);
            if (type.Equals("application/json", StringComparison.OrdinalIgnoreCase) || type is "*/*" or "application/*")
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsToken(string text)
    {
        foreach (var c in text)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || "!#$%&'*+-.^_`|~".Contains(c, StringComparison.Ordinal)))
            {
                return false;
            }
        }

        return text.Length > 0;
    }

    private static string Reason(int status) => status switch
    {
        200 => "OK",
        202 => "Accepted",
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        405 => "Method Not Allowed",
        406 => "Not Acceptable",
        408 => "Request Timeout",
        411 => "Length Required",
        413 => "Content Too Large",
        415 => "Unsupported Media Type",
        431 => "Request Header Fields Too Large",
        500 => "Internal Server Error",
        503 => "Service Unavailable",
        _ => "Status",
    };
}
