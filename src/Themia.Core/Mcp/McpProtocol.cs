using System.Text.Json;
using Themia.Core.Diagnostics;

namespace Themia.Core.Mcp;

/// <summary>
/// [S-9962] [S-9967] The Model Context Protocol over one parsed message: <c>initialize</c> (the
/// version agreed, the server's tools capability), <c>ping</c>, <c>tools/list</c> and
/// <c>tools/call</c>; notifications and client replies are taken without an answer, any other method
/// is not found. Stateless: no session id, so every request stands on its own.
/// </summary>
public sealed class McpProtocol(McpTools tools, string serverVersion)
{
    private const string Scope = "mcp";

    /// <summary>[S-9962] The version spoken first: what a client that asks for another is offered.</summary>
    public const string Latest = "2025-06-18";

    /// <summary>[S-9962] Every version spoken.</summary>
    public static IReadOnlyList<string> Spoken { get; } = [Latest, "2025-03-26"];

    /// <summary>[S-9962] What <c>initialize</c> tells the client to tell its model.</summary>
    public const string Instructions =
        "Themia is a Windows desktop-widget app. These tools read, never change, what Themia shows: its displays, views and widgets, " +
        "notes, task lists, habits and calendar events. Data from accounts is as Themia last synced it.";

    /// <summary>
    /// [S-9962] The answer to <paramref name="incoming"/> as a JSON-RPC body, or null when none is
    /// owed (a notification, a client's reply): the transport answers 202 then.
    /// </summary>
    public async Task<byte[]?> HandleAsync(JsonRpcIncoming incoming, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        switch (incoming)
        {
            case JsonRpcRejected rejected:
                return JsonRpc.Error(rejected.Id, rejected.Code, rejected.Message);
            case JsonRpcNotification or JsonRpcClientReply:
                return null;
            case JsonRpcCall call:
                return await CallAsync(call, cancellation).ConfigureAwait(false);
            default:
                throw new ArgumentOutOfRangeException(nameof(incoming), incoming.GetType().Name, "not a parsed message");
        }
    }

    private async Task<byte[]> CallAsync(JsonRpcCall call, CancellationToken cancellation)
    {
        switch (call.Method)
        {
            case "initialize":
                return Initialize(call);
            case "ping":
                return JsonRpc.Result(call.Id, w => { w.WriteStartObject(); w.WriteEndObject(); });
            case "tools/list":
                return JsonRpc.Result(call.Id, WriteTools);
            case "tools/call":
                return await ToolCallAsync(call, cancellation).ConfigureAwait(false);
            default:
                return JsonRpc.Error(call.Id, JsonRpcCodes.MethodNotFound, $"Method not found: {call.Method}");
        }
    }

    /// <summary>[S-9962] The client's version when it is spoken, else <see cref="Latest"/>; a missing version is invalid params.</summary>
    private byte[] Initialize(JsonRpcCall call)
    {
        if (call.Params is not { ValueKind: JsonValueKind.Object } p
            || !p.TryGetProperty("protocolVersion", out var asked)
            || asked.ValueKind != JsonValueKind.String)
        {
            return JsonRpc.Error(call.Id, JsonRpcCodes.InvalidParams, "initialize needs a protocolVersion.");
        }

        var version = Agree(asked.GetString());
        return JsonRpc.Result(call.Id, w =>
        {
            w.WriteStartObject();
            w.WriteString("protocolVersion", version);
            w.WriteStartObject("capabilities");
            w.WriteStartObject("tools");
            w.WriteBoolean("listChanged", false);
            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteStartObject("serverInfo");
            w.WriteString("name", "themia");
            w.WriteString("title", "Themia");
            w.WriteString("version", serverVersion);
            w.WriteEndObject();
            w.WriteString("instructions", Instructions);
            w.WriteEndObject();
        });
    }

    /// <summary>[S-9962] The version a client asking for <paramref name="asked"/> gets.</summary>
    public static string Agree(string? asked) => asked is not null && Spoken.Contains(asked) ? asked : Latest;

    private static void WriteTools(Utf8JsonWriter w)
    {
        w.WriteStartObject();
        w.WriteStartArray("tools");
        foreach (var tool in McpTools.All)
        {
            w.WriteStartObject();
            w.WriteString("name", tool.Name);
            w.WriteString("title", tool.Title);
            w.WriteString("description", tool.Description);
            w.WritePropertyName("inputSchema");
            using (var schema = JsonDocument.Parse(tool.InputSchema))
            {
                schema.RootElement.WriteTo(w);
            }

            // [S-9967] Every tool only reads: clients may run them without asking each time.
            w.WriteStartObject("annotations");
            w.WriteBoolean("readOnlyHint", true);
            w.WriteBoolean("destructiveHint", false);
            w.WriteBoolean("idempotentHint", true);
            w.WriteBoolean("openWorldHint", false);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        w.WriteEndArray();
        w.WriteEndObject();
    }

    /// <summary>[S-9968] A tool's answer as MCP's text content; an unknown tool or bad arguments are invalid params.</summary>
    private async Task<byte[]> ToolCallAsync(JsonRpcCall call, CancellationToken cancellation)
    {
        if (call.Params is not { ValueKind: JsonValueKind.Object } p
            || !p.TryGetProperty("name", out var nameElement)
            || nameElement.ValueKind != JsonValueKind.String)
        {
            return JsonRpc.Error(call.Id, JsonRpcCodes.InvalidParams, "tools/call needs a tool name.");
        }

        JsonElement? arguments = p.TryGetProperty("arguments", out var given) ? given : null;
        McpToolResult result;
        try
        {
            result = await tools.CallAsync(nameElement.GetString()!, arguments, cancellation).ConfigureAwait(false);
        }
        catch (McpArgumentException ex)
        {
            return JsonRpc.Error(call.Id, JsonRpcCodes.InvalidParams, ex.Message);
        }
        catch (OperationCanceledException)
        {
            Log.Warn(Scope, $"the tool {nameElement.GetString()} took too long");
            return JsonRpc.Error(call.Id, JsonRpcCodes.InternalError, "Themia took too long to answer.");
        }

        return JsonRpc.Result(call.Id, w =>
        {
            w.WriteStartObject();
            w.WriteStartArray("content");
            w.WriteStartObject();
            w.WriteString("type", "text");
            w.WriteString("text", result.Text);
            w.WriteEndObject();
            w.WriteEndArray();
            w.WriteBoolean("isError", result.IsError);
            w.WriteEndObject();
        });
    }
}
