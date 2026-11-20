using System.Text.Json;
using Themia.Core.Model;

namespace Themia.Core.Storage;

/// <summary>
/// [S-41] A state document, taken as bytes on the thread that owns the live state (the only thread that changes it).
/// What the store is handed and writes: it holds nothing the owner goes on changing, so a write on another thread
/// never reads a widget list mid-change or a rect half moved.
/// </summary>
public sealed class StateSnapshot
{
    private readonly byte[] _json;

    private StateSnapshot(byte[] json) => _json = json;

    /// <summary>The state as it is now, serialised here: call it on the thread that owns <paramref name="state"/>.</summary>
    public static StateSnapshot Take(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new(JsonSerializer.SerializeToUtf8Bytes(state, StateJson.AppState));
    }

    /// <summary>The document, as the file holds it.</summary>
    internal ReadOnlyMemory<byte> Json => _json;

    /// <summary>[S-889] The state it was taken from, as it was then: a new object graph, the same document.</summary>
    internal AppState Read()
        => JsonSerializer.Deserialize(_json, StateJson.AppState)
            ?? throw new InvalidOperationException("a state snapshot read back as no state");
}
