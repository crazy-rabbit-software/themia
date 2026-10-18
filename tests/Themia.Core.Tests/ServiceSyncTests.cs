using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Localization;
using Themia.Core.Services;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-9925]..[S-9929] The service layer's host side, driven by recorded answers through the one client
/// and the sync's entries (the "Polling" conformance of docs/WIDGET-KIT.md section 9): every request
/// goes through the conditional helper, the bucket's block and the one classifier; each pilot's shape
/// drives its throttle, its 401, its 403, its 304 where it has one, and its error body.
/// </summary>
public sealed partial class ServiceSyncTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly ServiceDescriptor Hub = ServiceLayerTests.GitHubShaped with
    {
        ApiRoot = new("https://api.hub.example/"),
        WebHosts = ["hub.example"],
        SignIn = new([new OAuthDeviceCode(new("https://hub.example/login/device/code"), new("https://hub.example/login/oauth/access_token"), "client",
            [], ScopeSeparator.Space, new RefreshRule(true, TimeSpan.Zero), new("https://hub.example/apps/themia/installations/new"))]),
    };

    // ---- a GitHub-shaped query over a test wire type ---------------------------------------------------

    internal sealed record HubItem(long Id, string Title, string Url, DateTimeOffset Updated);

    internal sealed record HubPage(IReadOnlyList<HubItem> Items);

    internal sealed record HubRepo(string Default_branch);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(HubPage))]
    [JsonSerializable(typeof(HubRepo))]
    internal sealed partial class HubWire : JsonSerializerContext;

    private sealed class Reviews(string bucket = "search", IReadOnlyList<IServiceStep>? steps = null) : IServiceQuery<HubPage>
    {
        public string Key => "reviews|" + bucket;
        public PagingKind Paging => PagingKind.LinkHeader;
        public FreshnessHints Freshness => FreshnessHints.ETag;
        public IReadOnlyList<IServiceStep> Steps => steps ?? [];
        public JsonTypeInfo<HubPage> Wire => HubWire.Default.HubPage;

        public List<IReadOnlyDictionary<string, string>> Seen { get; } = [];

        public ServiceRequest Build(PageAt page, IReadOnlyDictionary<string, string> fromSteps)
        {
            Seen.Add(fromSteps);
            var address = page is PageAt.Address next ? next.Next : new Uri("https://api.hub.example/search/issues?q=review");
            return new(HttpVerb.Get, address, null, bucket, Key + "|" + address.Query);
        }

        public KitChunk Map(HubPage wire, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now)
            => new([.. wire.Items.Select(i => new KitItem("pr:" + i.Id, i.Title, When: KitWhen.Instant(i.Updated), Link: new Uri(i.Url)))]);
    }

    private sealed class DefaultBranch : IServiceStep
    {
        public string Key => "repo";
        public FreshnessHints Freshness => FreshnessHints.ETag;

        public ServiceRequest Build() => new(HttpVerb.Get, new("https://api.hub.example/repos/octo/app"), null, "core", "repo");

        public IReadOnlyDictionary<string, string>? Answer(ReadOnlyMemory<byte> body, out string? problem)
            => ServiceWire.TryParse(body.Span, HubWire.Default.HubRepo, out var repo, out problem)
                ? new Dictionary<string, string> { ["branch"] = repo!.Default_branch }
                : null;
    }

    private static FixtureReplay Recording([CallerFilePath] string here = "")
        => FixtureReplay.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "services", "hub-items.json")), out _)!;

    private static ServiceReply Reply(int status, string body = "", params (string Name, string Value)[] headers)
        => new(status, RateLimitRulesTests.Headers(headers), Encoding.UTF8.GetBytes(body));

    // ---- [S-9927] the one client -------------------------------------------------------------------------

    [Fact]
    public async Task S9927_pages_follow_the_link_header_and_items_pass_the_item_rules()
    {
        var replay = Recording();
        var client = new ServiceClient(Hub, replay, new ServiceMemory());

        var fetch = await client.FetchAsync("acct", new Reviews(), count: 10, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.Ok, fetch.Status);
        Assert.Equal(["pr:1", "pr:2", "pr:3"], fetch.Items.Select(i => i.Id));
        Assert.Null(fetch.Items[1].Link); // a link off the service's own hosts opens nothing ([S-7002])
        Assert.Equal(2, replay.Asked.Count);
    }

    [Fact]
    public async Task S9927_the_held_entity_tag_is_sent_and_a_304_is_answered_from_the_held_body()
    {
        var replay = Recording();
        var memory = new ServiceMemory();
        var client = new ServiceClient(Hub, replay, memory);

        await client.FetchAsync("acct", new Reviews(), count: 2, () => Now, CancellationToken.None);
        var again = await client.FetchAsync("acct", new Reviews(), count: 2, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.Ok, again.Status);
        Assert.Equal(["pr:1", "pr:2"], again.Items.Select(i => i.Id));
        Assert.Null(replay.Asked[0].IfNoneMatch);
        Assert.Equal("\"p1\"", replay.Asked[1].IfNoneMatch);
    }

    [Fact]
    public async Task S9927_a_304_with_nothing_held_is_a_bad_response_never_an_empty_list()
    {
        var replay = new FixtureReplay().Add("/search", Reply(304));
        var fetch = await new ServiceClient(Hub, replay, new ServiceMemory()).FetchAsync("acct", new Reviews(), 5, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.BadResponse, fetch.Status);
        Assert.Empty(fetch.Items);
    }

    [Fact]
    public async Task S9927_a_throttle_blocks_its_bucket_and_nothing_asks_it_again_before_the_reset()
    {
        var reset = Now.AddMinutes(30);
        var replay = new FixtureReplay()
            .Add("/search", Reply(403, """{"message":"API rate limit exceeded"}""", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)), ("X-RateLimit-Resource", "search")))
            .Add("/repos", Reply(200, """{"default_branch":"main"}"""));
        var memory = new ServiceMemory();
        var client = new ServiceClient(Hub, replay, memory);

        var first = await client.FetchAsync("acct", new Reviews(), 5, () => Now, CancellationToken.None);
        Assert.Equal(FetchStatus.RateLimited, first.Status);
        Assert.Equal(reset, first.RetryAt);
        Assert.Equal("search", first.Bucket);

        var blocked = await client.FetchAsync("acct", new Reviews(), 5, () => Now.AddMinutes(10), CancellationToken.None);
        Assert.Equal(FetchStatus.RateLimited, blocked.Status);
        Assert.Single(replay.Asked); // the search bucket was not asked

        // Another account's search, and this account's core reads, are not blocked.
        await client.FetchAsync("other", new Reviews(), 5, () => Now.AddMinutes(10), CancellationToken.None);
        Assert.Equal(2, replay.Asked.Count);
        Assert.Null(memory.BlockedUntil("acct", "core", Now.AddMinutes(10)));
    }

    [Theory]
    [InlineData(401, FetchStatus.SignInRequired)]
    [InlineData(403, FetchStatus.Forbidden)]
    [InlineData(404, FetchStatus.NotFound)]
    [InlineData(500, FetchStatus.BadResponse)]
    public async Task S9927_a_refusal_reaches_the_one_vocabulary(int status, FetchStatus expected)
    {
        var replay = new FixtureReplay().Add("/search", Reply(status, """{"message":"no"}"""));
        var fetch = await new ServiceClient(Hub, replay, new ServiceMemory()).FetchAsync("acct", new Reviews(), 5, () => Now, CancellationToken.None);

        Assert.Equal(expected, fetch.Status);
    }

    [Fact]
    public async Task S9927_a_transport_failure_is_its_own_state()
    {
        var fetch = await new ServiceClient(Hub, new FixtureReplay(), new ServiceMemory()).FetchAsync("acct", new Reviews(), 5, () => Now, CancellationToken.None);
        Assert.Equal(FetchStatus.Unreachable, fetch.Status);
    }

    [Fact]
    public async Task S9927_a_steps_values_reach_the_query_and_a_held_step_rides_out_a_blip()
    {
        var replay = new FixtureReplay()
            .Add("/repos", Reply(200, """{"default_branch":"main"}""", ("ETag", "\"r\"")))
            .Add("/search", Reply(200, """{"items":[]}"""));
        var memory = new ServiceMemory();
        var query = new Reviews(steps: [new DefaultBranch()]);

        var fetch = await new ServiceClient(Hub, replay, memory).FetchAsync("acct", query, 5, () => Now, CancellationToken.None);
        Assert.Equal(FetchStatus.Ok, fetch.Status);
        Assert.Equal("main", query.Seen[0]["branch"]);

        var offline = new FixtureReplay().Add("/search", Reply(200, """{"items":[]}"""));
        var again = await new ServiceClient(Hub, offline, memory).FetchAsync("acct", query, 5, () => Now, CancellationToken.None);
        Assert.Equal(FetchStatus.Ok, again.Status); // the step's answer was held
    }

    [Fact]
    public async Task S9927_an_answer_that_is_not_the_wire_shape_is_a_bad_response()
    {
        var replay = new FixtureReplay().Add("/search", Reply(200, """{"items":"nope"}"""));
        var fetch = await new ServiceClient(Hub, replay, new ServiceMemory()).FetchAsync("acct", new Reviews(), 5, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.BadResponse, fetch.Status);
        Assert.Contains("$.items", fetch.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task S9927_no_account_and_no_public_reads_asks_nothing()
    {
        var replay = new FixtureReplay();
        var fetch = await new ServiceClient(Hub, replay, new ServiceMemory()).FetchAsync(null, new Reviews(), 5, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.NotConfigured, fetch.Status);
        Assert.Empty(replay.Asked);
    }

    // ---- a Linear-shaped GraphQL query --------------------------------------------------------------------

    internal sealed record GraphIssues(GraphViewer Viewer);

    internal sealed record GraphViewer(GraphConnection AssignedIssues);

    internal sealed record GraphConnection(IReadOnlyList<GraphNode> Nodes, GraphPageInfo PageInfo);

    internal sealed record GraphNode(string Id, string Title);

    internal sealed record GraphPageInfo(bool HasNextPage, string? EndCursor);

    internal sealed record GraphVars(int First, string? After);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(GraphIssues))]
    [JsonSerializable(typeof(GraphVars))]
    internal sealed partial class GraphWire : JsonSerializerContext;

    private sealed class Assigned : IServiceQuery<GraphIssues>
    {
        public string Key => "assigned";
        public PagingKind Paging => PagingKind.BodyCursor;
        public FreshnessHints Freshness => FreshnessHints.None;
        public IReadOnlyList<IServiceStep> Steps => [];
        public JsonTypeInfo<GraphIssues> Wire => GraphWire.Default.GraphIssues;

        public ServiceRequest Build(PageAt page, IReadOnlyDictionary<string, string> fromSteps)
            => new(HttpVerb.Post, new("https://api.graph.example/graphql"),
                GraphQLEnvelope.Request("query Q($first: Int!, $after: String) { viewer { assignedIssues { nodes { id title } } } }",
                    new GraphVars(2, page is PageAt.Cursor c ? c.Value : null), GraphWire.Default.GraphVars), "graphql",
                Key + (page is PageAt.Cursor cur ? "|" + cur.Value : string.Empty));

        public KitChunk Map(GraphIssues wire, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now)
            => new([.. wire.Viewer.AssignedIssues.Nodes.Select(n => new KitItem(n.Id, n.Title))],
                wire.Viewer.AssignedIssues.PageInfo.HasNextPage ? wire.Viewer.AssignedIssues.PageInfo.EndCursor : null);
    }

    /// <summary>The GraphQL transport answers by the cursor in the body, not the address.</summary>
    private sealed class GraphReplay(params ServiceReply[] replies) : IServiceTransport
    {
        private int _next;

        public List<string> Bodies { get; } = [];

        public Task<ServiceReply> SendAsync(string? accountId, ServiceDescriptor descriptor, ServiceRequest request, string? ifNoneMatch, CancellationToken cancellation)
        {
            Bodies.Add(Encoding.UTF8.GetString(request.Body!.Utf8.Span));
            return Task.FromResult(replies[Math.Min(_next++, replies.Length - 1)]);
        }
    }

    [Fact]
    public async Task S9927_graphql_pages_by_its_body_cursor_and_unwraps_data_for_the_rules()
    {
        var transport = new GraphReplay(
            Reply(200, """{"data":{"viewer":{"assignedIssues":{"nodes":[{"id":"a","title":"A"},{"id":"b","title":"B"}],"pageInfo":{"hasNextPage":true,"endCursor":"c1"}}}}}"""),
            Reply(200, """{"data":{"viewer":{"assignedIssues":{"nodes":[{"id":"c","title":"C"}],"pageInfo":{"hasNextPage":false,"endCursor":null}}}}}"""));

        var fetch = await new ServiceClient(ServiceLayerTests.LinearShaped, transport, new ServiceMemory()).FetchAsync("acct", new Assigned(), 10, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.Ok, fetch.Status);
        Assert.Equal(["a", "b", "c"], fetch.Items.Select(i => i.Id));
        Assert.Contains("\"after\":\"c1\"", transport.Bodies[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task S9927_a_graphql_throttle_on_a_400_blocks_the_graphql_bucket()
    {
        var reset = Now.AddMinutes(20);
        var transport = new GraphReplay(Reply(400, """{"errors":[{"message":"Rate limited","extensions":{"code":"RATELIMITED"}}]}""",
            ("X-RateLimit-Requests-Remaining", "0"), ("X-RateLimit-Requests-Reset", reset.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var memory = new ServiceMemory();

        var fetch = await new ServiceClient(ServiceLayerTests.LinearShaped, transport, memory).FetchAsync("acct", new Assigned(), 10, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.RateLimited, fetch.Status);
        Assert.Equal(reset, fetch.RetryAt);
        Assert.Equal(reset, memory.BlockedUntil("acct", "graphql", Now));
    }

    [Fact]
    public async Task S9927_a_graphql_200_with_errors_and_no_list_is_its_codes()
    {
        var transport = new GraphReplay(Reply(200, """{"data":null,"errors":[{"message":"nope","extensions":{"code":"AUTHENTICATION_ERROR"}}]}"""));
        var fetch = await new ServiceClient(ServiceLayerTests.LinearShaped, transport, new ServiceMemory()).FetchAsync("acct", new Assigned(), 10, () => Now, CancellationToken.None);

        Assert.Equal(FetchStatus.SignInRequired, fetch.Status);
    }

    // ---- a Todoist-shaped replica ----------------------------------------------------------------------------

    internal sealed class TodoReplica
    {
        public Dictionary<string, string> Items { get; set; } = [];
        public string? Token { get; set; }
    }

    internal sealed class TodoSync
    {
        [JsonPropertyName("sync_token")] public string? SyncToken { get; set; }
        [JsonPropertyName("full_sync")] public bool FullSync { get; set; }
        [JsonPropertyName("items")] public List<TodoItem> Items { get; set; } = [];
    }

    internal sealed class TodoItem
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;
        [JsonPropertyName("is_deleted")] public bool IsDeleted { get; set; }
    }

    [JsonSerializable(typeof(TodoReplica))]
    [JsonSerializable(typeof(TodoSync))]
    internal sealed partial class TodoWire : JsonSerializerContext;

    private sealed class Todos : ServiceReplica<TodoReplica, TodoSync>, IReplicaView
    {
        protected override JsonTypeInfo<TodoReplica> ReplicaJson => TodoWire.Default.TodoReplica;
        protected override JsonTypeInfo<TodoSync> WireJson => TodoWire.Default.TodoSync;
        public override string Key => "todo";
        public IServiceReplicaQuery Replica => this;
        string IReplicaView.Key => "today";

        public List<string?> Tokens { get; } = [];

        public override ServiceRequest Build(string? syncToken)
        {
            Tokens.Add(syncToken);
            return new(HttpVerb.Post, new("https://api.sync.example/api/v1/sync"), FormBody.Encode([new("sync_token", syncToken ?? "*")]), syncToken is null ? "sync-full" : "sync-partial", "sync");
        }

        protected override TodoReplica Merge(TodoReplica? held, TodoSync delta, DateTimeOffset now)
        {
            var items = delta.FullSync || held is null ? [] : new Dictionary<string, string>(held.Items);
            foreach (var item in delta.Items)
            {
                if (item.IsDeleted)
                {
                    items.Remove(item.Id);
                }
                else
                {
                    items[item.Id] = item.Content;
                }
            }

            return new TodoReplica { Items = items, Token = delta.SyncToken };
        }

        protected override TodoReplica Validate(TodoReplica replica) => replica;
        protected override string? SyncTokenOf(TodoReplica replica) => replica.Token;
        protected override bool IsFull(TodoSync delta) => delta.FullSync;

        public KitPage View(string replicaJson, DateTimeOffset now)
            => Read(replicaJson, out _) is { } replica ? new KitPage([.. replica.Items.OrderBy(i => i.Key).Select(i => new KitItem(i.Key, i.Value))]) : new KitPage([]);
    }

    [Fact]
    public async Task S9907_a_replica_reads_everything_then_merges_deltas_and_starts_over_on_a_refused_token()
    {
        var todos = new Todos();
        var memory = new ServiceMemory();

        var full = await new ServiceClient(ServiceLayerTests.TodoistShaped, new GraphReplay(
            Reply(200, """{"sync_token":"t1","full_sync":true,"items":[{"id":"1","content":"Milk"},{"id":"2","content":"Eggs"}]}""")), memory)
            .SyncAsync("acct", todos, null, null, () => Now, CancellationToken.None);
        Assert.Equal(FetchStatus.Ok, full.Status);
        Assert.True(full.Replica!.Full);
        Assert.Equal("t1", full.Replica.SyncToken);
        Assert.Equal("sync-full", full.Bucket);

        var delta = await new ServiceClient(ServiceLayerTests.TodoistShaped, new GraphReplay(
            Reply(200, """{"sync_token":"t2","full_sync":false,"items":[{"id":"1","is_deleted":true},{"id":"3","content":"Bread"}]}""")), memory)
            .SyncAsync("acct", todos, full.Replica.Json, "t1", () => Now, CancellationToken.None);
        Assert.Equal(["Bread", "Eggs"], todos.View(delta.Replica!.Json, Now).Items.Select(i => i.Title).Order());
        Assert.Equal("sync-partial", delta.Bucket);

        // A delta onto nothing held: the token is refused and everything is read again.
        var transport = new GraphReplay(
            Reply(200, """{"sync_token":"t9","full_sync":false,"items":[]}"""),
            Reply(200, """{"sync_token":"t10","full_sync":true,"items":[{"id":"5","content":"Tea"}]}"""));
        todos.Tokens.Clear();
        var restart = await new ServiceClient(ServiceLayerTests.TodoistShaped, transport, memory).SyncAsync("acct", todos, null, "stale", () => Now, CancellationToken.None);
        Assert.Equal(["stale", null], todos.Tokens);
        Assert.True(restart.Replica!.Full);
    }

    // ---- [S-9925] the entries ---------------------------------------------------------------------------------

    private static readonly ProviderId HubId = Hub.Provider;

    private static ServiceEntries Entries() => new(id => id == HubId ? Hub : id == ServiceLayerTests.TodoistShaped.Provider ? ServiceLayerTests.TodoistShaped : null);

    private static readonly ServiceAccount Work = new("hub:1", HubId, "Work");
    private static readonly ServiceAccount Home = new("hub:2", HubId, "Home");

    [Fact]
    public void S9925_an_interest_reads_every_account_of_its_provider_or_the_ones_it_names()
    {
        var entries = Entries();
        var all = new ServiceInterest(HubId, new Reviews(), [], 5);
        var one = new ServiceInterest(HubId, new Reviews(), ["hub:2"], 3);

        entries.Reconcile([all, one], [Work, Home, new ServiceAccount("x", new ProviderId("other"), "Other")], _ => TimeSpan.FromMinutes(5), Now);

        Assert.Equal(2, entries.All.Count); // one entry per account and query, shared by both widgets
        Assert.Equal(5, entries.All.Max(e => e.Count));
        Assert.Equal(["hub:2"], entries.Targets(one).Select(a => a.Id));
    }

    [Fact]
    public void S9925_two_accounts_merge_and_each_says_how_it_went()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work, Home], _ => TimeSpan.FromMinutes(5), Now);

        var work = entries.Find(ServiceEntries.KeyOf(interest, "hub:1"))!;
        var home = entries.Find(ServiceEntries.KeyOf(interest, "hub:2"))!;
        entries.Complete(work, new ServiceFetch(FetchStatus.Ok, [new KitItem("pr:1", "One", When: KitWhen.Instant(Now.AddHours(-2)))]), Now);
        entries.Complete(home, new ServiceFetch(FetchStatus.Ok, [new KitItem("pr:1", "One"), new KitItem("pr:9", "Nine", When: KitWhen.Instant(Now.AddHours(-1)))]), Now);

        var read = entries.Read(interest, Now);
        Assert.Equal(["pr:9", "pr:1"], read.Items.Select(i => i.Id));
        Assert.Equal(["Work", "Home"], read.Outcomes.Select(o => o.Owner));
        Assert.Equal(["hub.example"], read.WebHosts!);
        Assert.Equal(new Uri("https://hub.example/apps/themia/installations/new"), read.GrantPage);

        // A failure keeps what is held, and says so per account.
        entries.Complete(home, ServiceFetch.Failed(FetchStatus.Offline, "down"), Now.AddMinutes(5));
        read = entries.Read(interest, Now.AddMinutes(5));
        Assert.Equal(2, read.Items.Count);
        Assert.Equal(FetchStatus.Offline, read.Outcomes[1].Status);
        Assert.Equal(Now, read.Outcomes[1].HeldAt);
    }

    [Fact]
    public void S9925_a_refusal_names_the_account_and_the_screen_offers_the_grant_page()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work], _ => TimeSpan.FromMinutes(5), Now);
        entries.Complete(entries.All.Single(), ServiceFetch.Failed(FetchStatus.NotFound, "HTTP 404"), Now);

        var read = entries.Read(interest, Now);
        Assert.Equal(KitAccountState.Forbidden, Assert.Single(read.Accounts!).State);

        var screen = KitScreens.Decide(Locale.En, read.Accounts, read.Chosen ?? [], read.Outcomes, 0, "No reviews waiting", grantPage: read.GrantPage, now: Now);
        Assert.Equal(KitScreenKind.Forbidden, screen.Kind);
        Assert.True(screen.OffersAccounts);
        Assert.True(screen.OffersGrant);
    }

    [Fact]
    public void S9925_a_sign_in_only_the_user_can_mend_is_not_asked_and_says_so()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work with { NeedsSignIn = true }], _ => TimeSpan.FromMinutes(5), Now);

        Assert.Empty(entries.Due(Now));
        var screen = KitScreens.Decide(Locale.En, entries.Read(interest, Now).Accounts, [], entries.Read(interest, Now).Outcomes, 0, "x");
        Assert.Equal(KitScreenKind.SignInRequired, screen.Kind);
    }

    [Fact]
    public void S9925_a_throttle_holds_the_entry_and_the_line_says_when_it_asks_again()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work], _ => TimeSpan.FromMinutes(1), Now);
        var entry = entries.All.Single();

        entries.Complete(entry, ServiceFetch.Failed(FetchStatus.RateLimited, "HTTP 403", Now.AddMinutes(45), "search"), Now);

        Assert.Empty(entries.Due(Now.AddMinutes(44)));
        Assert.Single(entries.Due(Now.AddMinutes(45)));
        var screen = KitScreens.Decide(Locale.En, entries.Read(interest, Now).Accounts, [], entries.Read(interest, Now).Outcomes, 0, "x", now: Now);
        Assert.Contains("asking again at", screen.Sentence, StringComparison.Ordinal);
    }

    [Fact]
    public void S9908_the_cadence_keeps_the_services_floor_and_a_success_that_spent_its_budget_waits_for_the_reset()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work], _ => TimeSpan.FromSeconds(10), Now); // below the one-minute floor
        var entry = entries.All.Single();
        Assert.Equal(TimeSpan.FromMinutes(1), entry.Schedule.Interval);

        entries.Complete(entry, new ServiceFetch(FetchStatus.Ok, [], RetryAt: Now.AddMinutes(9), Bucket: "search"), Now);
        Assert.Empty(entries.Due(Now.AddMinutes(8)));
        Assert.Single(entries.Due(Now.AddMinutes(9)));
    }

    [Fact]
    public void S9925_an_entry_no_widget_reads_any_more_is_let_go()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work], _ => TimeSpan.FromMinutes(5), Now);
        Assert.Single(entries.All);

        entries.Reconcile([], [Work], _ => TimeSpan.FromMinutes(5), Now);
        Assert.Empty(entries.All);
    }

    [Fact]
    public void S9907_a_replica_entry_reads_everything_at_least_daily()
    {
        var entries = Entries();
        var todos = new Todos();
        var provider = ServiceLayerTests.TodoistShaped.Provider;
        var interest = new ServiceInterest(provider, (IReplicaView)todos, [], 50);
        entries.Reconcile([interest], [new ServiceAccount("todo:1", provider, "Me")], _ => TimeSpan.FromMinutes(5), Now);
        var entry = entries.All.Single();

        Assert.Null(ServiceEntries.TokenToSend(entry, Now));
        entries.Complete(entry, new ServiceFetch(FetchStatus.Ok, [], Replica: new ReplicaMerge("""{"Items":{"1":"Milk"},"Token":"t1"}""", "t1", Full: true)), Now);
        Assert.Equal("t1", ServiceEntries.TokenToSend(entry, Now.AddHours(23)));
        Assert.Null(ServiceEntries.TokenToSend(entry, Now.AddHours(25)));
        Assert.Equal(["Milk"], entries.Read(interest, Now).Items.Select(i => i.Title));
    }

    // ---- [S-9926] the shelf ----------------------------------------------------------------------------------

    [Fact]
    public void S9926_an_accounts_entries_round_trip_through_its_sealed_shelf_through_the_item_rules()
    {
        var entries = Entries();
        var interest = new ServiceInterest(HubId, new Reviews(), [], 5);
        entries.Reconcile([interest], [Work], _ => TimeSpan.FromMinutes(5), Now);
        entries.Complete(entries.All.Single(), new ServiceFetch(FetchStatus.Ok,
        [
            new KitItem("pr:1", "One", "octo/app #1", Lead: new KitLead(KitLeadKind.Icon, "PullRequest"), When: KitWhen.Instant(Now), Badges: [new KitBadge("Draft")], Link: new Uri("https://hub.example/octo/app/pull/1")),
            new KitItem("t:2", "Due", When: KitWhen.OnDay(new DateOnly(2026, 10, 20)), Group: new KitGroup("d", null, new DateOnly(2026, 10, 20))),
        ], Summary: new KitSummary("main · passing", Fraction: 0.5)), Now);

        var shelf = entries.ToShelf("hub:1", HubId);
        var json = JsonSerializer.Serialize(shelf, SyncJsonContext.Default.ServiceShelf);
        var back = JsonSerializer.Deserialize(json, SyncJsonContext.Default.ServiceShelf)!;

        // A tampered link on the way back is dropped, the row kept.
        back.Entries[0].Items.Add(new StoredKitItem { Id = "pr:3", Title = "Off-site", Link = "https://evil.example/x" });

        var fresh = Entries();
        fresh.Reconcile([interest], [Work], _ => TimeSpan.FromMinutes(5), Now);
        var repairs = new List<string>();
        fresh.Seed(back, "hub:1", Now.AddMinutes(1), repairs);

        var read = fresh.Read(interest, Now.AddMinutes(1));
        Assert.Equal(["t:2", "pr:1", "pr:3"], read.Items.Select(i => i.Id)); // newest first: a day ahead, an instant, no time
        var before = entries.Read(interest, Now).Items.Single(i => i.Id == "pr:1");
        var after = read.Items.Single(i => i.Id == "pr:1");
        Assert.Equal(before with { Badges = null }, after with { Badges = null });
        Assert.Equal(before.Badges!, after.Badges!);
        Assert.Equal(entries.Read(interest, Now).Items.Single(i => i.Id == "t:2"), read.Items.Single(i => i.Id == "t:2"));
        Assert.Null(read.Items.Single(i => i.Id == "pr:3").Link);
        Assert.Empty(fresh.Due(Now.AddMinutes(1))); // fresh for its real age: not asked again at once
    }

    [Fact]
    public void S9926_a_shelf_of_another_account_or_provider_is_not_read()
    {
        var shelf = new ServiceShelf { Owner = "hub:1", Provider = "other" };
        Assert.Null(ServiceShelves.Validate(shelf, "hub:1", HubId.Value, ["hub.example"], 200, []));
        Assert.Null(ServiceShelves.Validate(new ServiceShelf { Owner = "hub:1", Provider = HubId.Value }, "hub:2", HubId.Value, ["hub.example"], 200, []));
    }

    [Fact]
    public void S9926_the_service_kind_is_an_account_kind_its_shelves_are_swept_with()
    {
        var shelf = SyncVault.ShelfName(SyncKind.Service, "hub:1");
        Assert.StartsWith("service-", shelf, StringComparison.Ordinal);
        Assert.Empty(ShelfSweep.Orphans([shelf], ["hub:1"]));
        Assert.Equal([shelf], ShelfSweep.Orphans([shelf], ["hub:2"]));
    }

    [Fact]
    public async Task S9929_a_recording_parses_and_answers_by_address_in_order()
    {
        var replay = Recording();
        var first = await replay.SendAsync("a", Hub, new ServiceRequest(HttpVerb.Get, new("https://api.hub.example/search/issues?q=review"), null, "search", "k"), null, CancellationToken.None);
        var again = await replay.SendAsync("a", Hub, new ServiceRequest(HttpVerb.Get, new("https://api.hub.example/search/issues?q=review"), null, "search", "k"), "\"p1\"", CancellationToken.None);

        Assert.Equal(200, first.Status);
        Assert.Equal(304, again.Status);
        Assert.Null(FixtureReplay.Parse("{}", out var problem));
        Assert.NotNull(problem);
    }
}
