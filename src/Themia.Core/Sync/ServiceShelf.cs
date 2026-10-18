using Themia.Sdk.Kit;

namespace Themia.Core.Sync;

/// <summary>
/// [S-9926] One account's kit-service data on this PC: the merged items of each entry (one per
/// query the widgets read) and, for a service read through its sync token, the replica with its token
/// ([S-9907]). Sealed like every shelf ([S-2602]), one per account under the one <c>service</c> kind,
/// purged on sign-out ([S-2607]) and swept with the rest ([S-2665]). Validated on load by
/// <see cref="ServiceShelves.Validate"/>: ids once, bounds, links re-checked against the service's hosts.
/// </summary>
public sealed class ServiceShelf : ISyncShelf
{
    public const int Version = 1;

    public int SchemaVersion { get; set; } = Version;

    /// <summary>The account id.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>The provider the account is of, checked against the account on load.</summary>
    public string Provider { get; set; } = string.Empty;

    public List<ServiceShelfEntry> Entries { get; set; } = [];

    public List<ServiceShelfReplica> Replicas { get; set; } = [];
}

/// <summary>What one query last answered for the account.</summary>
public sealed class ServiceShelfEntry
{
    public string Key { get; set; } = string.Empty;

    public DateTimeOffset FetchedAt { get; set; }

    public List<StoredKitItem> Items { get; set; } = [];

    public StoredKitSummary? Summary { get; set; }
}

/// <summary>A replica as the provider's own JSON, with its token and its last full read.</summary>
public sealed class ServiceShelfReplica
{
    public string Key { get; set; } = string.Empty;

    public string Json { get; set; } = string.Empty;

    public string? SyncToken { get; set; }

    public DateTimeOffset FetchedAt { get; set; }

    public DateTimeOffset? FullAt { get; set; }
}

/// <summary>A <see cref="KitItem"/> as the shelf writes it.</summary>
public sealed class StoredKitItem
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public string? Preview { get; set; }

    public KitLeadKind Lead { get; set; }

    public string? LeadValue { get; set; }

    public KitTone LeadTone { get; set; }

    public DateTimeOffset? At { get; set; }

    public DateOnly? Day { get; set; }

    public KitTone Tone { get; set; }

    public bool Emphasis { get; set; }

    public List<StoredKitBadge>? Badges { get; set; }

    public string? Link { get; set; }

    public string? GroupKey { get; set; }

    public string? GroupHeading { get; set; }

    public DateOnly? GroupDay { get; set; }

    public static StoredKitItem From(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new StoredKitItem
        {
            Id = item.Id,
            Title = item.Title,
            Subtitle = item.Subtitle,
            Preview = item.Preview,
            Lead = item.Lead.Kind,
            LeadValue = item.Lead.Value,
            LeadTone = item.Lead.Tone,
            At = item.When?.At,
            Day = item.When?.Day,
            Tone = item.Tone,
            Emphasis = item.Emphasis,
            Badges = item.Badges is { Count: > 0 } badges ? [.. badges.Select(b => new StoredKitBadge { Text = b.Text, Tone = b.Tone })] : null,
            Link = item.Link?.AbsoluteUri,
            GroupKey = item.Group?.Key,
            GroupHeading = item.Group?.Heading,
            GroupDay = item.Group?.Day,
        };
    }

    /// <summary>The item, or null when it is not one (no id); a link that does not parse is left out.</summary>
    public KitItem? ToItem()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            return null;
        }

        KitWhen? when = At is { } at ? KitWhen.Instant(at) : Day is { } day ? KitWhen.OnDay(day) : null;
        return new KitItem(
            Id,
            Title ?? string.Empty,
            Subtitle,
            Preview,
            new KitLead(Enum.IsDefined(Lead) ? Lead : KitLeadKind.None, LeadValue, Enum.IsDefined(LeadTone) ? LeadTone : KitTone.Neutral),
            when,
            Enum.IsDefined(Tone) ? Tone : KitTone.Neutral,
            Emphasis,
            Badges?.Where(b => !string.IsNullOrEmpty(b.Text)).Select(b => new KitBadge(b.Text, Enum.IsDefined(b.Tone) ? b.Tone : KitTone.Neutral)).ToList(),
            Uri.TryCreate(Link, UriKind.Absolute, out var link) ? link : null,
            GroupKey is { Length: > 0 } group ? new KitGroup(group, GroupHeading, GroupDay) : null);
    }
}

public sealed class StoredKitBadge
{
    public string Text { get; set; } = string.Empty;

    public KitTone Tone { get; set; }
}

public sealed class StoredKitSummary
{
    public string Text { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public double? Fraction { get; set; }

    public KitTone Tone { get; set; }

    public static StoredKitSummary? From(KitSummary? summary)
        => summary is null ? null : new StoredKitSummary { Text = summary.Text, Detail = summary.Detail, Fraction = summary.Fraction, Tone = summary.Tone };

    public KitSummary ToSummary() => new(Text ?? string.Empty, Detail, Fraction, Enum.IsDefined(Tone) ? Tone : KitTone.Neutral);
}

/// <summary>[S-9926] Reading a service shelf back: what survives the item rules, for the account and provider it says.</summary>
public static class ServiceShelves
{
    /// <summary>The most entries and replicas one account's shelf keeps (the widgets' queries are far fewer).</summary>
    public const int MaxEntries = 64;

    /// <summary>
    /// The shelf's entries as items through the item rules ([S-7002]: ids once, bounds, links on the
    /// service's hosts), each entry cut to <paramref name="itemBound"/>; entries without a key or seen
    /// twice dropped. Null when the shelf is not this account's or this provider's.
    /// </summary>
    public static IReadOnlyList<(string Key, DateTimeOffset At, IReadOnlyList<KitItem> Items, KitSummary? Summary)>? Validate(
        ServiceShelf shelf, string owner, string provider, IReadOnlyCollection<string> webHosts, int itemBound, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(repairs);

        if (!string.Equals(shelf.Owner, owner, StringComparison.Ordinal) || !string.Equals(shelf.Provider, provider, StringComparison.Ordinal))
        {
            repairs.Add("the service shelf belongs to another account or provider");
            return null;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<(string, DateTimeOffset, IReadOnlyList<KitItem>, KitSummary?)>();
        foreach (var entry in (shelf.Entries ?? []).Take(MaxEntries))
        {
            if (entry is null || string.IsNullOrEmpty(entry.Key) || !keys.Add(entry.Key))
            {
                repairs.Add("dropped a service entry without a key or seen twice");
                continue;
            }

            var raw = (entry.Items ?? []).Select(i => i?.ToItem()).OfType<KitItem>().ToList();
            var items = KitItems.Clean(raw, webHosts);
            if (items.Count > itemBound)
            {
                items = [.. items.Take(itemBound)];
            }

            if (items.Count != (entry.Items?.Count ?? 0))
            {
                repairs.Add($"a service entry kept {items.Count} of {entry.Items?.Count ?? 0} items");
            }

            entries.Add((entry.Key, entry.FetchedAt, items, KitItems.Clean(entry.Summary?.ToSummary())));
        }

        return entries;
    }
}
