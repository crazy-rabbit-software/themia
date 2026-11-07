using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Themia.Core.Mcp;

/// <summary>[S-9963] A JSON-RPC 2.0 id: a string or a whole number, never null, written back as it came.</summary>
public readonly record struct JsonRpcId
{
    private JsonRpcId(string? text, long number)
    {
        Text = text;
        Number = number;
    }

    /// <summary>The id when it was a string; null when it was a number.</summary>
    public string? Text { get; }

    /// <summary>The id when it was a number.</summary>
    public long Number { get; }

    public static JsonRpcId Of(string text) => new(text ?? throw new ArgumentNullException(nameof(text)), 0);

    public static JsonRpcId Of(long number) => new(null, number);

    public void WriteTo(Utf8JsonWriter writer, string name)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (Text is { } text)
        {
            writer.WriteString(name, text);
        }
        else
        {
            writer.WriteNumber(name, Number);
        }
    }

    public override string ToString() => Text ?? Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>[S-9963] The JSON-RPC 2.0 error codes the server answers with.</summary>
public static class JsonRpcCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
}

/// <summary>[S-9963] One message a client sent, as parsed at the trust boundary.</summary>
public abstract record JsonRpcIncoming;

/// <summary>A request: it is answered.</summary>
/// <param name="Params">The params object, detached from the parsed document; null when there were none.</param>
public sealed record JsonRpcCall(JsonRpcId Id, string Method, JsonElement? Params) : JsonRpcIncoming;

/// <summary>A notification: never answered.</summary>
public sealed record JsonRpcNotification(string Method) : JsonRpcIncoming;

/// <summary>
/// A response from the client (a result or an error with an id). The server never sends a request,
/// so there is nothing it answers; it is accepted and dropped.
/// </summary>
public sealed record JsonRpcClientReply(JsonRpcId Id) : JsonRpcIncoming;

/// <summary>A message refused: the error to answer with, under its id where one could be read.</summary>
public sealed record JsonRpcRejected(JsonRpcId? Id, int Code, string Message) : JsonRpcIncoming;

/// <summary>
/// [S-9963] JSON-RPC 2.0 at the trust boundary: one message per body, parsed into a typed shape or
/// refused with the code the specification gives; and the answers, written without reflection.
/// </summary>
public static class JsonRpc
{
    /// <summary>[S-9964] The longest body read: past this the transport refuses it before any parse.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    /// <summary>[S-9964] The deepest nesting a message may have.</summary>
    public const int MaxDepth = 16;

    /// <summary>[S-9964] The longest method name.</summary>
    public const int MaxMethodLength = 128;

    /// <summary>[S-9964] The longest string id.</summary>
    public const int MaxIdLength = 256;

    private static readonly JsonDocumentOptions Options = new()
    {
        MaxDepth = MaxDepth,
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
    };

    /// <summary>
    /// [S-9963] [S-9964] Parse one message. A body over <see cref="MaxBodyBytes"/>, not UTF-8 JSON or
    /// nested past <see cref="MaxDepth"/> is a parse error; a batch (an array: MCP 2025-06-18 has
    /// none), a missing or wrong <c>"jsonrpc"</c>, a null, fractional or overlong id, a method that
    /// is not a short string or params that are not an object or array are an invalid request.
    /// </summary>
    public static JsonRpcIncoming Parse(ReadOnlySpan<byte> body)
    {
        if (body.Length > MaxBodyBytes)
        {
            return new JsonRpcRejected(null, JsonRpcCodes.ParseError, "The message is too large.");
        }

        JsonDocument document;
        try
        {
            // The span is copied: the document must not outlive the caller's buffer.
            document = JsonDocument.Parse(body.ToArray(), Options);
        }
        catch (JsonException)
        {
            return new JsonRpcRejected(null, JsonRpcCodes.ParseError, "The message is not valid JSON.");
        }

        using (document)
        {
            return Read(document.RootElement);
        }
    }

    private static JsonRpcIncoming Read(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return new JsonRpcRejected(null, JsonRpcCodes.InvalidRequest, "Batches are not supported.");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return new JsonRpcRejected(null, JsonRpcCodes.InvalidRequest, "A message is a JSON object.");
        }

        // The id first, so a refusal can be answered under it.
        JsonRpcId? id = null;
        var hasId = root.TryGetProperty("id", out var idElement);
        if (hasId)
        {
            switch (idElement.ValueKind)
            {
                case JsonValueKind.String:
                    var text = idElement.GetString()!;
                    if (text.Length > MaxIdLength)
                    {
                        return new JsonRpcRejected(null, JsonRpcCodes.InvalidRequest, "The id is too long.");
                    }

                    id = JsonRpcId.Of(text);
                    break;
                case JsonValueKind.Number when idElement.TryGetInt64(out var number):
                    id = JsonRpcId.Of(number);
                    break;
                default:
                    return new JsonRpcRejected(null, JsonRpcCodes.InvalidRequest, "The id must be a string or a whole number.");
            }
        }

        if (!root.TryGetProperty("jsonrpc", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "2.0")
        {
            return new JsonRpcRejected(id, JsonRpcCodes.InvalidRequest, "Only JSON-RPC 2.0 is spoken.");
        }

        if (!root.TryGetProperty("method", out var methodElement))
        {
            // No method: a response, which needs an id and exactly one of result or error.
            var hasResult = root.TryGetProperty("result", out _);
            var hasError = root.TryGetProperty("error", out _);
            return id is { } replyId && hasResult != hasError
                ? new JsonRpcClientReply(replyId)
                : new JsonRpcRejected(id, JsonRpcCodes.InvalidRequest, "A message needs a method.");
        }

        if (methodElement.ValueKind != JsonValueKind.String || methodElement.GetString() is not { Length: > 0 and <= MaxMethodLength } method)
        {
            return new JsonRpcRejected(id, JsonRpcCodes.InvalidRequest, "The method must be a short string.");
        }

        JsonElement? parameters = null;
        if (root.TryGetProperty("params", out var paramsElement))
        {
            if (paramsElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                return new JsonRpcRejected(id, JsonRpcCodes.InvalidRequest, "Params must be an object or an array.");
            }

            parameters = paramsElement.Clone();
        }

        return id is { } callId ? new JsonRpcCall(callId, method, parameters) : new JsonRpcNotification(method);
    }

    /// <summary>[S-9963] A result: <paramref name="writeResult"/> writes the result's value (an object).</summary>
    public static byte[] Result(JsonRpcId id, Action<Utf8JsonWriter> writeResult)
    {
        ArgumentNullException.ThrowIfNull(writeResult);
        return Write(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            id.WriteTo(writer, "id");
            writer.WritePropertyName("result");
            writeResult(writer);
        });
    }

    /// <summary>[S-9963] An error, under the id when there is one (null otherwise, as the specification says).</summary>
    public static byte[] Error(JsonRpcId? id, int code, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return Write(writer =>
        {
            writer.WriteString("jsonrpc", "2.0");
            if (id is { } known)
            {
                known.WriteTo(writer, "id");
            }
            else
            {
                writer.WriteNull("id");
            }

            writer.WriteStartObject("error");
            writer.WriteNumber("code", code);
            writer.WriteString("message", message);
            writer.WriteEndObject();
        });
    }

    /// <summary>One JSON object, written by <paramref name="body"/>, as UTF-8 bytes.</summary>
    public static byte[] Write(Action<Utf8JsonWriter> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>A body as text, for tests and the log.</summary>
    internal static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
