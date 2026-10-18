using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-9902]..[S-9923] The service layer's pure core on synthetic descriptors shaped on the three
/// pilots' documented replies (GitHub REST, Linear GraphQL, Todoist Sync), so an assumption that holds
/// for one service only fails here before any service exists (docs/WIDGET-KIT.md section 11.2, step 10).
/// </summary>
public sealed partial class ServiceLayerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 9, 0, 0, TimeSpan.Zero);

    private static readonly CadenceRow Cadence = new()
    {
        Id = new("testService"), Label = LocalText.Fixed("Test"), Glyph = "", Choices = [1, 5, 15], DefaultMinutes = 5, Rank = 9,
    };

    internal static readonly ServiceDescriptor GitHubShaped = new()
    {
        Provider = new("hubShaped"), Feature = new("hubShaped"), Cadence = Cadence,
        Transport = TransportKind.RestJson, ApiRoot = new("https://api.hub.example/"),
        SignIn = new([new PastedToken(new("https://hub.example/settings/tokens"), new TokenShape(20, 255, TokenShape.Word))]),
        Pinned = [new HeaderPin("Accept", "application/vnd.hub+json")],
        Limits = RateLimitRulesTests.GitHubShaped,
        Errors = new ErrorScheme(ErrorBodyKind.None, null, new Dictionary<string, FetchStatus>(), [new MessageRule("secondary rate limit", FetchStatus.RateLimited)], null),
        Freshness = FreshnessHints.ETag,
        WebHosts = ["hub.example"],
    };

    internal static readonly ServiceDescriptor LinearShaped = new()
    {
        Provider = new("graphShaped"), Feature = new("graphShaped"), Cadence = Cadence,
        Transport = TransportKind.GraphQL, ApiRoot = new("https://api.graph.example/graphql"),
        SignIn = new([new PastedToken(new("https://graph.example/settings/api"), new TokenShape(20, 128, TokenShape.Word, ["lin_api_"]), TokenScheme.Raw)]),
        Limits = RateLimitRulesTests.LinearShaped,
        Errors = new ErrorScheme(ErrorBodyKind.GraphQLErrors, null, new Dictionary<string, FetchStatus>
        {
            ["RATELIMITED"] = FetchStatus.RateLimited,
            ["AUTHENTICATION_ERROR"] = FetchStatus.SignInRequired,
            ["FORBIDDEN"] = FetchStatus.Forbidden,
        }, [], null),
        WebHosts = ["graph.example"],
    };

    internal static readonly ServiceDescriptor TodoistShaped = new()
    {
        Provider = new("syncShaped"), Feature = new("syncShaped"), Cadence = Cadence,
        Transport = TransportKind.FormPost, ApiRoot = new("https://api.sync.example/api/v1/"),
        SignIn = new([new PastedToken(new("https://sync.example/app/settings/integrations"), new TokenShape(40, 40, TokenShape.Hex))]),
        Limits = RateLimitRulesTests.TodoistShaped,
        Errors = new ErrorScheme(ErrorBodyKind.JsonCode, "error_tag", new Dictionary<string, FetchStatus>
        {
            ["UNAUTHORIZED"] = FetchStatus.SignInRequired,
            ["LIMITS_REACHED"] = FetchStatus.RateLimited,
        }, [], "error_extra.retry_after"),
        MaxPages = 1, ItemBound = 5000,
        WebHosts = ["app.sync.example"],
    };

    private static ServiceReply Reply(int status, string body = "", params (string Name, string Value)[] headers)
        => new(status, RateLimitRulesTests.Headers(headers), Encoding.UTF8.GetBytes(body));

    private static string Epoch(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

    // ---- [S-9921] classification -------------------------------------------------------------------

    [Fact]
    public void S9921_a_spent_budget_on_a_403_is_a_throttle_until_the_reset_in_its_bucket()
    {
        var reset = Now.AddMinutes(40);
        var verdict = ServiceFailures.Classify(GitHubShaped,
            Reply(403, """{"message":"API rate limit exceeded"}""", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", Epoch(reset)), ("X-RateLimit-Resource", "search")), Now);

        Assert.Equal(FetchStatus.RateLimited, verdict.Status);
        Assert.Equal(reset, verdict.RetryAt);
        Assert.Equal("search", verdict.Bucket);
    }

    [Fact]
    public void S9921_a_throttle_said_in_words_with_budget_left_waits_the_default()
    {
        var verdict = ServiceFailures.Classify(GitHubShaped,
            Reply(403, """{"message":"You have exceeded a secondary rate limit. Please wait a few minutes."}""", ("X-RateLimit-Remaining", "4000")), Now);

        Assert.Equal(FetchStatus.RateLimited, verdict.Status);
        Assert.Equal(Now + Themia.Core.Accounts.RateLimitRules.DefaultWait, verdict.RetryAt);
    }

    [Theory]
    [InlineData(401, FetchStatus.SignInRequired)]
    [InlineData(403, FetchStatus.Forbidden)]
    [InlineData(404, FetchStatus.NotFound)]
    [InlineData(410, FetchStatus.NotFound)]
    [InlineData(422, FetchStatus.BadResponse)]
    [InlineData(500, FetchStatus.BadResponse)]
    [InlineData(503, FetchStatus.BadResponse)]
    public void S9921_a_status_alone_says_what_it_means(int status, FetchStatus expected)
    {
        var verdict = ServiceFailures.Classify(GitHubShaped, Reply(status, """{"message":"x"}"""), Now);

        Assert.Equal(expected, verdict.Status);
        Assert.Null(verdict.RetryAt);
    }

    [Fact]
    public void S9921_an_anonymous_401_is_a_refusal_there_is_no_sign_in_to_mend()
        => Assert.Equal(FetchStatus.Forbidden, ServiceFailures.Classify(GitHubShaped, Reply(401), Now, anonymous: true).Status);

    [Fact]
    public void S9921_a_graphql_throttle_on_a_400_is_known_by_its_code_and_waits_for_the_reset_in_milliseconds()
    {
        var reset = Now.AddMinutes(25);
        var body = """{"errors":[{"message":"Rate limit exceeded","extensions":{"code":"RATELIMITED"}}]}""";
        var verdict = ServiceFailures.Classify(LinearShaped,
            Reply(400, body, ("X-RateLimit-Requests-Remaining", "12"), ("X-RateLimit-Requests-Reset", reset.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture))), Now);

        Assert.Equal(FetchStatus.RateLimited, verdict.Status);
        Assert.Equal(reset, verdict.RetryAt);
    }

    [Fact]
    public void S9921_a_graphql_200_with_errors_and_no_list_is_classified_by_its_codes()
    {
        var body = """{"data":null,"errors":[{"message":"Authentication required","extensions":{"code":"AUTHENTICATION_ERROR"}}]}""";
        var envelope = GraphQLEnvelope.Read(Encoding.UTF8.GetBytes(body), "viewer.assignedIssues");

        var verdict = ServiceFailures.Classify(LinearShaped, Reply(200, body), Now, graphQL: envelope);

        Assert.Equal(FetchStatus.SignInRequired, verdict.Status);
    }

    [Fact]
    public void S9922_partial_data_is_used_when_the_lists_own_path_is_there_and_the_errors_are_the_detail()
    {
        var body = """{"data":{"viewer":{"assignedIssues":{"nodes":[]}}},"errors":[{"message":"team hidden","path":["viewer","team"],"extensions":{"code":"FORBIDDEN"}}]}""";
        var envelope = GraphQLEnvelope.Read(Encoding.UTF8.GetBytes(body), "viewer.assignedIssues");

        Assert.NotNull(envelope);
        Assert.True(envelope.HasList);
        Assert.Equal("viewer.team", envelope.Errors[0].Path);

        var verdict = ServiceFailures.Classify(LinearShaped, Reply(200, body), Now, graphQL: envelope);
        Assert.True(verdict.Ok);
        Assert.Contains("team hidden", verdict.Detail, StringComparison.Ordinal);

        using var data = JsonDocument.Parse(envelope.Data);
        Assert.True(data.RootElement.TryGetProperty("viewer", out _));
    }

    [Fact]
    public void S9921_a_body_code_wait_is_read_from_its_field()
    {
        var verdict = ServiceFailures.Classify(TodoistShaped,
            Reply(429, """{"error":"Too many requests","error_tag":"LIMITS_REACHED","error_extra":{"retry_after":30}}"""), Now);

        Assert.Equal(FetchStatus.RateLimited, verdict.Status);
        Assert.Equal(Now.AddSeconds(30), verdict.RetryAt);
    }

    [Fact]
    public void S9921_a_401_carrying_retry_after_is_a_sign_in_not_a_wait()
    {
        var verdict = ServiceFailures.Classify(TodoistShaped,
            Reply(401, """{"error":"Unauthorized","error_tag":"UNAUTHORIZED"}""", ("Retry-After", "600")), Now);

        Assert.Equal(FetchStatus.SignInRequired, verdict.Status);
        Assert.Null(verdict.RetryAt);
    }

    [Fact]
    public void S9921_a_code_the_scheme_does_not_know_is_unexpected_and_named()
    {
        var verdict = ServiceFailures.Classify(TodoistShaped, Reply(400, """{"error_tag":"SOMETHING_NEW"}"""), Now);

        Assert.Equal(FetchStatus.BadResponse, verdict.Status);
        Assert.Equal("SOMETHING_NEW", verdict.UnknownCode);
    }

    [Fact]
    public void S9921_a_body_that_is_not_json_falls_back_to_the_status()
        => Assert.Equal(FetchStatus.NotFound, ServiceFailures.Classify(TodoistShaped, Reply(404, "<html>gone</html>"), Now).Status);

    [Fact]
    public void S9921_a_success_that_spent_the_budget_is_ok_and_blocks_its_bucket_until_the_reset()
    {
        var reset = Now.AddMinutes(9);
        var verdict = ServiceFailures.Classify(GitHubShaped, Reply(200, "[]", ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", Epoch(reset)), ("X-RateLimit-Resource", "core")), Now);

        Assert.True(verdict.Ok);
        Assert.Equal(reset, verdict.RetryAt);
        Assert.Equal("core", verdict.Bucket);
    }

    [Fact]
    public void S9921_a_304_is_ok()
        => Assert.True(ServiceFailures.Classify(GitHubShaped, Reply(304), Now).Ok);

    // ---- [S-9906] paging ---------------------------------------------------------------------------------

    private static readonly Uri FirstPage = new("https://api.hub.example/search?q=x&per_page=2");

    [Fact]
    public void S9906_the_link_headers_next_page_is_followed_on_the_same_https_origin_only()
    {
        const string link = "<https://api.hub.example/search?q=x&page=2>; rel=\"next\", <https://api.hub.example/search?q=x&page=5>; rel=\"last\"";
        Assert.Equal(new Uri("https://api.hub.example/search?q=x&page=2"), LinkPaging.Next(link, FirstPage));

        Assert.Null(LinkPaging.Next("<https://evil.example/search?page=2>; rel=\"next\"", FirstPage));
        Assert.Null(LinkPaging.Next("<http://api.hub.example/search?page=2>; rel=\"next\"", FirstPage));
        Assert.Null(LinkPaging.Next("<https://user:pw@api.hub.example/search?page=2>; rel=\"next\"", FirstPage));
        Assert.Null(LinkPaging.Next("<https://api.hub.example/search?page=1>; rel=\"prev\"", FirstPage));
        Assert.Null(LinkPaging.Next(null, FirstPage));
        Assert.Equal(new Uri("https://api.hub.example/a?x=1,2"), LinkPaging.Next("<https://api.hub.example/a?x=1,2>; rel=\"next\"", FirstPage));
    }

    private static KitChunk Chunk(int count, string? cursor = null)
        => new([.. Enumerable.Range(0, count).Select(i => new KitItem($"id{i}", $"t{i}"))], cursor);

    [Fact]
    public void S9906_the_pager_stops_at_the_count_the_bounds_an_empty_page_and_the_end()
    {
        var enough = new ServicePager(PagingKind.BodyCursor, wanted: 3, maxPages: 5, itemBound: 200, pageSize: 2);
        Assert.IsType<PageAt.Cursor>(enough.Next(FirstPage, null, Chunk(2, "c1")));
        Assert.Null(enough.Next(FirstPage, null, Chunk(2, "c2")));
        Assert.Equal(PageStop.Enough, enough.Stop);

        var bounded = new ServicePager(PagingKind.BodyCursor, wanted: 100, maxPages: 2, itemBound: 200, pageSize: 2);
        bounded.Next(FirstPage, null, Chunk(2, "a"));
        Assert.Null(bounded.Next(FirstPage, null, Chunk(2, "b")));
        Assert.Equal(PageStop.Bound, bounded.Stop);

        var empty = new ServicePager(PagingKind.BodyCursor, 100, 5, 200, 2);
        Assert.Null(empty.Next(FirstPage, null, Chunk(0, "a")));
        Assert.Equal(PageStop.EmptyPage, empty.Stop);

        var end = new ServicePager(PagingKind.BodyCursor, 100, 5, 200, 2);
        Assert.Null(end.Next(FirstPage, null, Chunk(2, null)));
        Assert.Equal(PageStop.End, end.Stop);

        var single = new ServicePager(PagingKind.None, 100, 5, 200, 2);
        Assert.Null(single.Next(FirstPage, null, Chunk(2, "ignored")));
        Assert.Equal(PageStop.End, single.Stop);
    }

    [Fact]
    public void S9906_a_cursor_that_repeats_is_a_loop_never_an_endless_fetch()
    {
        var pager = new ServicePager(PagingKind.BodyCursor, 100, 10, 200, 2);
        Assert.NotNull(pager.Next(FirstPage, null, Chunk(2, "same")));
        Assert.Null(pager.Next(FirstPage, null, Chunk(2, "same")));
        Assert.Equal(PageStop.Loop, pager.Stop);

        var links = new ServicePager(PagingKind.LinkHeader, 100, 10, 200, 2);
        Assert.Null(links.Next(FirstPage, $"<{FirstPage.AbsoluteUri}>; rel=\"next\"", Chunk(2)));
        Assert.Equal(PageStop.Loop, links.Stop);
    }

    [Fact]
    public void S9906_offsets_step_by_what_came_and_stop_at_a_short_page()
    {
        var pager = new ServicePager(PagingKind.OffsetLimit, 100, 10, 200, 2);
        Assert.Equal(new PageAt.Offset(2), pager.Next(FirstPage, null, Chunk(2)));
        Assert.Null(pager.Next(FirstPage, null, Chunk(1)));
        Assert.Equal(PageStop.End, pager.Stop);
    }

    // ---- [S-9908] polling ---------------------------------------------------------------------------------

    [Fact]
    public void S9908_the_next_poll_keeps_the_floors_and_waits_out_a_block()
    {
        Assert.Equal(Now.AddMinutes(5), ServicePolling.NextPoll(Now, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), null));
        Assert.Equal(Now.AddMinutes(2), ServicePolling.NextPoll(Now, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2), null));
        Assert.Equal(Now.AddMinutes(15), ServicePolling.NextPoll(Now, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), null, TimeSpan.FromMinutes(15)));
        Assert.Equal(Now.AddMinutes(40), ServicePolling.NextPoll(Now, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), Now.AddMinutes(40)));
        Assert.Equal(Now.AddMinutes(2), ServicePolling.NextPoll(Now, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), Now.AddSeconds(30)));
    }

    // ---- [S-9909] merge -------------------------------------------------------------------------------------

    [Fact]
    public void S9909_accounts_merge_newest_first_once_each_and_cut_to_the_count()
    {
        var work = new KitItem[]
        {
            new("pr:1", "one", When: KitWhen.Instant(Now.AddHours(-1))),
            new("pr:2", "two", When: KitWhen.Instant(Now.AddHours(-3))),
        };
        var home = new KitItem[]
        {
            new("pr:2", "two again", When: KitWhen.Instant(Now.AddHours(-3))),
            new("pr:3", "three", When: KitWhen.Instant(Now.AddMinutes(-5))),
            new("t:9", "a day", When: KitWhen.OnDay(DateOnly.FromDateTime(Now.UtcDateTime).AddDays(-2))),
            new("x", "no time"),
        };

        var merged = KitMerge.Merge([work, home], count: 4);

        Assert.Equal(["pr:3", "pr:1", "pr:2", "t:9"], merged.Select(i => i.Id));
        Assert.Equal("two", merged[2].Title); // the first account's copy
    }

    // ---- [S-9922] [S-9923] envelopes ----------------------------------------------------------------------

    internal sealed record IssueVariables(int First, string? After);

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(IssueVariables))]
    internal sealed partial class TestWireContext : JsonSerializerContext;

    [Fact]
    public void S9922_a_graphql_request_is_the_query_and_its_variables_through_the_services_context()
    {
        var body = GraphQLEnvelope.Request("query Q($first: Int!) { viewer { id } }", new IssueVariables(50, null), TestWireContext.Default.IssueVariables);

        Assert.Equal("application/json", body.ContentType);
        using var document = JsonDocument.Parse(body.Utf8);
        Assert.Equal("query Q($first: Int!) { viewer { id } }", document.RootElement.GetProperty("query").GetString());
        Assert.Equal(50, document.RootElement.GetProperty("variables").GetProperty("first").GetInt32());
    }

    [Fact]
    public void S9922_an_answer_that_is_not_an_envelope_is_none()
    {
        Assert.Null(GraphQLEnvelope.Read("[]"u8, "viewer"));
        Assert.Null(GraphQLEnvelope.Read("{}"u8, "viewer"));
        Assert.Null(GraphQLEnvelope.Read("not json"u8, "viewer"));
        Assert.False(GraphQLEnvelope.Read("""{"data":{"viewer":null}}"""u8, "viewer")!.HasList);
    }

    [Fact]
    public void S9923_a_form_post_escapes_its_fields_in_order()
    {
        var body = FormBody.Encode([new("sync_token", "*"), new("resource_types", """["items","projects"]""")]);

        Assert.Equal("application/x-www-form-urlencoded", body.ContentType);
        Assert.Equal("sync_token=%2A&resource_types=%5B%22items%22%2C%22projects%22%5D", Encoding.UTF8.GetString(body.Utf8.Span));
    }

    // ---- [S-9903] the query contract -------------------------------------------------------------------------

    internal sealed record IssueWire(IReadOnlyList<string> Titles);

    [JsonSerializable(typeof(IssueWire))]
    internal sealed partial class IssueWireContext : JsonSerializerContext;

    private sealed class TitlesQuery : IServiceQuery<IssueWire>
    {
        public string Key => "titles";
        public PagingKind Paging => PagingKind.None;
        public FreshnessHints Freshness => FreshnessHints.ETag;
        public IReadOnlyList<IServiceStep> Steps => [];
        public System.Text.Json.Serialization.Metadata.JsonTypeInfo<IssueWire> Wire => IssueWireContext.Default.IssueWire;

        public ServiceRequest Build(PageAt page, IReadOnlyDictionary<string, string> fromSteps)
            => new(HttpVerb.Get, new("https://api.hub.example/titles"), null, "core", Key);

        public KitChunk Map(IssueWire wire, IReadOnlyDictionary<string, string> fromSteps, DateTimeOffset now)
            => new([.. wire.Titles.Select((t, i) => new KitItem($"t{i}", t))]);
    }

    [Fact]
    public void S9903_a_query_answers_through_its_source_generated_wire_type_or_says_where_it_broke()
    {
        IServiceQuery query = new TitlesQuery();

        var chunk = query.Answer("""{"Titles":["a","b"]}"""u8.ToArray(), new Dictionary<string, string>(), Now, out var problem);
        Assert.Null(problem);
        Assert.Equal(["a", "b"], chunk!.Items.Select(i => i.Title));

        Assert.Null(query.Answer("""{"Titles":[1]}"""u8.ToArray(), new Dictionary<string, string>(), Now, out problem));
        Assert.Contains("$.Titles", problem, StringComparison.Ordinal);

        Assert.Null(query.Answer(ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>(), Now, out problem));
        Assert.NotNull(problem);
    }

    // ---- [S-9910]..[S-9917] sign-in data -----------------------------------------------------------------

    [Theory]
    [InlineData(LoopbackHost.Localhost, "/", "http://localhost:5050/")]
    [InlineData(LoopbackHost.Loopback4, "", "http://127.0.0.1:5050")]
    [InlineData(LoopbackHost.Loopback4, "/callback", "http://127.0.0.1:5050/callback")]
    public void S9913_a_loopback_redirect_is_written_as_its_rule_says(LoopbackHost host, string path, string expected)
        => Assert.Equal(expected, new LoopbackRule(host, path).RedirectUri(5050));

    [Fact]
    public void S9913_a_fixed_port_list_is_not_any_port()
    {
        Assert.True(new LoopbackRule(LoopbackHost.Loopback4, "/").AnyPort);
        Assert.False(new LoopbackRule(LoopbackHost.Loopback4, "/", [53682, 53683]).AnyPort);
    }

    [Fact]
    public void S9915_a_pasted_token_must_have_its_shape_before_it_is_tried()
    {
        var hex = new TokenShape(40, 40, TokenShape.Hex);
        Assert.Equal(new string('a', 40), hex.Accept("  " + new string('a', 40) + "\n"));
        Assert.Null(hex.Accept(new string('a', 39)));
        Assert.Null(hex.Accept(new string('g', 40)));
        Assert.Null(hex.Accept(null));

        var prefixed = new TokenShape(20, 128, TokenShape.Word, ["lin_api_"]);
        Assert.NotNull(prefixed.Accept("lin_api_" + new string('x', 30)));
        Assert.Null(prefixed.Accept("ghp_" + new string('x', 30)));
        Assert.Null(prefixed.Accept("lin_api_" + new string('x', 20) + " spaces"));
    }

    [Fact]
    public void S9910_scopes_join_as_the_provider_wants()
    {
        Assert.Equal("read write", Scopes.Join(["read", "write"], ScopeSeparator.Space));
        Assert.Equal("data:read,user", Scopes.Join(["data:read", "user"], ScopeSeparator.Comma));
    }

    [Fact]
    public void S9917_build_secrets_parse_by_provider_key_and_a_broken_entry_fails_them_all()
    {
        var secrets = ClientSecretRules.Parse("google=GOCSPX-abc_DEF-123; todoist=0123456789abcdef", out var problem);
        Assert.Null(problem);
        Assert.Equal("GOCSPX-abc_DEF-123", secrets!["google"]);
        Assert.Equal("0123456789abcdef", secrets["todoist"]);
        Assert.Equal("google, todoist (2)", ClientSecretRules.Describe(secrets));

        Assert.Empty(ClientSecretRules.Parse(null, out _)!);
        Assert.Empty(ClientSecretRules.Parse("  ", out _)!);

        Assert.Null(ClientSecretRules.Parse("google=GOCSPX-abc;todoist", out problem));
        Assert.NotNull(problem);
        Assert.Null(ClientSecretRules.Parse("Google=GOCSPX-abcdefgh", out _));
        Assert.Null(ClientSecretRules.Parse("google=has space inside", out _));
        Assert.Null(ClientSecretRules.Parse("google=short", out _));
        Assert.Null(ClientSecretRules.Parse("google=GOCSPX-abcdefgh;google=GOCSPX-abcdefgh", out _));
    }
}
