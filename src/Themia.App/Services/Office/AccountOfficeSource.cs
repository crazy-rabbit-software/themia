using Themia.App.Services.Accounts;
using Themia.App.Services.Sync;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services.Office;

/// <summary>
/// [S-588] [S-2630] [S-2640] Calendars, birthdays and contacts for every widget, from the offline
/// store: one instance per host (<see cref="OfficeSources.Current"/>).
/// <para>
/// <b>Sync owns the network.</b> Each calendar a widget in any view shows ([S-2608]) is held from a
/// year back to a year ahead ([S-2631]): the weeks around today refreshed every five minutes, the
/// whole window every six hours. Each address book a contacts widget reads is held whole up to
/// <see cref="OfficeShelves.MaxContacts"/>, refreshed every fifteen minutes; birthdays daily. Each is an
/// entry of the one sync engine ([S-2691]): its schedule, its back-off, one log line per change of state,
/// and what is in flight cancelled when its account leaves or the source closes. Every change is saved
/// to the account's shelf, read off the UI thread at start ([S-2693]). The cadence rides the shared ticker.
/// </para>
/// <para>
/// <b>Widgets only read.</b> A request inside a calendar's window is answered from the shelf at once,
/// with the time it was synced and how the last sync went (the one vocabulary, <see cref="FetchStatus"/>);
/// only a calendar never synced yet, a range outside the window (paged more than a year away, answered
/// through <see cref="SyncCache{T}"/> in memory) or the user's Refresh reaches the network. <see cref="Changed"/>
/// is raised whenever a sync finished, so widgets redraw from the shelf.
/// </para>
/// <para>
/// UI thread throughout: the ticker and every caller are there, and each await resumes there.
/// </para>
/// </summary>
internal sealed partial class AccountOfficeSource : IOfficeSource, IDisposable
{
    private const string Scope = "office-sync";

    /// <summary>[S-588] Contacts change slowly, and reading them walks the address book.</summary>
    public static readonly TimeSpan ContactsInterval = TimeSpan.FromMinutes(15);

    private readonly AccountService _accounts;
    private readonly SyncHost _sync;
    private readonly SyncEngine<OfficeEntry> _engine;
    private readonly Dictionary<string, CalendarBox> _calendars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContactsBox> _contacts = new(StringComparer.Ordinal);
    private readonly SyncCache<IReadOnlyList<CalendarEvent>> _outside = new(Scope);

    public AccountOfficeSource(AccountService accounts, SyncHost sync, Ticker ticker)
    {
        ArgumentNullException.ThrowIfNull(ticker);
        _accounts = accounts;
        _sync = sync;
        _engine = new SyncEngine<OfficeEntry>(new SyncEntries<OfficeEntry>(Scope), RunAsync, () => DateTimeOffset.UtcNow, ticker.Subscribe,
            entry => !entry.Loading && Usable(entry.AccountId));
        _engine.Changed += entry =>
        {
            // A run that finished brought something new (or a reason there is nothing new): widgets redraw from the shelf.
            if (!entry.Schedule.Busy)
            {
                Raise();
            }
        };
        _accounts.AccountsChanged += OnAccountsChanged;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += OnNetworkAvailable;
        Reconcile();
    }

    public AccountService Service => _accounts;

    /// <summary>Raised on the UI thread when an account came, went or changed health, and when a sync finished.</summary>
    public event Action? Changed;

    public IReadOnlyList<AccountInfo> Accounts(FeatureId feature) => _accounts.WithFeature(feature);

    // -----------------------------------------------------------------------------------------
    // Reading
    // -----------------------------------------------------------------------------------------

    public async Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation)
    {
        if (Calendar(target.AccountId) is not { } box)
        {
            return new Synced<IReadOnlyList<CalendarEvent>>(null, FetchStatus.NotConfigured, null, null);
        }

        await box.Loaded.WaitAsync(cancellation).ConfigureAwait(true);
        var item = box.Item(target.CalendarId);

        // Never synced: the first step of the first run, awaited (the weeks around today; the rest of
        // the window follows and raises Changed). After a failure the schedule holds the next try, so
        // a widget redrawing on every change cannot turn a refusal into a request loop.
        if (force)
        {
            _engine.Entries.Force(item, DateTimeOffset.UtcNow);
            await _engine.RunAsync(item).WaitAsync(cancellation).ConfigureAwait(true);
        }
        else if (item.Span is null && item.IsDue(DateTimeOffset.UtcNow))
        {
            _ = _engine.RunAsync(item);
            await item.FirstStep.WaitAsync(cancellation).ConfigureAwait(true);
        }

        // Inside the synced window the shelf is the answer, whatever it holds: nothing yet (the first
        // sync failed: the status says why) is an answer too, never a reason to ask again here.
        var (windowFrom, windowTo) = CalendarSync.Window(DateOnly.FromDateTime(DateTime.Now), TimeZoneInfo.Local);
        if (item.Span is { } span && CalendarSync.Covers(span, from, to) || from >= windowFrom && to <= windowTo)
        {
            return item.Span is { } held
                ? new Synced<IReadOnlyList<CalendarEvent>>(CalendarSync.Slice(held, from, to), item.Schedule.Status, item.Schedule.Detail, held.NearAt)
                : new Synced<IReadOnlyList<CalendarEvent>>(null, item.Schedule.Status, item.Schedule.Detail, null);
        }

        // Paged past the window: asked for what is on screen, held in memory only.
        var key = $"{target.AccountId}|{target.CalendarId}|{Iso(from)}|{Iso(to)}";
        return await _outside.GetAsync(key, CalendarSync.NearInterval, force, ct => LoadEventsAsync(target, from, to, ct), cancellation).ConfigureAwait(true);
    }

    public async Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation)
    {
        if (Calendar(accountId) is not { } box)
        {
            return new Synced<IReadOnlyList<CalendarInfo>>(null, FetchStatus.NotConfigured, null, null);
        }

        await box.Loaded.WaitAsync(cancellation).ConfigureAwait(true);

        // The selection's page's picker: asked when the list is old, never on a timer.
        await AskAsync(box.CalendarList, force, cancellation).ConfigureAwait(true);
        var list = box.CalendarList;
        return new Synced<IReadOnlyList<CalendarInfo>>(list.Calendars, list.Schedule.Status, list.Schedule.Detail, list.At);
    }

    public async Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation)
    {
        if (Calendar(accountId) is not { } box)
        {
            return new Synced<IReadOnlyList<Birthday>>(null, FetchStatus.NotConfigured, null, null);
        }

        await box.Loaded.WaitAsync(cancellation).ConfigureAwait(true);

        // A widget that reads birthdays wants them kept fresh until the next reconcile says otherwise.
        var birthdays = _engine.Entries.Want(box.Birthdays.Key, () => box.Birthdays);
        _engine.EnsureTicking();

        if (force || birthdays.Birthdays is null)
        {
            await AskAsync(birthdays, force, cancellation).ConfigureAwait(true);
        }

        return new Synced<IReadOnlyList<Birthday>>(birthdays.Birthdays, birthdays.Schedule.Status, birthdays.Schedule.Detail, birthdays.At);
    }

    public async Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation)
    {
        if (Contacts(accountId) is not { } book)
        {
            return new Synced<IReadOnlyList<Contact>>(null, FetchStatus.NotConfigured, null, null);
        }

        await book.Loaded.WaitAsync(cancellation).ConfigureAwait(true);

        if (force || book.People is null)
        {
            await AskAsync(book, force, cancellation).ConfigureAwait(true);
        }

        IReadOnlyList<Contact>? people = book.People is { } all ? [.. all.Take(Math.Max(0, max))] : null;
        return new Synced<IReadOnlyList<Contact>>(people, book.Schedule.Status, book.Schedule.Detail, book.At);
    }

    /// <summary>A widget's own ask: the user's Refresh goes now; otherwise only when the entry is due (a failure's back-off holds).</summary>
    private Task AskAsync(OfficeEntry entry, bool force, CancellationToken cancellation)
    {
        if (force)
        {
            _engine.Entries.Force(entry, DateTimeOffset.UtcNow);
        }
        else if (!entry.IsDue(DateTimeOffset.UtcNow))
        {
            return Task.CompletedTask;
        }

        return _engine.RunAsync(entry).WaitAsync(cancellation);
    }

    // -----------------------------------------------------------------------------------------
    // What to sync, and when
    // -----------------------------------------------------------------------------------------

    private CalendarBox? Calendar(string accountId)
    {
        if (_calendars.TryGetValue(accountId, out var box))
        {
            return box;
        }

        if (_accounts.Find(accountId) is not { } account || !account.Features.Has(HostFeatures.Calendar))
        {
            return null;
        }

        box = new CalendarBox(this, account.Id);
        _calendars[account.Id] = box;
        box.Loaded = box.LoadAsync();
        _engine.EnsureTicking();
        return box;
    }

    private ContactsBox? Contacts(string accountId)
    {
        if (_contacts.TryGetValue(accountId, out var book))
        {
            return book;
        }

        if (_accounts.Find(accountId) is not { } account || !account.Features.Has(HostFeatures.Contacts))
        {
            return null;
        }

        book = _engine.Entries.Hold(ContactsBox.KeyOf(account.Id), () => new ContactsBox(this, account.Id));
        _contacts[account.Id] = book;
        book.Loaded = book.LoadAsync();
        _engine.EnsureTicking();
        return book;
    }

    /// <summary>
    /// Follow the accounts and the widgets of every view: a box for each account something reads. What no widget
    /// reads any more stays on the shelf (a view may come back) but is not synced.
    /// </summary>
    private void Reconcile()
    {
        if (_engine.Disposed)
        {
            return;
        }

        var calendarAccounts = _accounts.WithFeature(HostFeatures.Calendar).Select(a => a.Id).ToList();
        var contactAccounts = _accounts.WithFeature(HostFeatures.Contacts).Select(a => a.Id).ToList();

        foreach (var id in _calendars.Keys.Where(id => !calendarAccounts.Contains(id)).ToList())
        {
            var gone = _calendars[id];
            _calendars.Remove(id);
            _engine.Entries.RemoveWhere(e => e is CalendarItem item && ReferenceEquals(item.Box, gone) || ReferenceEquals(e, gone.Birthdays) || ReferenceEquals(e, gone.CalendarList));
            gone.Cancel();
        }

        foreach (var id in _contacts.Keys.Where(id => !contactAccounts.Contains(id)).ToList())
        {
            var gone = _contacts[id];
            _contacts.Remove(id);
            _engine.Entries.Remove(gone.Key);
            gone.Cancel();
        }

        _engine.Reconcile(() =>
        {
            foreach (var interest in _sync.Interests.Calendar)
            {
                var selected = CalendarRules.SelectAccounts([.. interest.AccountIds], calendarAccounts);
                foreach (var target in CalendarRules.Targets(selected, interest.Calendars))
                {
                    if (Calendar(target.AccountId) is not { } box)
                    {
                        continue;
                    }

                    var item = box.Item(target.CalendarId);
                    _engine.Entries.Want(item.Key, () => item);

                    // [S-3782] The near window follows the shared accounts cadence.
                    item.Schedule.Interval = _sync.Interests.AccountInterval;

                    if (interest.Birthdays)
                    {
                        _engine.Entries.Want(box.Birthdays.Key, () => box.Birthdays);
                    }
                }
            }

            foreach (var interest in _sync.Interests.Contacts)
            {
                foreach (var id in CalendarRules.SelectAccounts([.. interest.AccountIds], contactAccounts))
                {
                    if (Contacts(id) is { } book)
                    {
                        _engine.Entries.Want(book.Key, () => book);
                    }
                }
            }
        }, drop: false);
    }

    private bool Usable(string accountId) => _accounts.Find(accountId) is { Problem: null };

    private void OnNetworkAvailable() => _engine.Wake();

    private void OnAccountsChanged()
    {
        Reconcile();
        Raise();
    }

    private void Raise()
    {
        if (_engine.Disposed || Changed is not { } changed)
        {
            return;
        }

        foreach (var handler in changed.GetInvocationList().Cast<Action>())
        {
            try
            {
                handler();
            }
            catch (Exception ex)
            {
                // One widget's handler must not stop the others hearing it.
                Log.Error(Scope, "a calendar or contacts listener threw", ex);
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // Syncing
    // -----------------------------------------------------------------------------------------

    /// <summary>One run of an entry, by what it is; its account's leaving cancels it too.</summary>
    private async Task<SyncRun> RunAsync(OfficeEntry entry, bool asked, CancellationToken cancellation)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, entry.Cancellation);
        var token = linked.Token;

        switch (entry)
        {
            case CalendarItem item:
                await SyncEventsAsync(item, asked, token).ConfigureAwait(true);
                break;

            case BirthdaysEntry birthdays:
                birthdays.Birthdays = await LoadBirthdaysAsync(birthdays.AccountId, token).ConfigureAwait(true);
                birthdays.At = DateTimeOffset.UtcNow;
                birthdays.Box.Save();
                break;

            case CalendarListEntry list:
                list.Calendars = await LoadCalendarsAsync(list.AccountId, token).ConfigureAwait(true);
                list.At = DateTimeOffset.UtcNow;
                list.Box.Save();
                break;

            case ContactsBox book:
                // [S-2685] The changes since the book's cursor, or the book in full.
                (book.People, book.Cursor) = await SyncBookAsync(book.AccountId, book.People, book.Cursor, token).ConfigureAwait(true);
                book.At = DateTimeOffset.UtcNow;
                book.Save();
                break;
        }

        return SyncRun.Ok;
    }

    /// <summary>
    /// One calendar's run: the whole window when that is due or the user refreshed, else the weeks around today. A
    /// calendar synced for the first time reads the weeks around today first, for a quick first draw
    /// (<see cref="CalendarItem.FirstStep"/>), and the rest of the window straight after. [S-2684] A calendar its
    /// provider reports changes for is one round over the whole window each time (the changes since the cursor, or
    /// the window in full when there is none).
    /// </summary>
    private async Task SyncEventsAsync(CalendarItem item, bool asked, CancellationToken cancellation)
    {
        // Its waiters resume on their own turn, never inside this run.
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        item.FirstStep = first.Task;
        var target = new CalendarTarget(item.AccountId, item.CalendarId);

        try
        {
            if (WindowCursorKind(item.AccountId, item.CalendarId) is { } kind)
            {
                if (item.Span is null)
                {
                    await EventsOnceAsync(item, target, full: false, cancellation).ConfigureAwait(true);
                    first.TrySetResult();
                    Raise();
                }

                item.Span = await SyncWindowAsync(target, item.Span, kind, cancellation).ConfigureAwait(true);

                // A round answers for the whole window: the full refresh's clock moves with it.
                item.Full.Succeeded(DateTimeOffset.UtcNow);
                item.Box.Save();
                return;
            }

            // The user's Refresh asks for the whole window: whatever they are looking at is in it.
            var full = asked || (item.Span is not null && item.Full.IsDue(DateTimeOffset.UtcNow));
            await EventsOnceAsync(item, target, full, cancellation).ConfigureAwait(true);
            first.TrySetResult();

            if (!full && item.Span is not null && item.Full.IsDue(DateTimeOffset.UtcNow))
            {
                Raise();
                await EventsOnceAsync(item, target, full: true, cancellation).ConfigureAwait(true);
            }
        }
        finally
        {
            first.TrySetResult();
        }
    }

    private async Task EventsOnceAsync(CalendarItem item, CalendarTarget target, bool full, CancellationToken cancellation)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var (from, to) = full ? CalendarSync.Window(today, TimeZoneInfo.Local) : CalendarSync.Near(today, TimeZoneInfo.Local);

        var events = await LoadEventsAsync(target, from, to, cancellation).ConfigureAwait(true);
        item.Span = CalendarSync.Merge(item.Span, item.CalendarId, events, from, to, DateTimeOffset.UtcNow, full);

        // A full read is a near one too (the engine moves the near clock); a near read leaves the full one's alone.
        if (full)
        {
            item.Full.Succeeded(DateTimeOffset.UtcNow);
        }

        item.Box.Save();
    }

    public void Dispose()
    {
        if (_engine.Disposed)
        {
            return;
        }

        // [S-2691] Every run in flight hears it; what it brings back after is dropped.
        _engine.Dispose();
        _accounts.AccountsChanged -= OnAccountsChanged;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= OnNetworkAvailable;

        foreach (var box in _calendars.Values)
        {
            box.Cancel();
        }

        foreach (var book in _contacts.Values)
        {
            book.Cancel();
        }

        _calendars.Clear();
        _contacts.Clear();
        Changed = null;
    }

    // -----------------------------------------------------------------------------------------
    // What is held
    // -----------------------------------------------------------------------------------------

    /// <summary>One thing of one account the engine keeps fresh: a calendar, its birthdays, its calendar list, an address book.</summary>
    private abstract class OfficeEntry(string key, TimeSpan interval, string accountId) : SyncEntry(key, interval)
    {
        public string AccountId { get; } = accountId;

        /// <summary>[S-2693] Its account's shelf is still being read: it is not asked before that lands.</summary>
        public abstract bool Loading { get; }

        /// <summary>Tripped when its account leaves or the source closes.</summary>
        public abstract CancellationToken Cancellation { get; }

        public override string Subject => $"{AccountId}: {GetType().Name}";
    }

    /// <summary>One calendar of one account: the weeks around today on the entry's schedule, the whole window on <see cref="Full"/>.</summary>
    private sealed class CalendarItem(CalendarBox box, string? calendarId)
        : OfficeEntry(KeyOf(box.AccountId, calendarId), CalendarSync.NearInterval, box.AccountId)
    {
        public static string KeyOf(string accountId, string? calendarId) => $"calendar|{accountId}|{calendarId}";

        public CalendarBox Box { get; } = box;

        public string? CalendarId { get; } = calendarId;

        public CalendarSpan? Span { get; set; }

        /// <summary>When the whole window was last read: a clock only (its failures are the entry's).</summary>
        public SyncSchedule Full { get; } = new(CalendarSync.FullInterval);

        /// <summary>The first step of the run that is out: what a widget waiting on a first sync awaits.</summary>
        public Task FirstStep { get; set; } = Task.CompletedTask;

        public override bool Loading => Box.Loading;

        public override CancellationToken Cancellation => Box.Cancellation;

        /// <summary>The near window's time, or the whole window's once something is held and the last run worked (a failure's back-off holds both).</summary>
        public override bool IsDue(DateTimeOffset now)
            => Schedule.IsDue(now) || (!Schedule.Busy && Span is not null && Schedule.Status == FetchStatus.Ok && Full.IsDue(now));
    }

    /// <summary>[S-575] An account's contacts' birthdays, held for a day.</summary>
    private sealed class BirthdaysEntry(CalendarBox box)
        : OfficeEntry($"birthdays|{box.AccountId}", CalendarSync.BirthdaysInterval, box.AccountId)
    {
        public CalendarBox Box { get; } = box;

        public IReadOnlyList<Birthday>? Birthdays { get; set; }

        public DateTimeOffset? At { get; set; }

        public override bool Loading => Box.Loading;

        public override CancellationToken Cancellation => Box.Cancellation;
    }

    /// <summary>[S-567] An account's calendar list: asked by the menu when old, never on a timer.</summary>
    private sealed class CalendarListEntry(CalendarBox box)
        : OfficeEntry($"calendars|{box.AccountId}", CalendarSync.CalendarsInterval, box.AccountId)
    {
        public CalendarBox Box { get; } = box;

        public IReadOnlyList<CalendarInfo>? Calendars { get; set; }

        public DateTimeOffset? At { get; set; }

        public override bool Loading => Box.Loading;

        public override CancellationToken Cancellation => Box.Cancellation;
    }

    /// <summary>One calendar account's shelf in memory: its calendars, their windows, its birthdays.</summary>
    private sealed class CalendarBox
    {
        private readonly AccountOfficeSource _owner;
        private readonly Dictionary<string, CalendarItem> _items = new(StringComparer.Ordinal);
        private readonly CancellationTokenSource _cancel;

        public CalendarBox(AccountOfficeSource owner, string accountId)
        {
            _owner = owner;
            AccountId = accountId;
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(owner._engine.Cancellation);
            Birthdays = owner._engine.Entries.Hold($"birthdays|{accountId}", () => new BirthdaysEntry(this));
            CalendarList = owner._engine.Entries.Hold($"calendars|{accountId}", () => new CalendarListEntry(this));
        }

        public string AccountId { get; }

        public BirthdaysEntry Birthdays { get; }

        public CalendarListEntry CalendarList { get; }

        public Task Loaded { get; set; } = Task.CompletedTask;

        public bool Loading { get; private set; } = true;

        public CancellationToken Cancellation => _cancel.Token;

        /// <summary>A calendar of the account, held by the engine (shown or not).</summary>
        public CalendarItem Item(string? calendarId)
        {
            var key = calendarId ?? string.Empty;
            if (!_items.TryGetValue(key, out var item))
            {
                item = new CalendarItem(this, calendarId);
                _items[key] = item;
            }

            return _owner._engine.Entries.Hold(item.Key, () => item);
        }

        /// <summary>[S-2632] [S-2693] The account's calendar shelf, read off the UI thread and validated.</summary>
        public async Task LoadAsync()
        {
            ShelfRead<CalendarShelf> read;
            try
            {
                read = await _owner._sync.Vault.LoadAsync(SyncKind.Calendar, AccountId, SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The read never throws for bad data; anything here is a bug, and the account syncs from nothing.
                Log.Error(Scope, "reading a calendar shelf threw", ex);
                read = new ShelfRead<CalendarShelf>(null, ShelfReadOutcome.Discarded, ex.Message);
            }

            if (_cancel.IsCancellationRequested)
            {
                return;
            }

            if (read.Value is { } stored)
            {
                var dropped = 0;
                var shelf = OfficeShelves.Validate(stored, ref dropped);
                if (dropped > 0)
                {
                    Log.Warn(Scope, $"{AccountId}: {dropped} unusable calendar entries dropped from the store");
                }

                var now = DateTimeOffset.UtcNow;

                if (shelf.Calendars is { } calendars)
                {
                    CalendarList.Calendars = [.. calendars.Select(OfficeShelves.Restore)];
                    CalendarList.At = shelf.CalendarsAt;
                    CalendarList.Schedule.Seed(shelf.CalendarsAt, now);
                }

                foreach (var span in shelf.Spans)
                {
                    var item = Item(span.CalendarId);
                    item.Span = span;
                    item.Schedule.Seed(span.NearAt, now);
                    item.Full.Seed(span.FullAt == default ? null : span.FullAt, now);
                }

                if (shelf.Birthdays is { } birthdays)
                {
                    Birthdays.Birthdays = [.. birthdays.Select(OfficeShelves.Restore)];
                    Birthdays.At = shelf.BirthdaysAt;
                    Birthdays.Schedule.Seed(shelf.BirthdaysAt, now);
                }

                // [S-3823] What is stored, for settings › Accounts.
                _owner._sync.Report(AccountId, SyncStats.Of(shelf));
            }

            Loading = false;
            _owner._engine.Poll();
            _owner.Raise();
        }

        public void Save()
        {
            // An account signed out mid-sync must not be written back after its purge, nor a shelf still being read over.
            if (Loading || _owner._accounts.Find(AccountId) is null)
            {
                return;
            }

            var shelf = new CalendarShelf
            {
                Owner = AccountId,
                Calendars = CalendarList.Calendars is { } list ? [.. list.Take(OfficeShelves.MaxCalendars).Select(OfficeShelves.Store)] : null,
                CalendarsAt = CalendarList.Calendars is null ? null : CalendarList.At,
                Spans = [.. _items.Values.Where(i => i.Span is not null).Select(i => i.Span!)],
                Birthdays = Birthdays.Birthdays is { } held ? [.. held.Take(OfficeShelves.MaxBirthdays).Select(OfficeShelves.Store)] : null,
                BirthdaysAt = Birthdays.Birthdays is null ? null : Birthdays.At,
            };

            _owner._sync.Vault.SaveSoon(SyncKind.Calendar, AccountId, shelf, SyncJsonContext.Default.CalendarShelf);
            _owner._sync.Report(AccountId, SyncStats.Of(shelf));
        }

        public void Cancel()
        {
            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore: already torn down.
            }
        }
    }

    /// <summary>One address book's shelf in memory: an entry of the engine itself.</summary>
    private sealed class ContactsBox : OfficeEntry
    {
        private readonly AccountOfficeSource _owner;
        private readonly CancellationTokenSource _cancel;
        private bool _loading = true;

        public ContactsBox(AccountOfficeSource owner, string accountId)
            : base(KeyOf(accountId), ContactsInterval, accountId)
        {
            _owner = owner;
            _cancel = CancellationTokenSource.CreateLinkedTokenSource(owner._engine.Cancellation);
        }

        public static string KeyOf(string accountId) => $"contacts|{accountId}";

        public IReadOnlyList<Contact>? People { get; set; }

        /// <summary>[S-2680] Where the provider's changes continue from; null when none can follow.</summary>
        public SyncCursor? Cursor { get; set; }

        public DateTimeOffset? At { get; set; }

        public Task Loaded { get; set; } = Task.CompletedTask;

        public override bool Loading => _loading;

        public override CancellationToken Cancellation => _cancel.Token;

        /// <summary>[S-2641] [S-2693] The book's shelf, read off the UI thread and validated.</summary>
        public async Task LoadAsync()
        {
            ShelfRead<ContactsShelf> read;
            try
            {
                read = await _owner._sync.Vault.LoadAsync(SyncKind.Contacts, AccountId, SyncJsonContext.Default.ContactsShelf, ContactsShelf.Version).ConfigureAwait(true);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The read never throws for bad data; anything here is a bug, and the book syncs from nothing.
                Log.Error(Scope, "reading a contacts shelf threw", ex);
                read = new ShelfRead<ContactsShelf>(null, ShelfReadOutcome.Discarded, ex.Message);
            }

            if (_cancel.IsCancellationRequested)
            {
                return;
            }

            if (read.Value is { } stored)
            {
                var dropped = 0;
                var shelf = OfficeShelves.Validate(stored, ref dropped);
                if (dropped > 0)
                {
                    Log.Warn(Scope, $"{AccountId}: {dropped} unusable contacts dropped from the store");
                }

                if (shelf.At is { } at)
                {
                    People = [.. shelf.Contacts.Select(c => OfficeShelves.Restore(c, AccountId))];
                    Cursor = shelf.Cursor;
                    At = at;
                    Schedule.Seed(at, DateTimeOffset.UtcNow);
                    _owner._sync.Report(AccountId, SyncStats.Of(shelf));
                }
            }

            _loading = false;
            _owner._engine.Poll();
            _owner.Raise();
        }

        public void Save()
        {
            if (_loading || _owner._accounts.Find(AccountId) is null || People is null)
            {
                return;
            }

            var shelf = new ContactsShelf
            {
                Owner = AccountId,
                At = At,
                Contacts = [.. People.Take(OfficeShelves.MaxContacts).Select(OfficeShelves.Store)],
                Cursor = Cursor,
            };

            _owner._sync.Vault.SaveSoon(SyncKind.Contacts, AccountId, shelf, SyncJsonContext.Default.ContactsShelf);
            _owner._sync.Report(AccountId, SyncStats.Of(shelf));
        }

        public void Cancel()
        {
            try
            {
                _cancel.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore: already torn down.
            }
        }
    }
}
