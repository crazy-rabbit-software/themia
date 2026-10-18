using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Services;

/// <summary>
/// [S-9925] What a kit widget wants the service sync to keep fresh: its provider, its query (or its
/// view of the provider's replica, [S-9907]), the accounts it reads (empty: every account offering
/// the provider's feature, [S-7041]) and how many items it shows. Added by the widget's config
/// (<c>IInterests</c>); the host reads them back and keeps one entry per provider, account and query.
/// </summary>
public sealed record ServiceInterest
{
    public ServiceInterest(ProviderId provider, IServiceQuery query, IReadOnlyList<string> accountIds, int count)
    {
        ArgumentNullException.ThrowIfNull(query);
        Provider = provider;
        Query = query;
        AccountIds = accountIds ?? [];
        Count = Math.Max(1, count);
    }

    public ServiceInterest(ProviderId provider, IReplicaView view, IReadOnlyList<string> accountIds, int count)
    {
        ArgumentNullException.ThrowIfNull(view);
        Provider = provider;
        View = view;
        AccountIds = accountIds ?? [];
        Count = Math.Max(1, count);
    }

    public ProviderId Provider { get; }

    /// <summary>The query, for a service read by request; null for a view of a replica.</summary>
    public IServiceQuery? Query { get; }

    /// <summary>The view, for a service read through its sync token; null for a query.</summary>
    public IReplicaView? View { get; }

    public IReadOnlyList<string> AccountIds { get; }

    public int Count { get; }

    /// <summary>The query's or view's key: what an entry is held under beside its account.</summary>
    public string QueryKey => Query?.Key ?? ("view:" + View!.Key);

    /// <summary>Everything that changes what is synced, for the interests' key.</summary>
    public string Key()
        => string.Create(CultureInfo.InvariantCulture, $"{Provider.Value}|{string.Join(',', AccountIds)}|{QueryKey}|{Count}");
}

/// <summary>
/// [S-9907] A service read through its own sync token, type-erased for the host: the replica is held
/// as the provider's own JSON (written and read through its context), one per account, shared by every
/// widget of the provider. The provider implements it, usually through <see cref="ServiceReplica{TReplica, TWire}"/>.
/// </summary>
public interface IServiceReplicaQuery
{
    /// <summary>The replica's key: one per provider (and account, beside it).</summary>
    string Key { get; }

    /// <summary>A full read at least this often, whatever the deltas say.</summary>
    TimeSpan FullReadEvery { get; }

    /// <summary>The sync request: <paramref name="syncToken"/> null asks for everything (its own bucket).</summary>
    ServiceRequest Build(string? syncToken);

    /// <summary>
    /// A successful answer onto what is held, or null with <paramref name="problem"/> when the body is
    /// not the wire shape. A delta onto nothing held is not merged: the answer says the token was rejected.
    /// </summary>
    ReplicaMerge? Merge(string? heldJson, ReadOnlyMemory<byte> body, DateTimeOffset now, out string? problem);

    /// <summary>On load: the replica repaired to its bounds, or null with <paramref name="problem"/> when it cannot be used.</summary>
    string? Validate(string json, out string? problem);
}

/// <summary>A merged replica: its JSON, the token to send next, whether the answer was a full read, and whether the service refused the token sent.</summary>
public sealed record ReplicaMerge(string Json, string? SyncToken, bool Full, bool TokenRejected = false);

/// <summary>[S-9907] A widget's list as a view of a replica: the filter, the sort and the groups, never a request.</summary>
public interface IReplicaView
{
    IServiceReplicaQuery Replica { get; }

    /// <summary>What changes the view (the list, its horizon).</summary>
    string Key { get; }

    KitPage View(string replicaJson, DateTimeOffset now);
}

/// <summary>
/// [S-9907] A replica over the provider's own types: it writes the three pure functions and the
/// request; this base turns them into the host's erased contract through its source-generated JSON.
/// </summary>
public abstract class ServiceReplica<TReplica, TWire> : IServiceReplicaQuery
    where TReplica : class
{
    protected abstract JsonTypeInfo<TReplica> ReplicaJson { get; }

    protected abstract JsonTypeInfo<TWire> WireJson { get; }

    public abstract string Key { get; }

    public virtual TimeSpan FullReadEvery => TimeSpan.FromDays(1);

    public abstract ServiceRequest Build(string? syncToken);

    /// <summary>A delta onto what is held (upserts by id, removals by tombstone); a full answer replaces.</summary>
    protected abstract TReplica Merge(TReplica? held, TWire delta, DateTimeOffset now);

    /// <summary>On load: bounds, ids once, links re-checked.</summary>
    protected abstract TReplica Validate(TReplica replica);

    /// <summary>The token the merged replica holds for the next request.</summary>
    protected abstract string? SyncTokenOf(TReplica replica);

    /// <summary>Whether <paramref name="delta"/> is a full read.</summary>
    protected abstract bool IsFull(TWire delta);

    public ReplicaMerge? Merge(string? heldJson, ReadOnlyMemory<byte> body, DateTimeOffset now, out string? problem)
    {
        if (!ServiceWire.TryParse(body.Span, WireJson, out var delta, out problem))
        {
            return null;
        }

        var held = heldJson is null ? null : Read(heldJson, out _);
        var full = IsFull(delta!);
        if (!full && held is null)
        {
            // A delta needs something to apply to: ask for everything.
            return new ReplicaMerge(string.Empty, null, Full: false, TokenRejected: true);
        }

        var merged = Validate(Merge(held, delta!, now));
        return new ReplicaMerge(JsonSerializer.Serialize(merged, ReplicaJson), SyncTokenOf(merged), full);
    }

    public string? Validate(string json, out string? problem)
    {
        var replica = Read(json, out problem);
        return replica is null ? null : JsonSerializer.Serialize(Validate(replica), ReplicaJson);
    }

    /// <summary>The held replica as the provider's type, for its views.</summary>
    public TReplica? Read(string json, out string? problem)
    {
        try
        {
            var replica = JsonSerializer.Deserialize(json, ReplicaJson);
            problem = replica is null ? "a null replica" : null;
            return replica;
        }
        catch (JsonException ex)
        {
            problem = $"not the replica at {ex.Path ?? "$"}";
            return null;
        }
    }
}

/// <summary>
/// [S-9909] Merging what several accounts (or several entries) answered into one list: newest first
/// (an instant, else a day, else the source's order), the same item seen through two accounts once
/// (the first account's copy), cut to the widget's count.
/// </summary>
public static class KitMerge
{
    public static IReadOnlyList<KitItem> Merge(IReadOnlyList<IReadOnlyList<KitItem>> perSource, int count)
    {
        ArgumentNullException.ThrowIfNull(perSource);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var all = new List<(KitItem Item, int Source, int Index)>();
        for (var source = 0; source < perSource.Count; source++)
        {
            var items = perSource[source] ?? [];
            for (var index = 0; index < items.Count; index++)
            {
                if (items[index] is { } item && seen.Add(item.Id))
                {
                    all.Add((item, source, index));
                }
            }
        }

        return [.. all
            .OrderByDescending(e => Instant(e.Item))
            .ThenBy(e => e.Index)
            .ThenBy(e => e.Source)
            .Take(Math.Max(0, count))
            .Select(e => e.Item)];
    }

    // An item without a time sorts after every item with one; a day counts from its start.
    private static long Instant(KitItem item) => item.When switch
    {
        { At: { } at } => at.UtcTicks,
        { Day: { } day } => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks,
        _ => long.MinValue,
    };
}

/// <summary>
/// [S-9917] The build-time client secrets (<c>THEMIA_CLIENT_SECRETS</c>, <c>google=…;todoist=…</c>):
/// one parsing rule for the build and the app. A key is a provider's <c>BuildSecret</c> (a module id,
/// [S-6900]); a value is what a client secret is made of (letters, digits, <c>_ - .</c>, 8 to 256
/// characters). An entry that breaks the rule fails the whole value, so a typo never ships half
/// the secrets.
/// </summary>
public static class ClientSecretRules
{
    public const int MinLength = 8;
    public const int MaxLength = 256;

    /// <summary>The secrets by key, or null with <paramref name="problem"/> when the value breaks the rule. Empty for no value at all.</summary>
    public static IReadOnlyDictionary<string, string>? Parse(string? value, out string? problem)
    {
        problem = null;
        var secrets = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
        {
            return secrets;
        }

        foreach (var raw in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = raw.IndexOf('=', StringComparison.Ordinal);
            if (equals <= 0)
            {
                problem = "an entry without a key (write key=secret)";
                return null;
            }

            var key = raw[..equals].Trim();
            var secret = raw[(equals + 1)..].Trim();

            if (!IsKey(key))
            {
                problem = $"'{key}' is not a provider key";
                return null;
            }

            if (!IsSecret(secret))
            {
                problem = $"the secret for '{key}' holds characters or a length a client secret does not have";
                return null;
            }

            if (!secrets.TryAdd(key, secret))
            {
                problem = $"'{key}' is given twice";
                return null;
            }
        }

        return secrets;
    }

    /// <summary>Whether <paramref name="key"/> may name a build secret: a module id ([S-6900]).</summary>
    public static bool IsKey(string? key)
        => key is { Length: > 0 and <= 64 } && key[0] is >= 'a' and <= 'z' && key.All(char.IsAsciiLetterOrDigit);

    public static bool IsSecret(string? secret)
        => secret is { Length: >= MinLength and <= MaxLength } && secret.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.');

    /// <summary>For the log: how many secrets a build holds, by key, never a value.</summary>
    public static string Describe(IReadOnlyDictionary<string, string> secrets)
    {
        ArgumentNullException.ThrowIfNull(secrets);
        return secrets.Count == 0
            ? "none"
            : string.Join(", ", secrets.Keys.Order(StringComparer.Ordinal)) + " (" + secrets.Count.ToString(CultureInfo.InvariantCulture) + ")";
    }
}
