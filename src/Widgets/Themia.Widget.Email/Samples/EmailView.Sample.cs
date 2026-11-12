using Themia.App.Services.Mail;
using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.Email;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2711] [S-2056] The mail widget's sample: the sample day's mail held in memory (its cast,
/// ages from its moment), through a fixture <see cref="IMailSource"/> of its own, so the header's compose
/// button ([S-558]) and the rows draw without an account. Nothing reaches the shell or an account.
/// </summary>
internal sealed partial class EmailView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(twoAccounts: false, empty: false, failed: false);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The sample's mail: the work account's (<paramref name="twoAccounts"/>: and the home one's), none when
    /// <paramref name="empty"/>; <paramref name="failed"/>: the last refresh failed (no network), what is
    /// held from 20 minutes ago.
    /// </summary>
    internal void HoldSample(bool twoAccounts, bool empty, bool failed)
    {
        var now = SampleDay.Now;
        MailAccount work = new("preview-work", HostProviders.Microsoft, "Work", SampleCast.Owner.Email, null);
        MailAccount home = new("preview-home", HostProviders.Imap, "Home", SampleCast.OwnerHomeEmail, null);

        MailMessage Sample(MailAccount account, int minutes, string from, string address, string subject, string preview, bool read)
            => new(account.Id, account.Provider, MailFolder.Inbox, "m" + minutes, subject, from, address, now.AddMinutes(-minutes), read, preview, string.Empty);

        MailMessage From(SamplePerson person, MailAccount account, int minutes, string subject, string preview, bool read)
            => Sample(account, minutes, person.Name, person.Email, subject, preview, read);

        // [S-2056] Ten at work over the last day, and three at home when both accounts are shown.
        MailMessage[] messages = empty ? [] :
        [
            From(SampleCast.Ana, work, 4, "Design review at 11", "Room 4.12, or join on Teams. I'll walk through the onboarding flow first.", false),
            From(SampleCast.Dana, work, 21, "Quarterly numbers", "Hi all, attached are the numbers for Thursday's review.", false),
            From(SampleCast.Lena, home, 38, "Lunch today?", "Still on for 12:30? I booked a table by the lake.", false),
            From(SampleCast.Sam, work, 55, "Notes for our 1:1", "A few things I'd like to talk through this afternoon.", false),
            From(SampleCast.Priya, work, 72, "Re: Launch checklist", "I ticked off the store listing; the screenshots are next.", true),
            Sample(work, 95, "Build service", "builds@lumen.example", "Nightly build passed", "All 1,204 tests passed on main.", true),
            From(SampleCast.Bea, work, 140, "Budget check-in at 4", "I'll bring the updated forecast; half an hour should do.", true),
            From(SampleCast.Alex, work, 210, "Onboarding sketches", "Three directions for the first screen. I like the second best.", true),
            Sample(work, 380, "Travel desk", "travel@lumen.example", "Your tickets to Lisbon", "Your flights for the offsite are booked; the details are attached.", true),
            From(SampleCast.Liam, work, 640, "Contract renewal", "Thanks for the call. I've sent the draft to our legal team.", true),
            Sample(home, 900, "Bookshop", "orders@bookshop.example", "Your order has shipped", "Your parcel is on its way and should arrive tomorrow.", true),
            From(SampleCast.Dana, work, 1020, "Release notes draft", "Could you look over the notes before Friday?", true),
            From(SampleCast.Mia, home, 1300, "Dinner on Wednesday", "Shall we try the new place by the river?", true),
        ];

        MailAccount[] accounts = twoAccounts ? [work, home] : [work];
        _config.AccountIds = twoAccounts ? [] : [work.Id];

        var source = new MailKitSource(
            new SampleMailSource(accounts, [.. messages.Where(m => accounts.Any(a => a.Id == m.AccountId))], failed ? now.AddMinutes(-20) : now.AddMinutes(-3), failed),
            () => _config);
        UseSource(source);
        _binding.Clock = () => now;
        _binding.Apply(source, _config, Tick, show: true);
    }

    /// <summary>The fixture: fixed accounts and one stored list, fetched a few minutes ago; nothing reaches the shell.</summary>
    private sealed class SampleMailSource(IReadOnlyList<MailAccount> accounts, IReadOnlyList<MailMessage> messages, DateTimeOffset at, bool failed) : IMailSource
    {
        public event Action? AccountsChanged { add { } remove { } }

        public IReadOnlyList<MailAccount> Accounts => accounts;

        public IMailFeed Open(MailQuery query)
            => new Feed(new MailFeedSnapshot(
                messages,
                [.. accounts.Select(a => failed
                    ? new MailAccountOutcome(a.Id, MailAccountState.Failed, FetchStatus.Offline, null, At: messages.Count > 0 ? at : null)
                    : new MailAccountOutcome(a.Id, MailAccountState.Ok, FetchStatus.Ok, null, At: at))],
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
