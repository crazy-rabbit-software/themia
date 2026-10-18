using System.Globalization;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Services;

/// <summary>
/// [S-9907] A service read through its own sync token: the held thing is a typed replica of the
/// account's data, not a widget's list. One replica per account is shared by every widget of the
/// provider; a widget's list is a view of it, never a request of its own. The provider writes the
/// three pure functions; the host holds the replica and its token together in the account's sealed
/// service shelf, through the provider's own JSON context.
/// </summary>
public interface IServiceReplica<TReplica, TWire>
    where TReplica : class
{
    /// <summary>A delta onto what is held: upserts by id, removals by tombstone; a full answer replaces.</summary>
    TReplica Merge(TReplica? held, TWire delta);

    /// <summary>On load: bounds, ids once, links re-checked; a bad replica is repaired, never half-used.</summary>
    TReplica Validate(TReplica replica);

    /// <summary>The token to send with the next request ("*" or the service's own for a full read when none is held).</summary>
    string? SyncToken(TReplica? held);
}

/// <summary>A widget's view of a replica: the filter, the sort and the groups, recomputed on every merge and at the day's rollover.</summary>
public interface IReplicaView<TReplica, TQuery>
    where TReplica : class
{
    KitPage View(TReplica replica, TQuery query, DateTimeOffset now, TimeSpan zoneOffset);
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
