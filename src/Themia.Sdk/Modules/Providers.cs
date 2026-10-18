using Themia.Core.Accounts;

namespace Themia.Sdk;

/// <summary>
/// [S-6910] A provider module: an account or data service widgets read from, declared with
/// <c>[assembly: ThemiaProvider(id, typeof(M))]</c> (docs/WIDGET-KIT.md section 4.3). It has no UI.
/// A member is added here when a table of the host's moves onto it, and the table goes in the same
/// commit (the kit's sign-in strategies, its service descriptor and its build secret come with
/// section 11.2's steps).
/// </summary>
public interface IProviderModule
{
    /// <summary>Its id ([S-6900]): the name an account of it is written under in <c>accounts.json</c> ([S-501]).</summary>
    ProviderId Id { get; }

    /// <summary>What the Accounts page and an account's subtitle call it.</summary>
    LocalText Label { get; }

    /// <summary>[S-502] What its accounts can feed a widget.</summary>
    FeatureSet Features { get; }

    /// <summary>How its accounts prove themselves on every request.</summary>
    AccountAuth Auth { get; }

    /// <summary>Its place in Settings › Accounts › Add account (lower first).</summary>
    int AddRank { get; }

    /// <summary>
    /// [S-3780] Its own row in Settings › Widgets › Updates, or null when its accounts follow the
    /// host's Accounts row (every account provider of today).
    /// </summary>
    CadenceRow? Cadence { get; }

    /// <summary>
    /// [S-9910] How its accounts sign in, in the order the Accounts page offers them: the members of
    /// the closed set the host runs (<c>ServiceSignIn</c>). None for a provider whose accounts the host
    /// adds through a password form (IMAP, CalDAV, CardDAV).
    /// </summary>
    Services.ProviderSignIn SignIn => Services.ProviderSignIn.None;

    /// <summary>[S-9902] Its kit service, or null for a provider whose data the host reads itself (mail, calendars, contacts, tasks).</summary>
    Services.ServiceDescriptor? Service => null;

    /// <summary>
    /// [S-9917] Its key in <c>THEMIA_CLIENT_SECRETS</c> when a sign-in member's secret is
    /// <see cref="Services.ClientSecretRule.BuildTime"/>; null otherwise.
    /// </summary>
    string? BuildSecret => null;
}

/// <summary>
/// The features the host itself serves from a signed-in account ([S-502]); a kit service's feature
/// is its provider's id (docs/WIDGET-KIT.md section 5.7).
/// </summary>
public static class HostFeatures
{
    public static readonly FeatureId Mail = new("mail");
    public static readonly FeatureId Calendar = new("calendar");
    public static readonly FeatureId Contacts = new("contacts");
    public static readonly FeatureId Tasks = new("tasks");
}

/// <summary>
/// An immutable set of <see cref="FeatureId"/>s: what an account offers, what the signed-in
/// accounts offer together. Equal when the members are, whatever the order they were given in.
/// </summary>
public sealed class FeatureSet : IEquatable<FeatureSet>
{
    private readonly FeatureId[] _members;

    private FeatureSet(FeatureId[] members) => _members = members;

    public static FeatureSet None { get; } = new([]);

    public static FeatureSet Of(params ReadOnlySpan<FeatureId> features) => Of((IEnumerable<FeatureId>)features.ToArray());

    public static FeatureSet Of(IEnumerable<FeatureId> features)
    {
        ArgumentNullException.ThrowIfNull(features);
        var members = features.Where(f => f.Value is not null).Distinct().OrderBy(f => f.Value, StringComparer.Ordinal).ToArray();
        return members.Length == 0 ? None : new FeatureSet(members);
    }

    public IReadOnlyList<FeatureId> Members => _members;

    public bool IsEmpty => _members.Length == 0;

    public bool Has(FeatureId feature) => Array.IndexOf(_members, feature) >= 0;

    public FeatureSet Without(FeatureId feature) => Of(_members.Where(m => m != feature));

    public FeatureSet Union(FeatureSet other) => Of(_members.Concat((other ?? throw new ArgumentNullException(nameof(other)))._members));

    public static FeatureSet operator |(FeatureSet left, FeatureSet right) => (left ?? throw new ArgumentNullException(nameof(left))).Union(right);

    public static implicit operator FeatureSet(FeatureId feature) => Of(feature);

    public bool Equals(FeatureSet? other) => other is not null && _members.AsSpan().SequenceEqual(other._members);

    public override bool Equals(object? obj) => Equals(obj as FeatureSet);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var member in _members)
        {
            hash.Add(member);
        }

        return hash.ToHashCode();
    }

    public override string ToString() => _members.Length == 0 ? "(none)" : string.Join(", ", _members);
}

/// <summary>
/// [S-3780] A row of Settings › Widgets › Updates: one shared refresh cadence for every widget of a
/// kind of source, its offered minutes (a closed list; a stored value snaps to the nearest) and its
/// default.
/// </summary>
public sealed record CadenceRow
{
    public required CadenceId Id { get; init; }

    public required LocalText Label { get; init; }

    /// <summary>Segoe Fluent Icons code point of the row.</summary>
    public required string Glyph { get; init; }

    /// <summary>The offered minutes, ascending.</summary>
    public required IReadOnlyList<int> Choices { get; init; }

    /// <summary>What a new state, or one without this row's entry, uses; one of <see cref="Choices"/>.</summary>
    public required int DefaultMinutes { get; init; }

    /// <summary>Its place on the page (lower first).</summary>
    public required int Rank { get; init; }
}
