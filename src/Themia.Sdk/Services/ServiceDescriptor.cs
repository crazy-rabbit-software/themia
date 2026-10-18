using Themia.Core.Widgets;

namespace Themia.Sdk.Services;

/// <summary>
/// [S-9902] A kit service as data: which member of each closed strategy set it uses, and their
/// parameters (addresses, header names, codes). The provider module's <c>Service</c>; the host's
/// generic client, sign-in and sync read it and never branch on which service it is
/// (docs/WIDGET-KIT.md section 5.7). A provider's descriptor is a <c>static readonly</c> value with
/// no callbacks; what cannot be data is its queries' rules (<see cref="IServiceQuery"/>).
/// </summary>
public sealed record ServiceDescriptor
{
    /// <summary>The provider module's id ([S-6900]).</summary>
    public required ProviderId Provider { get; init; }

    /// <summary>What widgets ask for ([S-502]): a kit service's feature is its provider's id.</summary>
    public required FeatureId Feature { get; init; }

    /// <summary>Its row in Settings › Widgets › Updates ([S-3780]).</summary>
    public required CadenceRow Cadence { get; init; }

    public required TransportKind Transport { get; init; }

    /// <summary>Where requests go (https): the REST root, or the GraphQL endpoint.</summary>
    public required Uri ApiRoot { get; init; }

    /// <summary>Whether an account may name its own root (an Enterprise server).</summary>
    public ApiRootRule Root { get; init; } = ApiRootRule.Fixed;

    /// <summary>The sign-in members, in the order the Accounts page offers them.</summary>
    public required ProviderSignIn SignIn { get; init; }

    /// <summary>Headers every request carries (an <c>Accept</c>, an API version); never a credential.</summary>
    public IReadOnlyList<HeaderPin> Pinned { get; init; } = [];

    public required RateLimitScheme Limits { get; init; }

    public required ErrorScheme Errors { get; init; }

    /// <summary>The default freshness strategy; a query may narrow it.</summary>
    public FreshnessHints Freshness { get; init; } = FreshnessHints.None;

    public AnonymousRule Anonymous { get; init; } = AnonymousRule.None;

    /// <summary>The most pages one answer may take, whatever the service keeps offering.</summary>
    public int MaxPages { get; init; } = 5;

    /// <summary>The most items one answer may hold.</summary>
    public int ItemBound { get; init; } = 200;

    /// <summary>The hosts an item's link or an <c>OpenTo</c> may name (https only, [S-7002]).</summary>
    public required IReadOnlyList<string> WebHosts { get; init; }

    /// <summary>The shortest cadence the service's budget allows, whatever the user picks.</summary>
    public TimeSpan IdleFloor { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>How requests are framed (service-level).</summary>
public enum TransportKind
{
    /// <summary>A GET to a rules-built address; the body is the wire type.</summary>
    RestJson,

    /// <summary>A POST of <c>{"query", "variables"}</c> to the endpoint; <c>data</c> and <c>errors</c> in the answer.</summary>
    GraphQL,

    /// <summary>A POST of <c>application/x-www-form-urlencoded</c> fields; the answer as <see cref="RestJson"/>.</summary>
    FormPost,
}

/// <summary>Whether an account names its own API root.</summary>
public enum ApiRootRule
{
    Fixed,

    /// <summary>The account's own https root on a public host (validated when it is added).</summary>
    PerAccount,
}

/// <summary>A header every request of a service carries.</summary>
public sealed record HeaderPin(string Name, string Value);

/// <summary>[S-9905] What a service offers for "nothing changed".</summary>
[Flags]
public enum FreshnessHints
{
    /// <summary>Every answer is new; the cadence alone keeps it cheap.</summary>
    None = 0,

    /// <summary><c>If-None-Match</c> with the held <c>ETag</c>; a 304 reuses the held body.</summary>
    ETag = 1,
}

/// <summary>[S-9905] How a query reads only what changed, beside <see cref="FreshnessHints"/>.</summary>
public enum IncrementalKind
{
    None,

    /// <summary>The service's own sync token: a delta merged onto a held replica ([S-9907]).</summary>
    SyncToken,

    /// <summary>A small probe before the real query; an unchanged probe skips it.</summary>
    Probe,
}

/// <summary>[S-9912] Whether the service may be read without an account.</summary>
public sealed record AnonymousRule(bool PublicReads, TimeSpan Floor)
{
    public static AnonymousRule None { get; } = new(false, TimeSpan.Zero);

    /// <summary>Public data may be read without signing in, never more often than <paramref name="floor"/>.</summary>
    public static AnonymousRule Public(TimeSpan floor) => new(true, floor);
}

/// <summary>How a budget's reset is written.</summary>
public enum ResetUnit
{
    EpochSeconds,
    EpochMillis,
    DeltaSeconds,
}

/// <summary>One budget's three headers.</summary>
public sealed record RateBudget(string LimitHeader, string RemainingHeader, string ResetHeader);

/// <summary>
/// [S-9920] How a service says "slow down": its budgets' headers, the reset's unit, the header naming
/// the bucket a reply counted against, the statuses that are a throttle when a budget is spent or a
/// wait is named, and the statuses whose <c>Retry-After</c> is not a wait.
/// </summary>
public sealed record RateLimitScheme(
    IReadOnlyList<RateBudget> Budgets,
    ResetUnit Reset,
    string? BucketHeader,
    IReadOnlySet<int> ThrottleStatuses,
    IReadOnlySet<int> RetryAfterIgnoredOn)
{
    /// <summary>
    /// A service that names no budget and says "slow down" with a 429, or with a 403 carrying
    /// <c>Retry-After</c>: what the host's own account providers send.
    /// </summary>
    public static RateLimitScheme RetryAfterOnly { get; } = new([], ResetUnit.EpochSeconds, null, new HashSet<int> { 403, 429 }, new HashSet<int>());
}

/// <summary>Where a service puts its error codes.</summary>
public enum ErrorBodyKind
{
    /// <summary>Only the status says what went wrong (a message may be matched, <see cref="ErrorScheme.Messages"/>).</summary>
    None,

    /// <summary>A GraphQL <c>errors</c> array, the code in <c>extensions.code</c>.</summary>
    GraphQLErrors,

    /// <summary>A JSON field holding the code (<see cref="ErrorScheme.CodeField"/>).</summary>
    JsonCode,
}

/// <summary>A substring of the service's message that tells a status (a secondary rate limit said in words).</summary>
public sealed record MessageRule(string Contains, FetchStatus Status);

/// <summary>
/// [S-9921] How a service says what went wrong: where its codes are, what each means, the messages
/// that mean a status, and a body field holding the seconds to wait.
/// </summary>
public sealed record ErrorScheme(
    ErrorBodyKind Body,
    string? CodeField,
    IReadOnlyDictionary<string, FetchStatus> Codes,
    IReadOnlyList<MessageRule> Messages,
    string? RetryAfterField)
{
    /// <summary>The status alone.</summary>
    public static ErrorScheme StatusOnly { get; } = new(ErrorBodyKind.None, null, new Dictionary<string, FetchStatus>(), [], null);
}
