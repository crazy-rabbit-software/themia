using System.Net.Http;
using Themia.App.Services.Accounts;
using Themia.Core.Localization;
using Themia.Core.Accounts;
using Themia.Core.Developer;

namespace Themia.App.Services.Developer;

/// <summary>
/// Where Claude usage widgets read from: the app's one <see cref="ClaudeUsageService"/>.
/// Everything here is on the UI thread.
/// </summary>
internal interface IClaudeUsageSource
{
    /// <summary>Raised when a Claude account is added, removed or changes health.</summary>
    event Action? AccountsChanged;

    /// <summary>Every Claude account signed in to Themia.</summary>
    IReadOnlyList<DevAccount> Accounts { get; }

    /// <summary>A shared feed of one account's usage; <paramref name="accountId"/> empty for the first account.</summary>
    IDevFeed<ClaudeUsageReport> Open(string accountId, TimeSpan refresh);
}

/// <summary>
/// [S-633][S-634] Claude subscription usage for every Claude usage widget, read from Anthropic's
/// (undocumented) usage endpoint through Themia's own Claude account.
/// <para>
/// <see cref="AccountService"/> does the hard parts: it renews the OAuth token when stale, answers a
/// 401 with one renewal and one retry, and sends the CLI's user agent and the OAuth beta header the
/// endpoint wants. What is left here is reading the answer strictly and saying what a refusal
/// means. Nothing here ever deletes the account.
/// </para>
/// </summary>
internal sealed class ClaudeUsageService : IClaudeUsageSource, IDisposable
{
    private const string Scope = "claude";

    private readonly AccountService _accounts;
    private readonly DevFeedHub _hub;
    private bool _disposed;

    public ClaudeUsageService(AccountService accounts, Ticker ticker)
    {
        _accounts = accounts;
        _hub = new DevFeedHub(Scope, ticker, () => Accounts);
        _accounts.AccountsChanged += OnAccountsChanged;
    }

    public event Action? AccountsChanged;

    public IReadOnlyList<DevAccount> Accounts =>
        [.. _accounts.WithFeature(AccountFeatures.ClaudeUsage).Select(a => new DevAccount(a.Id, a.Label, a.Problem))];

    public IDevFeed<ClaudeUsageReport> Open(string accountId, TimeSpan refresh)
    {
        IReadOnlyList<string> chosen = string.IsNullOrWhiteSpace(accountId) ? [] : [accountId];

        return _hub.Open(new DevFeedSpec<ClaudeUsageReport, ClaudeUsageReport>(
            $"usage|{accountId}|{refresh.TotalMinutes}",
            chosen,
            Single: true,
            refresh,
            FetchAsync,
            parts => parts[0]));
    }

    private async Task<DevPart<ClaudeUsageReport>> FetchAsync(string accountId, CancellationToken cancellation)
    {
        var request = new RequestTemplate(HttpMethod.Get, OAuthClients.Claude.UsageUrl);
        request.Headers.Add(("Accept", "application/json"));

        AccountReply reply;
        try
        {
            reply = await _accounts.SendAsync(accountId, request, cancellation).ConfigureAwait(true);
        }
        catch (AccountException ex) when (ex.HttpStatus is >= 400 and not 401 and not 429)
        {
            // [S-634] The status in words that say what to do, with Anthropic's own reason. A 401 the
            // service could not mend is already "sign in again"; a 429 is the hub's to wait out.
            throw new AccountException(ex.Error, ex.Message, ex.HttpStatus, ex)
            {
                ProviderMessage = ClaudeUsageRules.MapError(ex.HttpStatus, ex.ProviderMessage, Strings.Current),
                RetryAt = ex.RetryAt,
            };
        }

        var parsed = ClaudeUsageRules.Parse(reply.Body, Strings.Current);
        if (parsed.Report is not { } report)
        {
            // [S-633] Loud: the endpoint changed shape. Never an all-zero default.
            throw new AccountException(AccountError.BadResponse, parsed.Error ?? "Unexpected usage response", reply.Status)
            {
                ProviderMessage = parsed.Error,
            };
        }

        return new DevPart<ClaudeUsageReport>(report);
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
        AccountsChanged = null;
    }
}
