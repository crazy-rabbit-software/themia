#if DEBUG
using Themia.App.Services.Mail;
using Themia.App.Shell.Gallery;
using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2711]): sample mail held in memory, through a fixture
/// <see cref="IMailSource"/>, so <c>--widget-png Email --state messages</c> draws the header's compose
/// button ([S-558]) and the rows without an account. <c>accounts</c> reads two accounts, so the
/// compose button asks which; <c>empty</c> is an empty folder, which still offers compose.
/// </summary>
internal sealed partial class EmailView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["messages", "accounts", "empty"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.Now;
        MailAccount work = new("preview-work", AccountProvider.Microsoft, "Work", "me@contoso.com", null);
        MailAccount home = new("preview-home", AccountProvider.Imap, "Home", "me@fastmail.com", null);

        MailMessage Sample(MailAccount account, int minutes, string from, string subject, string preview, bool read)
            => new(account.Id, account.Provider, MailFolder.Inbox, "m" + minutes, subject, from, "sender@example.com", now.AddMinutes(-minutes), read, preview, string.Empty);

        MailMessage[] messages = state == "empty" ? [] :
        [
            Sample(work, 4, "Dana Ruiz", "Quarterly numbers", "Hi all, attached are the numbers for the review on Thursday.", false),
            Sample(home, 38, "Lena Fischer", "Saturday hike", "Shall we start at nine? The forecast looks dry until the afternoon.", false),
            Sample(work, 95, "Build service", "Nightly build passed", "All 1,204 tests passed on main.", true),
            Sample(work, 300, "Priya Natarajan", "Re: Launch checklist", "I ticked off the store listing; the screenshots are next.", true),
            Sample(home, 1500, "Bookshop", "Your order has shipped", "Your parcel is on its way and should arrive on Tuesday.", true),
        ];

        MailAccount[] accounts = state == "accounts" ? [work, home] : [work];
        _config.AccountIds = state == "accounts" ? [] : [work.Id];

        if (_source is not null)
        {
            _source.AccountsChanged -= OnAccountsChanged;
        }

        CloseFeed();
        _source = new PreviewMailSource(accounts, [.. messages.Where(m => accounts.Any(a => a.Id == m.AccountId))], now.AddMinutes(-3));
        Reopen();
        Render();
        return Task.CompletedTask;
    }

    /// <summary>The fixture: fixed accounts and one stored list, fetched a few minutes ago; nothing reaches the shell.</summary>
    private sealed class PreviewMailSource(IReadOnlyList<MailAccount> accounts, IReadOnlyList<MailMessage> messages, DateTimeOffset at) : IMailSource
    {
        public event Action? AccountsChanged { add { } remove { } }

        public IReadOnlyList<MailAccount> Accounts => accounts;

        public IMailFeed Open(MailQuery query)
            => new Feed(new MailFeedSnapshot(
                messages,
                [.. accounts.Select(a => new MailAccountOutcome(a.Id, MailAccountState.Ok, FetchStatus.Ok, null, At: at))],
                false));

        public Task<string?> ChangeAsync(MailMessage message, MailChange change) => Task.FromResult<string?>(null);

        public bool Open(MailMessage message) => true;

        public bool Reply(MailMessage message) => true;

        public bool Compose(MailAccount account, IReadOnlyList<MailMessage> known) => true;

        private sealed class Feed(MailFeedSnapshot snapshot) : IMailFeed
        {
            public event Action? Updated { add { } remove { } }

            public MailFeedSnapshot Snapshot => snapshot;

            public void Refresh()
            {
                // ignore: the fixture has nothing newer to fetch.
            }

            public void Dispose()
            {
                // ignore: nothing held.
            }
        }
    }
}
#endif
