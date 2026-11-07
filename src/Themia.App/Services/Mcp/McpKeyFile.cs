using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using Themia.App.Services.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Mcp;
using Themia.Core.Storage;

namespace Themia.App.Services.Mcp;

/// <summary>
/// [S-9977] This install's server address at rest: <c>mcp.json</c> beside the state file, holding
/// the port and the token sealed with DPAPI for this Windows user (<see cref="Dpapi"/>). Not the
/// settings file, so no backup carries it. Absent, damaged, plain-text or sealed for someone else,
/// it is made again, loudly; the token is never written in the clear and never logged.
/// </summary>
internal sealed class McpKeyFile(string path)
{
    private const string Scope = "mcp";

    public const string FileName = "mcp.json";

    public string Path { get; } = path;

    /// <summary>The file beside <paramref name="statePath"/>.</summary>
    public static string PathBeside(string statePath)
        => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(statePath) ?? string.Empty, FileName);

    /// <summary>
    /// The stored address, or a new token on port 0 (any free port: the caller saves the one it got)
    /// when there is none that can be read here.
    /// </summary>
    public McpAddress Load()
    {
        if (!File.Exists(Path))
        {
            return new McpAddress(0, McpAccess.NewToken());
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(Path));
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("port", out var portElement) && portElement.TryGetInt32(out var port) && McpAccess.IsPort(port)
                && root.TryGetProperty("token", out var tokenElement) && tokenElement.ValueKind == JsonValueKind.String)
            {
                var opened = Dpapi.Open(tokenElement.GetString());
                if (opened is { State: SecretState.Open, Value: var token } && McpAccess.IsToken(token))
                {
                    return new McpAddress(port, token!);
                }

                Log.Warn(Scope, $"{FileName}'s token cannot be opened here ({opened.State}); a new one is made");
                return new McpAddress(port, McpAccess.NewToken());
            }

            Log.Warn(Scope, $"{FileName} is not a server address; a new one is made");
        }
        catch (JsonException ex)
        {
            Log.Warn(Scope, $"{FileName} is not valid JSON; a new address is made", ex);
        }
        catch (IOException ex)
        {
            Log.Warn(Scope, $"{FileName} could not be read; a new address is made", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warn(Scope, $"{FileName} could not be read; a new address is made", ex);
        }

        return new McpAddress(0, McpAccess.NewToken());
    }

    /// <summary>
    /// Store <paramref name="address"/> atomically (temp file, then rename). Throws
    /// <see cref="CryptographicException"/> when Windows cannot seal the token and <see cref="IOException"/>
    /// when it cannot be written: then nothing was written.
    /// </summary>
    public void Save(McpAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("port", address.Port);
            writer.WriteString("token", Dpapi.Seal(address.Token));
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path) ?? ".");
        var temp = Path + ".tmp";
        File.WriteAllBytes(temp, buffer.WrittenSpan.ToArray());
        AtomicFile.Replace(temp, Path);
    }
}
