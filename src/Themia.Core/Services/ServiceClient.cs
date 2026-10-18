using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.Services;

namespace Themia.Core.Services;

/// <summary>
/// [S-9903] How a kit service's request reaches the network: the host's account service on the
/// desktop (the account's token in its member's scheme, the descriptor's pinned headers, no retries of
/// its own), recorded answers in tests and previews (<see cref="FixtureReplay"/>). A transport failure
/// (no network, a timeout) is thrown as an <see cref="Accounts.AccountException"/>; every answer that
/// came back, whatever its status, is returned for the descriptor's schemes to read.
/// </summary>
public interface IServiceTransport
{
    Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation);
}

/// <summary>[S-9927] What one fetch of a query or a replica came to.</summary>
/// <param name="Status">Ok, or the one vocabulary's failure ([S-9900]).</param>
/// <param name="Items">The items through the item rules ([S-7002]), cut to the descriptor's bound; empty on a failure.</param>
/// <param name="RetryAt">When the reply's bucket may be asked again (a throttle, or a spent budget).</param>
/// <param name="Bucket">The bucket the block is on.</param>
/// <param name="Detail">One line for the log; never shown as is.</param>
public sealed record ServiceFetch(
    FetchStatus Status,
    IReadOnlyList<KitItem> Items,
    KitSummary? Summary = null,
    Themia.Sdk.LocalText? EmptySentence = null,
    DateTimeOffset? RetryAt = null,
    string? Bucket = null,
    string? Detail = null,
    ReplicaMerge? Replica = null)
{
    public static ServiceFetch Failed(FetchStatus status, string? detail, DateTimeOffset? retryAt = null, string? bucket = null)
        => new(status, [], RetryAt: retryAt, Bucket: bucket, Detail: detail);
}

/// <summary>
/// [S-9928] What the service layer remembers between fetches, in memory only: the bodies of
/// conditional requests by account and request key (bounded, the oldest out), so a 304 is answered
/// from here and never parsed as an empty list; the rate-limit blocks per account and bucket; and
/// the steps' answers.
/// </summary>
public sealed class ServiceMemory
{
    /// <summary>The most conditional bodies held at once.</summary>
    public const int MaxBodies = 256;

    private readonly Dictionary<string, (string ETag, ReadOnlyMemory<byte> Body, long Used)> _bodies = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _blocks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _steps = new(StringComparer.Ordinal);
    private long _clock;

    private static string Slot(string? accountId, string key) => (accountId ?? "anonymous") + "|" + key;

    public (string ETag, ReadOnlyMemory<byte> Body)? Body(string? accountId, string key)
    {
        if (!_bodies.TryGetValue(Slot(accountId, key), out var held))
        {
            return null;
        }

        _bodies[Slot(accountId, key)] = held with { Used = ++_clock };
        return (held.ETag, held.Body);
    }

    public void Keep(string? accountId, string key, string etag, ReadOnlyMemory<byte> body)
    {
        _bodies[Slot(accountId, key)] = (etag, body, ++_clock);
        while (_bodies.Count > MaxBodies)
        {
            _bodies.Remove(_bodies.MinBy(p => p.Value.Used).Key);
        }
    }

    public void Forget(string? accountId, string key) => _bodies.Remove(Slot(accountId, key));

    /// <summary>[S-9920] The block on an account's bucket, or null when it may be asked.</summary>
    public DateTimeOffset? BlockedUntil(string? accountId, string bucket, DateTimeOffset now)
        => _blocks.TryGetValue(Slot(accountId, bucket), out var until) && until > now ? until : null;

    public void Block(string? accountId, string bucket, DateTimeOffset until)
    {
        var slot = Slot(accountId, bucket);
        if (!_blocks.TryGetValue(slot, out var held) || until > held)
        {
            _blocks[slot] = until;
        }
    }

    public IReadOnlyDictionary<string, string>? Step(string? accountId, string key)
        => _steps.TryGetValue(Slot(accountId, key), out var values) ? values : null;

    public void KeepStep(string? accountId, string key, IReadOnlyDictionary<string, string> values) => _steps[Slot(accountId, key)] = values;

    /// <summary>[S-2607] An account signed out: nothing of it is kept.</summary>
    public void ForgetAccount(string accountId)
    {
        var prefix = accountId + "|";
        foreach (var slot in _bodies.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _bodies.Remove(slot);
        }

        foreach (var slot in _blocks.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _blocks.Remove(slot);
        }

        foreach (var slot in _steps.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _steps.Remove(slot);
        }
    }
}

/// <summary>
/// [S-9927] The one generic client of every kit service: no subclass and no branch per service. For a
/// query it runs the steps (each a held, conditional, throttled request of its own), then the pages:
/// each page's request is built by the query, sent with the held entity tag where the query's
/// freshness allows, classified through <see cref="ServiceFailures.Classify"/> (a throttle blocks the
/// account's bucket until its time), a 304 answered from the held body, a GraphQL answer unwrapped,
/// the page mapped by the query and handed to the one paging loop. For a replica it sends the held
/// sync token (none for a full read, and a full read when the service refuses the token) and merges.
/// It never logs a token or an item's text.
/// </summary>
public sealed class ServiceClient(ServiceDescriptor descriptor, IServiceTransport transport, ServiceMemory memory)
{
    private const string Scope = "service";

    public ServiceDescriptor Descriptor { get; } = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

    /// <summary>
    /// One fetch of <paramref name="query"/> for <paramref name="accountId"/> (null: anonymously, only
    /// where the descriptor allows public reads), up to <paramref name="count"/> items.
    /// </summary>
    public async Task<ServiceFetch> FetchAsync(string? accountId, IServiceQuery query, int count, Func<DateTimeOffset> clock, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(clock);

        if (accountId is null && !Descriptor.Anonymous.PublicReads)
        {
            return ServiceFetch.Failed(FetchStatus.NotConfigured, "no account and no public reads");
        }

        var fromSteps = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var step in query.Steps.Take(3))
        {
            var stepped = await StepAsync(accountId, step, clock, cancellation).ConfigureAwait(false);
            if (stepped.Values is not { } values)
            {
                return stepped.Failure!;
            }

            foreach (var (name, value) in values)
            {
                fromSteps[name] = value;
            }
        }

        var pager = new ServicePager(query.Paging, count, Descriptor.MaxPages, Descriptor.ItemBound, Math.Min(count, Descriptor.ItemBound));
        var items = new List<KitItem>();
        KitSummary? summary = null;
        Themia.Sdk.LocalText? empty = null;
        DateTimeOffset? spent = null;
        string? spentBucket = null;
        PageAt? page = PageAt.First;

        while (page is not null)
        {
            var request = query.Build(page, fromSteps);
            var answer = await AskAsync(accountId, request, query.Freshness, clock, cancellation).ConfigureAwait(false);
            if (answer.Failure is { } failure)
            {
                return failure;
            }

            if (answer.Verdict.RetryAt is { } block)
            {
                spent = block;
                spentBucket = answer.Verdict.Bucket ?? request.Bucket;
            }

            var chunk = query.Answer(answer.Body, fromSteps, clock(), out var problem);
            if (chunk is null)
            {
                return Bad(request, problem ?? "not the expected document");
            }

            items.AddRange(chunk.Items);
            summary ??= chunk.Summary;
            empty ??= chunk.EmptySentence;

            page = pager.Next(request.Address, answer.Link, chunk);

            if (pager.Stop == PageStop.Loop)
            {
                return Bad(request, "the pages loop");
            }
        }

        var clean = KitItems.Clean(items, Descriptor.WebHosts);
        if (clean.Count > Descriptor.ItemBound)
        {
            clean = [.. clean.Take(Descriptor.ItemBound)];
        }

        return new ServiceFetch(FetchStatus.Ok, clean, KitItems.Clean(summary), empty, spent, spentBucket);
    }

    /// <summary>
    /// [S-9907] One sync of a replica: the held token (none for a full read), merged by the
    /// provider; a refused token is asked again once as a full read.
    /// </summary>
    public async Task<ServiceFetch> SyncAsync(string accountId, IServiceReplicaQuery replica, string? heldJson, string? syncToken, Func<DateTimeOffset> clock, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(replica);
        ArgumentNullException.ThrowIfNull(clock);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = attempt == 0 ? syncToken : null;
            var request = replica.Build(token);
            var answer = await AskAsync(accountId, request, FreshnessHints.None, clock, cancellation).ConfigureAwait(false);
            if (answer.Failure is { } failure)
            {
                return failure;
            }

            var merged = replica.Merge(token is null ? null : heldJson, answer.Body, clock(), out var problem);
            if (merged is null)
            {
                return Bad(request, problem ?? "not the expected document");
            }

            if (merged.TokenRejected)
            {
                Log.Info(Scope, $"{Descriptor.Provider}: the sync token was refused; reading everything");
                continue;
            }

            return new ServiceFetch(FetchStatus.Ok, [], RetryAt: answer.Verdict.RetryAt, Bucket: answer.Verdict.Bucket ?? request.Bucket, Replica: merged);
        }

        return ServiceFetch.Failed(FetchStatus.BadResponse, "the service refused a full read's token too");
    }

    private async Task<(IReadOnlyDictionary<string, string>? Values, ServiceFetch? Failure)> StepAsync(string? accountId, IServiceStep step, Func<DateTimeOffset> clock, CancellationToken cancellation)
    {
        var request = step.Build();
        var answer = await AskAsync(accountId, request, step.Freshness, clock, cancellation).ConfigureAwait(false);
        if (answer.Failure is { } failure)
        {
            // A step answered before is used while it cannot be asked (a block, a blip).
            return memory.Step(accountId, step.Key) is { } held && failure.Status is FetchStatus.RateLimited or FetchStatus.Offline or FetchStatus.Unreachable or FetchStatus.TimedOut
                ? (held, null)
                : (null, failure);
        }

        var values = step.Answer(answer.Body, out var problem);
        if (values is null)
        {
            return (null, Bad(request, problem ?? "not the expected document"));
        }

        memory.KeepStep(accountId, step.Key, values);
        return (values, null);
    }

    private sealed record Answered(ServiceVerdict Verdict, ReadOnlyMemory<byte> Body, string? Link, ServiceFetch? Failure);

    /// <summary>One request through the conditional helper, the bucket's block and the one classifier.</summary>
    private async Task<Answered> AskAsync(string? accountId, ServiceRequest request, FreshnessHints freshness, Func<DateTimeOffset> clock, CancellationToken cancellation)
    {
        var now = clock();
        if (memory.BlockedUntil(accountId, request.Bucket, now) is { } blocked)
        {
            return new Answered(new ServiceVerdict(FetchStatus.RateLimited, blocked, request.Bucket), default, null,
                ServiceFetch.Failed(FetchStatus.RateLimited, $"the {request.Bucket} budget is spent", blocked, request.Bucket));
        }

        var conditional = (freshness & FreshnessHints.ETag) != 0 && request.Verb == HttpVerb.Get;
        var held = conditional ? memory.Body(accountId, request.Key) : null;

        ServiceReply reply;
        try
        {
            reply = await transport.SendAsync(accountId, Descriptor, request, held?.ETag, cancellation).ConfigureAwait(false);
        }
        catch (Accounts.AccountException ex)
        {
            return new Answered(new ServiceVerdict(ex.ToFetchStatus()), default, null, ServiceFetch.Failed(ex.ToFetchStatus(), ex.Message, ex.RetryAt, request.Bucket));
        }

        now = clock();
        var body = reply.Body;
        if (reply.IsNotModified)
        {
            if (held is not { } copy)
            {
                return new Answered(new ServiceVerdict(FetchStatus.BadResponse), default, null, Bad(request, "a 304 with nothing held"));
            }

            body = copy.Body;
        }

        var envelope = Descriptor.Transport == TransportKind.GraphQL && reply.IsSuccess ? GraphQLEnvelope.Read(body.Span, string.Empty) : null;
        if (Descriptor.Transport == TransportKind.GraphQL && reply.IsSuccess && envelope is null)
        {
            return new Answered(new ServiceVerdict(FetchStatus.BadResponse), default, null, Bad(request, "not a GraphQL answer"));
        }

        var verdict = ServiceFailures.Classify(Descriptor, reply with { Body = body }, now, anonymous: accountId is null, graphQL: envelope);
        var bucket = verdict.Bucket ?? request.Bucket;

        if (verdict.RetryAt is { } until)
        {
            memory.Block(accountId, bucket, until);
        }

        if (verdict.UnknownCode is { } code)
        {
            Log.Warn(Scope, $"{Descriptor.Provider} answered a code its scheme does not know: {code}");
        }

        if (!verdict.Ok)
        {
            return new Answered(verdict, default, null, ServiceFetch.Failed(verdict.Status, verdict.Detail, verdict.RetryAt, bucket));
        }

        if (verdict.Detail is { } partial)
        {
            Log.Debug(Scope, $"{Descriptor.Provider} answered with errors beside its data: {partial}");
        }

        if (conditional && reply.IsSuccess && reply.Header("ETag") is { Length: > 0 } etag)
        {
            memory.Keep(accountId, request.Key, etag, body);
        }

        return new Answered(verdict, envelope is { } graph ? graph.Data : body, reply.Header("Link"), null);
    }

    private ServiceFetch Bad(ServiceRequest request, string problem)
    {
        Log.Warn(Scope, $"{Descriptor.Provider} sent something unexpected for {request.Bucket} ({problem})");
        return ServiceFetch.Failed(FetchStatus.BadResponse, problem);
    }
}

/// <summary>
/// [S-9929] Recorded answers replayed as a transport, for tests and debug previews: the provider's
/// fixtures are what a widget's preview states draw, through the real rules. A reply is matched by the
/// request's address (the first recorded reply whose <c>match</c> the path and query contain), and
/// each match may answer a different reply when asked again (an ETag then a 304). Nothing reaches
/// the network.
/// </summary>
public sealed class FixtureReplay : IServiceTransport
{
    private readonly List<(string Match, ServiceReply Reply)> _replies = [];
    private readonly Dictionary<string, int> _asked = new(StringComparer.Ordinal);

    /// <summary>The requests asked, in order: address and the entity tag sent.</summary>
    public List<(Uri Address, string? IfNoneMatch, string? AccountId)> Asked { get; } = [];

    public FixtureReplay Add(string match, ServiceReply reply)
    {
        _replies.Add((match, reply));
        return this;
    }

    /// <summary>
    /// A recording: <c>{"replies": [{"match": "/search", "status": 200, "headers": {...}, "body": ...}]}</c>,
    /// the body a JSON value (written as is) or a string. Null with <paramref name="problem"/> when the
    /// document is not one.
    /// </summary>
    public static FixtureReplay? Parse(string json, out string? problem)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("replies", out var replies) || replies.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                problem = "no replies array";
                return null;
            }

            var replay = new FixtureReplay();
            foreach (var reply in replies.EnumerateArray())
            {
                var match = reply.TryGetProperty("match", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String ? m.GetString() ?? string.Empty : string.Empty;
                var status = reply.TryGetProperty("status", out var s) && s.TryGetInt32(out var code) ? code : 200;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (reply.TryGetProperty("headers", out var h) && h.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var header in h.EnumerateObject())
                    {
                        headers[header.Name] = header.Value.ValueKind == System.Text.Json.JsonValueKind.String ? header.Value.GetString() ?? string.Empty : header.Value.GetRawText();
                    }
                }

                var body = !reply.TryGetProperty("body", out var b) ? string.Empty
                    : b.ValueKind == System.Text.Json.JsonValueKind.String ? b.GetString() ?? string.Empty
                    : b.GetRawText();
                replay.Add(match, new ServiceReply(status, headers, System.Text.Encoding.UTF8.GetBytes(body)));
            }

            problem = null;
            return replay;
        }
        catch (System.Text.Json.JsonException ex)
        {
            problem = ex.Message;
            return null;
        }
    }

    public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(request);
        Asked.Add((request.Address, ifNoneMatch, accountId));

        var target = request.Address.PathAndQuery;
        var matches = _replies.Where(r => target.Contains(r.Match, StringComparison.Ordinal)).Select(r => r.Reply).ToList();
        if (matches.Count == 0)
        {
            throw new Accounts.AccountException(Accounts.AccountError.Unreachable, $"no recorded answer for {request.Address.AbsolutePath}");
        }

        var index = _asked.GetValueOrDefault(target);
        _asked[target] = index + 1;
        return Task.FromResult(matches[Math.Min(index, matches.Count - 1)]);
    }
}
