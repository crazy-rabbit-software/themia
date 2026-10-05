using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Developer;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Services.Developer;

/// <summary>[S-610] What the repository widget asks: one repository, through the first chosen account.</summary>
internal sealed record RepoQuery(
    IReadOnlyList<string> AccountIds,
    RepoRef Repo,
    int Commits,
    int Pulls,
    PullFilter Filter,
    PullOrder Order,
    int Issues,
    int Runs,
    TimeSpan Refresh)
{
    /// <summary>Everything that changes the answer; never the token ([S-624]).</summary>
    public string Key => string.Create(
        CultureInfo.InvariantCulture,
        $"repo|{string.Join(',', AccountIds)}|{Repo.FullName.ToUpperInvariant()}|{Commits},{Pulls},{Filter},{Order},{Issues},{Runs}|{Refresh.TotalMinutes}");
}

/// <summary>Which search a list widget runs.</summary>
internal enum SearchKind
{
    /// <summary>[S-615] Issues assigned to the account.</summary>
    Assigned,

    /// <summary>[S-616] Pull requests waiting on the account's review.</summary>
    Reviews,
}

internal sealed record SearchQuery(
    IReadOnlyList<string> AccountIds,
    SearchKind Kind,
    bool IncludeMentioned,
    bool IncludeDrafts,
    int Count,
    TimeSpan Refresh)
{
    public string Key => string.Create(
        CultureInfo.InvariantCulture,
        $"search|{Kind}|{string.Join(',', AccountIds)}|{IncludeMentioned},{IncludeDrafts},{Count}|{Refresh.TotalMinutes}");
}

internal sealed record NotificationQuery(
    IReadOnlyList<string> AccountIds,
    bool IncludeRead,
    bool ParticipatingOnly,
    int Count,
    TimeSpan Refresh)
{
    public string Key => string.Create(
        CultureInfo.InvariantCulture,
        $"notifications|{string.Join(',', AccountIds)}|{IncludeRead},{ParticipatingOnly},{Count}|{Refresh.TotalMinutes}");
}

/// <summary>
/// Where the GitHub widgets read from: the app's one <see cref="GitHubService"/>.
/// Everything here is on the UI thread.
/// </summary>
internal interface IGitHubSource
{
    /// <summary>Raised when a GitHub account is added, removed, renamed or changes health.</summary>
    event Action? AccountsChanged;

    /// <summary>Every GitHub account signed in to Themia, in the order they were added.</summary>
    IReadOnlyList<DevAccount> Accounts { get; }

    IDevFeed<RepoActivity> OpenRepo(RepoQuery query);

    IDevFeed<IReadOnlyList<SearchItem>> OpenSearch(SearchQuery query);

    IDevFeed<IReadOnlyList<NotificationItem>> OpenNotifications(NotificationQuery query);

    /// <summary>[S-621] Mark one thread read. Null when it worked, else a sentence why not; never throws.</summary>
    Task<string?> MarkReadAsync(NotificationItem item);

    /// <summary>[S-621] Mark every thread in the item's repository read. As <see cref="MarkReadAsync"/>.</summary>
    Task<string?> MarkRepositoryReadAsync(NotificationItem item);

    /// <summary>[S-748] Mark every notification of each account read. As <see cref="MarkReadAsync"/>; the first problem wins.</summary>
    Task<string?> MarkAllReadAsync(IReadOnlyList<string> accountIds);
}

/// <summary>
/// [S-610]..[S-625] GitHub for every GitHub widget: one per app, reached through
/// <see cref="AppServices.GitHub"/>, built on Themia's own accounts.
/// <para>
/// One client for the four widgets. Every GET goes through one cache keyed by account and address,
/// so a repository widget and a second one on the same repository cost one request, and every
/// repeat is <b>conditional</b>: the last answer's <c>ETag</c> and <c>Last-Modified</c> go back as
/// <c>If-None-Match</c> / <c>If-Modified-Since</c>, and GitHub's <c>304</c> — which does not count
/// against the account's budget — is answered from the cache. The notifications endpoint's
/// <c>X-Poll-Interval</c> stretches that feed's cadence, and a spent budget holds every request
/// for the account until its reset ([S-625]).
/// </para>
/// <para>
/// The token is attached by <see cref="AccountService"/> and never seen here; nothing is keyed by it.
/// </para>
/// </summary>
internal sealed class GitHubService : IGitHubSource, IDisposable
{
    private const string Scope = "github";

    /// <summary>The REST version every request pins, so a GitHub-side default change cannot reshape an answer.</summary>
    private const string ApiVersion = "2022-11-28";

    /// <summary>Distinct account-and-address answers held for conditional requests.</summary>
    private const int MaxCached = 128;

    private readonly AccountService _accounts;
    private readonly DevFeedHub _hub;
    private readonly Dictionary<string, Cached> _cache = new(StringComparer.Ordinal);
    private bool _disposed;

    public GitHubService(AccountService accounts, Ticker ticker)
    {
        _accounts = accounts;
        _hub = new DevFeedHub(Scope, ticker, () => Accounts, SavedDirectory);
        _accounts.AccountsChanged += OnAccountsChanged;
    }

    /// <summary>
    /// [S-747] Where the widgets' last answers are kept between runs: local, not roaming (a list of
    /// notification titles has no business following the user to another machine), and outside
    /// the state file's folder.
    /// </summary>
    private static string SavedDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Themia.Core.Storage.AppStateStore.FolderName,
        "Cache",
        "GitHub");

    public event Action? AccountsChanged;

    public IReadOnlyList<DevAccount> Accounts =>
        [.. _accounts.WithFeature(AccountFeatures.GitHub).Select(a => new DevAccount(a.Id, a.Label, a.Problem))];

    public IDevFeed<RepoActivity> OpenRepo(RepoQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return _hub.Open(new DevFeedSpec<RepoActivity, RepoActivity>(
            query.Key, query.AccountIds, Single: true, query.Refresh,
            (id, ct) => FetchRepoAsync(id, query, ct),
            parts => parts[0],
            Anonymous: true,
            Saved: DevSavedJsonContext.Default.RepoActivity));
    }

    public IDevFeed<IReadOnlyList<SearchItem>> OpenSearch(SearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = query.Kind == SearchKind.Reviews ? GitHubRules.ReviewQuery : GitHubRules.AssignedQuery(query.IncludeMentioned);
        var drafts = query.Kind != SearchKind.Reviews || query.IncludeDrafts;

        // Drafts are dropped after the search, so ask for some spare when they will be.
        var perPage = drafts ? query.Count : Math.Min(query.Count * 2, 50);

        return _hub.Open(new DevFeedSpec<IReadOnlyList<SearchItem>, IReadOnlyList<SearchItem>>(
            query.Key, query.AccountIds, Single: false, query.Refresh,
            async (id, ct) =>
            {
                var reply = await GetAsync(id, GitHubRules.SearchUrl(Api(id), text, perPage), ct).ConfigureAwait(true);
                return new DevPart<IReadOnlyList<SearchItem>>(GitHubRules.ToSearchItems(id, Parse(reply.Body, GitHubWireContext.Default.GhSearch)));
            },
            parts => GitHubRules.MergeSearch(parts, query.Count, drafts),
            Saved: DevSavedJsonContext.Default.IReadOnlyListSearchItem));
    }

    public IDevFeed<IReadOnlyList<NotificationItem>> OpenNotifications(NotificationQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _hub.Open(new DevFeedSpec<IReadOnlyList<NotificationItem>, IReadOnlyList<NotificationItem>>(
            query.Key, query.AccountIds, Single: false, query.Refresh,
            async (id, ct) =>
            {
                var url = GitHubRules.NotificationsUrl(Api(id), query.IncludeRead, query.ParticipatingOnly, query.Count);
                var reply = await GetAsync(id, url, ct).ConfigureAwait(true);
                var items = GitHubRules.ToNotifications(id, Parse(reply.Body, GitHubWireContext.Default.ListGhNotification));
                var poll = reply.Headers.TryGetValue("X-Poll-Interval", out var header) ? GitHubPolling.PollInterval(header) : null;
                return new DevPart<IReadOnlyList<NotificationItem>>(items, poll);
            },
            parts => GitHubRules.MergeNotifications(parts, query.Count),
            Saved: DevSavedJsonContext.Default.IReadOnlyListNotificationItem));
    }

    public Task<string?> MarkReadAsync(NotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ChangeAsync(item.AccountId, HttpMethod.Patch, GitHubRules.ThreadUrl(Api(item.AccountId), item.Id), null, "mark a thread read");
    }

    public Task<string?> MarkRepositoryReadAsync(NotificationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (GitHubRules.ParseRepo(item.Repo) is not { } repo)
        {
            return Task.FromResult<string?>(Str.Shell_GitHub_BadRepo.Text());
        }

        return ChangeAsync(item.AccountId, HttpMethod.Put, GitHubRules.RepoNotificationsUrl(Api(item.AccountId), repo), "{}", $"mark {repo} read");
    }

    public async Task<string?> MarkAllReadAsync(IReadOnlyList<string> accountIds)
    {
        ArgumentNullException.ThrowIfNull(accountIds);

        string? first = null;
        foreach (var id in accountIds.Distinct(StringComparer.Ordinal))
        {
            // GitHub answers 205, or 202 when it marks a large backlog in the background.
            var problem = await ChangeAsync(id, HttpMethod.Put, GitHubRules.AllNotificationsUrl(Api(id)), "{}", "mark every notification read").ConfigureAwait(true);
            first ??= problem;
        }

        return first;
    }

    private async Task<string?> ChangeAsync(string accountId, HttpMethod method, Uri url, string? json, string what)
    {
        if (_hub.BlockedUntil(accountId, DateTimeOffset.UtcNow) is { } until)
        {
            return Str.Shell_GitHub_SlowDown.Format(("time", LocaleFormat.Time(Strings.Current, until.ToLocalTime().DateTime)));
        }

        try
        {
            await _accounts.SendJsonAsync(accountId, method, url, json).ConfigureAwait(true);
        }
        catch (AccountException ex)
        {
            Log.Warn(Scope, $"could not {what} for {accountId}: {ex.Message}");
            return ex.ProviderMessage ?? ex.Message;
        }

        Log.Info(Scope, $"{what} done for {accountId}");

        // The held list is now wrong in a way a conditional request may not notice in time: drop
        // its validators so the refresh reads the truth.
        foreach (var key in _cache.Keys.Where(k => k.StartsWith(accountId + " ", StringComparison.Ordinal) && k.Contains("/notifications", StringComparison.Ordinal)).ToList())
        {
            _cache.Remove(key);
        }

        _hub.RefreshAccount(accountId);
        return null;
    }

    // -----------------------------------------------------------------------------------------
    // Fetching
    // -----------------------------------------------------------------------------------------

    /// <summary>The account's REST root (Enterprise included); github.com when the account names none.</summary>
    private Uri Api(string accountId)
        => GitHubRules.ApiRoot(accountId == DevScreens.AnonymousId ? null : _accounts.Find(accountId)?.ApiBaseUrl);

    private async Task<DevPart<RepoActivity>> FetchRepoAsync(string accountId, RepoQuery query, CancellationToken cancellation)
    {
        var api = Api(accountId);
        var repo = query.Repo;

        GhRepo? wire;
        try
        {
            wire = Parse((await GetAsync(accountId, GitHubRules.RepoUrl(api, repo), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.GhRepo);
        }
        catch (AccountException ex) when (ex.Error == AccountError.NotFound)
        {
            // GitHub answers 404 for a private repository the token cannot see, as well as for one
            // that does not exist; the sentence has to cover both.
            throw new AccountException(AccountError.NotFound, ex.Message, ex.HttpStatus, ex)
            {
                ProviderMessage = accountId == DevScreens.AnonymousId
                    ? $"{repo.FullName} was not found. A private repository needs a GitHub account in Accounts"
                    : $"{repo.FullName} was not found, or this account cannot see it",
            };
        }

        var summary = GitHubRules.ToRepo(wire)
            ?? throw new AccountException(AccountError.BadResponse, Str.Shell_GitHub_RepoUnnamed.Text());

        // The rest is decoration around the repository's own numbers: one of them failing is
        // logged and drawn as absent, as in the classic app. A throttle or a lost sign-in is not
        // decoration — it stops the lot.
        var runs = await OptionalAsync(
            async () => GitHubRules.ToRuns(Parse((await GetAsync(accountId, GitHubRules.RunsUrl(api, repo, query.Runs), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.GhRunList), Math.Max(query.Runs, 1)),
            "workflow runs").ConfigureAwait(true) ?? [];

        var commits = query.Commits <= 0 ? [] : await OptionalAsync(
            async () => GitHubRules.ToCommits(Parse((await GetAsync(accountId, GitHubRules.CommitsUrl(api, repo, query.Commits), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.ListGhCommit), query.Commits),
            "commits").ConfigureAwait(true) ?? [];

        var pulls = query.Pulls <= 0 ? [] : await OptionalAsync(
            async () => GitHubRules.FilterSortPulls(
                GitHubRules.ToPulls(Parse((await GetAsync(accountId, GitHubRules.PullsUrl(api, repo, query.Pulls), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.ListGhPull)),
                query.Filter,
                query.Order,
                query.Pulls),
            "pull requests").ConfigureAwait(true) ?? [];

        var issues = query.Issues <= 0 ? [] : await OptionalAsync(
            async () => GitHubRules.ToIssues(Parse((await GetAsync(accountId, GitHubRules.IssuesUrl(api, repo, query.Issues), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.ListGhIssue), query.Issues),
            "issues").ConfigureAwait(true) ?? [];

        ReleaseItem? release = null;
        try
        {
            release = GitHubRules.ToRelease(Parse((await GetAsync(accountId, GitHubRules.LatestReleaseUrl(api, repo), cancellation).ConfigureAwait(true)).Body, GitHubWireContext.Default.GhRelease));
        }
        catch (AccountException ex) when (ex.Error == AccountError.NotFound)
        {
            // ignore: a repository with no release answers 404 here, which is the normal case.
        }
        catch (AccountException ex) when (ex.Error is not (AccountError.RateLimited or AccountError.SignInRequired or AccountError.UnknownAccount))
        {
            Log.Warn(Scope, $"{repo}: latest release unavailable ({ex.Message})");
        }

        return new DevPart<RepoActivity>(new RepoActivity(summary, runs, commits, pulls, issues, release));
    }

    private async Task<IReadOnlyList<T>?> OptionalAsync<T>(Func<Task<IReadOnlyList<T>>> load, string what)
    {
        try
        {
            return await load().ConfigureAwait(true);
        }
        catch (AccountException ex) when (ex.Error is not (AccountError.RateLimited or AccountError.SignInRequired or AccountError.UnknownAccount or AccountError.Offline))
        {
            Log.Warn(Scope, $"{what} unavailable: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// [S-624][S-625] One GET as an account, conditional on the answer held for the same account and
    /// address. A 304 hands back the held body; a reply that spent the account's budget holds its
    /// requests until the reset.
    /// </summary>
    private async Task<AccountReply> GetAsync(string accountId, Uri url, CancellationToken cancellation)
    {
        var now = DateTimeOffset.UtcNow;
        if (_hub.BlockedUntil(accountId, now) is { } blocked)
        {
            throw new AccountException(AccountError.RateLimited, Str.Shell_GitHub_RateLimit.Text()) { RetryAt = blocked };
        }

        var key = accountId + " " + url.AbsoluteUri;
        _cache.TryGetValue(key, out var held);

        var request = new RequestTemplate(HttpMethod.Get, url);
        request.Headers.Add(("Accept", "application/vnd.github+json"));
        request.Headers.Add(("X-GitHub-Api-Version", ApiVersion));

        if (held is not null)
        {
            request.Headers.AddRange(GitHubPolling.Conditional(held.ETag, held.LastModified));
        }

        var reply = accountId == DevScreens.AnonymousId
            ? await SendAnonymousAsync(request, cancellation).ConfigureAwait(true)
            : await _accounts.SendAsync(accountId, request, cancellation).ConfigureAwait(true);

        now = DateTimeOffset.UtcNow;
        if (RateLimitRules.WaitUntil(reply.Status, RateLimitRules.Read(reply.Headers, now), now) is { } until)
        {
            _hub.Block(accountId, until);
        }

        if (reply.IsNotModified)
        {
            if (held is null)
            {
                throw new AccountException(AccountError.BadResponse, Str.Shell_GitHub_Unexpected304.Text(), 304);
            }

            held.At = now;
            return reply with { Status = 200, Body = held.Body };
        }

        reply.Headers.TryGetValue("ETag", out var etag);
        reply.Headers.TryGetValue("Last-Modified", out var modified);

        if (etag is not null || modified is not null)
        {
            _cache[key] = new Cached(reply.Body, etag, modified) { At = now };
            Trim();
        }
        else
        {
            _cache.Remove(key);
        }

        return reply;
    }

    /// <summary>
    /// [S-746] A request without an account: the same client, redirects and throttle retries as an
    /// account request, and no proof at all — nothing to attach, nothing to renew. A failure is the
    /// same typed exception an account request throws, except that a 401 (GitHub wants a sign-in
    /// for this) is a refusal, not a broken sign-in: there is none to mend.
    /// </summary>
    private static async Task<AccountReply> SendAnonymousAsync(RequestTemplate request, CancellationToken cancellation)
    {
        var reply = await AccountHttp.SendAsync(request, CredentialKind.None, null, cancellation).ConfigureAwait(true);

        if (reply.IsSuccess || reply.IsNotModified)
        {
            return reply;
        }

        var now = DateTimeOffset.UtcNow;
        var budget = RateLimitRules.Read(reply.Headers, now);
        var error = RateLimitRules.IsThrottle(reply.Status, budget)
            ? AccountError.RateLimited
            : reply.Status == 401 ? AccountError.Forbidden : AccountException.FromStatus(reply.Status);
        var said = ErrorBody.Message(reply.Body);

        throw new AccountException(error, $"{reply.FinalUrl.Host} answered HTTP {reply.Status} without an account.", reply.Status)
        {
            ProviderMessage = said,
            RetryAt = error == AccountError.RateLimited ? RateLimitRules.WaitUntil(reply.Status, budget, now) : null,
        };
    }

    private void Trim()
    {
        while (_cache.Count > MaxCached)
        {
            var oldest = _cache.MinBy(pair => pair.Value.At).Key;
            _cache.Remove(oldest);
        }
    }

    /// <summary>A body through a source-generated contract; anything else is a bad answer, never half a model.</summary>
    private static T Parse<T>(string body, JsonTypeInfo<T> contract)
    {
        try
        {
            return JsonSerializer.Deserialize(body, contract)
                ?? throw new AccountException(AccountError.BadResponse, Str.Shell_GitHub_Empty.Text());
        }
        catch (JsonException ex)
        {
            throw new AccountException(AccountError.BadResponse, Str.Shell_GitHub_Unparsable.Format(("reason", ex.Message)), 0, ex);
        }
    }

    private void OnAccountsChanged()
    {
        _hub.AccountsChanged();
        AccountsChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _accounts.AccountsChanged -= OnAccountsChanged;
        _hub.Dispose();
        _cache.Clear();
        AccountsChanged = null;
    }

    /// <summary>The last answer for one account and address, and the validators to ask about it with.</summary>
    private sealed class Cached(string body, string? etag, string? lastModified)
    {
        public string Body { get; } = body;

        public string? ETag { get; } = etag;

        public string? LastModified { get; } = lastModified;

        public DateTimeOffset At { get; set; }
    }
}
