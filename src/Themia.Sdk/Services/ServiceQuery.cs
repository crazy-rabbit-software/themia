using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Localization;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Services;

/// <summary>The verbs a kit service is asked with: it only ever reads.</summary>
public enum HttpVerb
{
    Get,

    /// <summary>A read framed as a POST (GraphQL, a sync endpoint); never a write.</summary>
    Post,
}

/// <summary>
/// [S-9903] One request a service's rules built: where, what body, which rate-limit bucket it counts
/// against, and the key it is held under (everything that changes the answer, never the token).
/// </summary>
public sealed record ServiceRequest(HttpVerb Verb, Uri Address, ServiceBody? Body, string Bucket, string Key);

/// <summary>A request body, built by the rules through their own JSON context or <see cref="FormBody"/>.</summary>
public sealed record ServiceBody(ReadOnlyMemory<byte> Utf8, string ContentType);

/// <summary>
/// [S-9903] What came back for a request: the status, the headers (keys compared without case) and
/// the body, bounded by the host's client.
/// </summary>
public sealed record ServiceReply(int Status, IReadOnlyDictionary<string, string> Headers, ReadOnlyMemory<byte> Body)
{
    public bool IsSuccess => Status is >= 200 and < 300;

    public bool IsNotModified => Status == 304;

    /// <summary>A header's value, or null.</summary>
    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>Where the next page starts: a closed set.</summary>
public abstract record PageAt
{
    private PageAt()
    {
    }

    /// <summary>The first page.</summary>
    public static PageAt First { get; } = new FirstPage();

    public sealed record FirstPage : PageAt;

    /// <summary>A cursor the last page's map returned (Relay's <c>endCursor</c>, a <c>next_cursor</c>, a page token).</summary>
    public sealed record Cursor(string Value) : PageAt;

    /// <summary>An item offset (services without cursors).</summary>
    public sealed record Offset(int Value) : PageAt;

    /// <summary>The address the last reply's <c>Link: rel="next"</c> named.</summary>
    public sealed record Address(Uri Next) : PageAt;
}

/// <summary>
/// [S-9904] One page's worth of a query, mapped by the service's rules: its items (before the item
/// rules), the next cursor, the summary line, the ids a delta removed, and, when the items are empty
/// for a reason the rules know, the sentence that says so.
/// </summary>
public sealed record KitChunk(
    IReadOnlyList<KitItem> Items,
    string? NextCursor = null,
    KitSummary? Summary = null,
    IReadOnlyList<string>? Removed = null,
    LocalText? EmptySentence = null)
{
    public static KitChunk Empty { get; } = new([]);
}

/// <summary>
/// [S-9904] A widget's list as a view of what a service holds: its items, the summary, and the
/// sentence for an empty list the rules know the reason of ("No active cycle").
/// </summary>
public sealed record KitPage(IReadOnlyList<KitItem> Items, KitSummary? Summary = null, LocalText? EmptySentence = null);

/// <summary>
/// [S-9903] What the kit asks of a service's query, whatever its wire type: the kit's whole contract
/// with a service's rules. The host's one client builds each page's request through
/// <see cref="Build"/>, sends it, classifies the reply, and hands a success to <see cref="Answer"/>.
/// </summary>
public interface IServiceQuery
{
    /// <summary>Everything that changes the answer (the list, its inputs, the count), never the token.</summary>
    string Key { get; }

    PagingKind Paging { get; }

    FreshnessHints Freshness { get; }

    /// <summary>Earlier requests whose answers this one needs (a default branch, the projects' names); at most three.</summary>
    IReadOnlyList<IServiceStep> Steps { get; }

    /// <summary>The page's request; <paramref name="fromSteps"/> holds what the steps found.</summary>
    ServiceRequest Build(PageAt page, IReadOnlyDictionary<string, string> fromSteps);

    /// <summary>
    /// A successful page's body as items, or null with <paramref name="problem"/> (the first path that
    /// failed) when it is not the wire shape: a bad response, never half-built. For GraphQL the body
    /// is the <c>data</c> member, already unwrapped.
    /// </summary>
    KitChunk? Answer(ReadOnlyMemory<byte> body, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now, out string? problem);
}

/// <summary>
/// [S-9903] A query over a source-generated wire type: the provider writes <see cref="Map"/> and the
/// kit parses with <see cref="Wire"/> (Native AOT: no reflection).
/// </summary>
public interface IServiceQuery<TWire> : IServiceQuery
{
    JsonTypeInfo<TWire> Wire { get; }

    KitChunk Map(TWire wire, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now);

    KitChunk? IServiceQuery.Answer(ReadOnlyMemory<byte> body, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now, out string? problem)
        => ServiceWire.TryParse(body.Span, Wire, out var wire, out problem) ? Map(wire!, fromSteps, now) : null;
}

/// <summary>
/// [S-9903] An earlier request of the same service whose answer is a table of named values for the
/// next one: fetched, held, throttled and classified like any entry, and shared by every query that
/// names the same <see cref="Key"/>.
/// </summary>
public interface IServiceStep
{
    string Key { get; }

    FreshnessHints Freshness { get; }

    ServiceRequest Build();

    /// <summary>The named values, or null with <paramref name="problem"/> when the body is not the wire shape.</summary>
    IReadOnlyDictionary<string, string>? Answer(ReadOnlyMemory<byte> body, out string? problem);
}

/// <summary>Parsing a body through a source-generated contract: a value, or why not (the first path that failed).</summary>
public static class ServiceWire
{
    public static bool TryParse<T>(ReadOnlySpan<byte> utf8, JsonTypeInfo<T> contract, out T? value, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(contract);

        value = default;
        if (utf8.IsEmpty)
        {
            problem = "an empty body";
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize(utf8, contract);
            problem = value is null ? "a null document" : null;
            return value is not null;
        }
        catch (JsonException ex)
        {
            problem = $"not the expected document at {ex.Path ?? "$"}";
            return false;
        }
    }
}

/// <summary>
/// [S-9916] Who a token belongs to, for a service whose identity is a query of its own (a GraphQL
/// <c>viewer</c>, a sync of the user): asked after every sign-in and on a pasted token before it is
/// kept. A token whose identity request fails is not saved.
/// </summary>
public interface IServiceIdentity
{
    ServiceRequest Build();

    /// <summary>The account it names, or null with <paramref name="problem"/>. For GraphQL the body is the <c>data</c> member.</summary>
    ServiceIdentity? Answer(ReadOnlyMemory<byte> body, out string? problem);
}
