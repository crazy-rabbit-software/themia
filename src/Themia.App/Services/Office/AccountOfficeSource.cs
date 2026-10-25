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
/// <see cref="OfficeShelves.MaxContacts"/>, refreshed every fifteen minutes; birthdays daily. Every
/// change is saved to the account's shelf. The cadence rides the shared ticker.
/// </para>
/// <para>
/// <b>Widgets only read.</b> A request inside a calendar's window is answered from the shelf at once,
/// with the time it was synced and whatever the last sync met; only a calendar never synced yet, a
/// range outside the window (paged more than a year away, answered through
/// <see cref="SyncCache{T}"/> in memory) or the user's Refresh reaches the network. <see cref="Changed"/>
/// is raised whenever a sync brought something new, so widgets redraw from the shelf.
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
    private readonly Ticker _ticker;
    private readonly Dictionary<string, CalendarBox> _calendars = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ContactsBox> _contacts = new(StringComparer.Ordinal);
    private readonly SyncCache<IReadOnlyList<CalendarEvent>> _outside = new(Scope);
    private IDisposable? _tick;
    private bool _disposed;

    public AccountOfficeSource(AccountService accounts, SyncHost sync, Ticker ticker)
    {
        _accounts = accounts;
        _sync = sync;
        _ticker = ticker;
        _accounts.AccountsChanged += OnAccountsChanged;
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += OnNetworkAvailable;
        Reconcile();
    }

    public AccountService Service => _accounts;

    /// <summary>Raised on the UI thread when an account came, went or changed health, and when a sync brought new data.</summary>
    public event Action? Changed;

    public IReadOnlyList<AccountInfo> Accounts(FeatureId feature) => _accounts.WithFeature(feature);

    // -----------------------------------------------------------------------------------------
    // Reading
    // -----------------------------------------------------------------------------------------

    public async Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation)
    {
        var box = Calendar(target.AccountId);
        if (box is null)
        {
            return new Synced<IReadOnlyList<CalendarEvent>>(null, SyncProblem.NoAccount, null, null);
        }

        var item = box.Item(target.CalendarId);

        // Never synced: the first step of the first sync, awaited (the weeks around today; the rest of
        // the window follows and raises Changed). After a failure the schedule holds the next try, so
        // a widget redrawing on every change cannot turn a refusal into a request loop.
        if (force)
        {
            await SyncEventsAsync(box, item, force: true).WaitAsync(cancellation).ConfigureAwait(true);
        }
        else if (item.Span is null && item.Near.IsDue(DateTimeOffset.UtcNow))
        {
            _ = SyncEventsAsync(box, item, force: false);
            await (item.FirstStep ?? Task.CompletedTask).WaitAsync(cancellation).ConfigureAwait(true);
        }

        // Inside the synced window the shelf is the answer, whatever it holds: nothing yet (the first
        // sync failed: the problem says why) is an answer too, never a reason to ask again here.
        var (windowFrom, windowTo) = CalendarSync.Window(DateOnly.FromDateTime(DateTime.Now), TimeZoneInfo.Local);
        if (item.Span is { } span && CalendarSync.Covers(span, from, to) || from >= windowFrom && to <= windowTo)
        {
            return item.Span is { } held
                ? new Synced<IReadOnlyList<CalendarEvent>>(CalendarSync.Slice(held, from, to), item.Problem, item.Full.Detail ?? item.Near.Detail, held.NearAt)
                : new Synced<IReadOnlyList<CalendarEvent>>(null, item.Problem, item.Full.Detail, null);
        }

        // Paged past the window: asked for what is on screen, held in memory only.
        var key = $"{target.AccountId}|{target.CalendarId}|{Iso(from)}|{Iso(to)}";
        return await _outside.GetAsync(key, CalendarSync.NearInterval, force, ct => LoadEventsAsync(target, from, to, ct), cancellation).ConfigureAwait(true);
    }

    public async Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation)
    {
        if (Calendar(accountId) is not { } box)
        {
            return new Synced<IReadOnlyList<CalendarInfo>>(null, SyncProblem.NoAccount, null, null);
        }

        // The settings page's picker: asked when the list is old, never on a timer.
        if (force || box.CalendarsSchedule.IsDue(DateTimeOffset.UtcNow))
        {
            await RunAsync(box.CalendarsSchedule, () => box.CalendarsProblem, p => box.CalendarsProblem = p, async () =>
            {
                box.CalendarList = await LoadCalendarsAsync(accountId, CancellationToken.None).ConfigureAwait(true);
                box.CalendarsAt = DateTimeOffset.UtcNow;
            }, box.Save, force).WaitAsync(cancellation).ConfigureAwait(true);
        }

        return new Synced<IReadOnlyList<CalendarInfo>>(box.CalendarList, box.CalendarsProblem, box.CalendarsSchedule.Detail, box.CalendarsAt);
    }

    public async Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation)
    {
        if (Calendar(accountId) is not { } box)
        {
            return new Synced<IReadOnlyList<Birthday>>(null, SyncProblem.NoAccount, null, null);
        }

        box.WantsBirthdays = true;

        if (force || (box.Birthdays is null && box.BirthdaysSchedule.IsDue(DateTimeOffset.UtcNow)))
        {
            await SyncBirthdaysAsync(box, force).WaitAsync(cancellation).ConfigureAwait(true);
        }

        return new Synced<IReadOnlyList<Birthday>>(box.Birthdays, box.BirthdaysProblem, box.BirthdaysSchedule.Detail, box.BirthdaysAt);
    }

    public async Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation)
    {
        if (Contacts(accountId) is not { } box)
        {
            return new Synced<IReadOnlyList<Contact>>(null, SyncProblem.NoAccount, null, null);
        }

        if (force || (box.People is null && box.Schedule.IsDue(DateTimeOffset.UtcNow)))
        {
            await SyncContactsAsync(box, force).WaitAsync(cancellation).ConfigureAwait(true);
        }

        IReadOnlyList<Contact>? people = box.People is { } all ? [.. all.Take(Math.Max(0, max))] : null;
        return new Synced<IReadOnlyList<Contact>>(people, box.Problem, box.Schedule.Detail, box.At);
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

        box = CalendarBox.Load(this, account.Id);
        _calendars[account.Id] = box;
        EnsureTicking();
        return box;
    }

    private ContactsBox? Contacts(string accountId)
    {
        if (_contacts.TryGetValue(accountId, out var box))
        {
            return box;
        }

        if (_accounts.Find(accountId) is not { } account || !account.Features.Has(HostFeatures.Contacts))
        {
            return null;
        }

        box = ContactsBox.Load(this, account.Id);
        _contacts[account.Id] = box;
        EnsureTicking();
        return box;
    }

    /// <summary>Follow the accounts and the widgets of every view: a box for each account something reads.</summary>
    private void Reconcile()
    {
        if (_disposed)
        {
            return;
        }

        var calendarAccounts = _accounts.WithFeature(HostFeatures.Calendar).Select(a => a.Id).ToList();
        var contactAccounts = _accounts.WithFeature(HostFeatures.Contacts).Select(a => a.Id).ToList();

        foreach (var id in _calendars.Keys.Where(id => !calendarAccounts.Contains(id)).ToList())
        {
            _calendars.Remove(id);
        }

        foreach (var id in _contacts.Keys.Where(id => !contactAccounts.Contains(id)).ToList())
        {
            _contacts.Remove(id);
        }

        // What no widget reads any more stays on the shelf (a view may come back) but is not synced.
        foreach (var box in _calendars.Values)
        {
            box.WantsBirthdays = false;
            foreach (var item in box.Items)
            {
                item.Wanted = false;
            }
        }

        foreach (var box in _contacts.Values)
        {
            box.Wanted = false;
        }

        foreach (var interest in _sync.Interests.Calendar)
        {
            var selected = CalendarRules.SelectAccounts([.. interest.AccountIds], calendarAccounts);
            foreach (var target in CalendarRules.Targets(selected, interest.Calendars))
            {
                if (Calendar(target.AccountId) is { } box)
                {
                    // [S-3782] The near window follows the shared accounts cadence.
                    box.Item(target.CalendarId).Near.Interval = _sync.Interests.AccountInterval;
                    box.Item(target.CalendarId).Wanted = true;
                    box.WantsBirthdays |= interest.Birthdays;
                }
            }
        }

        foreach (var interest in _sync.Interests.Contacts)
        {
            foreach (var id in CalendarRules.SelectAccounts([.. interest.AccountIds], contactAccounts))
            {
                if (Contacts(id) is { } box)
                {
                    box.Wanted = true;
                }
            }
        }

        EnsureTicking();
        OnTick();
    }

    private void EnsureTicking()
    {
        if (_disposed)
        {
            return;
        }

        if (_calendars.Count + _contacts.Count > 0)
        {
            // The ticker fires once on subscribing; that first pass is the start-up sync.
            _tick ??= _ticker.Subscribe(TimeSpan.FromMinutes(1), OnTick);
        }
        else
        {
            _tick?.Dispose();
            _tick = null;
        }
    }

    /// <summary>[S-2609] Everything due: what the widgets want, on its own cadence.</summary>
    private void OnTick()
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var box in _calendars.Values.ToArray())
        {
            if (!Usable(box.AccountId))
            {
                continue;
            }

            foreach (var item in box.Items.Where(i => i.Wanted).ToArray())
            {
                // Decides itself which window is due, if any.
                _ = SyncEventsAsync(box, item, force: false);
            }

            if (box.WantsBirthdays && box.BirthdaysSchedule.IsDue(now))
            {
                _ = SyncBirthdaysAsync(box, force: false);
            }
        }

        foreach (var box in _contacts.Values.ToArray())
        {
            if (box.Wanted && Usable(box.AccountId) && box.Schedule.IsDue(now))
            {
                _ = SyncContactsAsync(box, force: false);
            }
        }
    }

    private bool Usable(string accountId) => _accounts.Find(accountId) is { Problem: null };

    private void OnNetworkAvailable()
    {
        foreach (var box in _calendars.Values)
        {
            foreach (var item in box.Items)
            {
                item.Near.Wake();
                item.Full.Wake();
            }

            box.BirthdaysSchedule.Wake();
            box.CalendarsSchedule.Wake();
        }

        foreach (var box in _contacts.Values)
        {
            box.Schedule.Wake();
        }

        OnTick();
    }

    private void OnAccountsChanged()
    {
        Reconcile();
        Raise();
    }

    private void Raise()
    {
        if (_disposed || Changed is not { } changed)
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

    /// <summary>
    /// One calendar, when due (or asked): the whole window when that is due or the user refreshed,
    /// else the weeks around today. A calendar synced for the first time reads the weeks around today
    /// first, for a quick first draw (<see cref="CalendarItem.FirstStep"/>), and the rest of the window
    /// straight after. Single-flight per calendar.
    /// </summary>
    private Task SyncEventsAsync(CalendarBox box, CalendarItem item, bool force)
    {
        if (item.Flight is { IsCompleted: false } flight)
        {
            return flight;
        }

        var now = DateTimeOffset.UtcNow;

        // [S-2684] A calendar its provider reports changes for: every refresh is one round over the
        // whole window (the changes since the cursor, or the window in full when there is none).
        // A calendar never synced draws the weeks around today first, as below.
        if (WindowCursorKind(box.AccountId, item.CalendarId) is { } kind)
        {
            if (!force && !item.Near.IsDue(now))
            {
                return Task.CompletedTask;
            }

            var neverSynced = item.Span is null;
            var quick = neverSynced ? EventsOnceAsync(box, item, full: false, force) : Task.CompletedTask;
            item.Flight = ThenWindowRoundAsync(quick, neverSynced, box, item, kind, force);
            item.FirstStep = neverSynced ? quick : item.Flight;
            return item.Flight;
        }

        // The user's Refresh asks for the whole window: whatever they are looking at is in it.
        var full = force || (item.Span is not null && item.Full.IsDue(now));
        if (!force && !(full ? item.Full : item.Near).IsDue(now))
        {
            return Task.CompletedTask;
        }

        var first = EventsOnceAsync(box, item, full, force);
        item.FirstStep = first;
        item.Flight = ThenWholeWindowAsync(first, box, item, full);
        return item.Flight;
    }

    private async Task ThenWholeWindowAsync(Task first, CalendarBox box, CalendarItem item, bool wasFull)
    {
        await first.ConfigureAwait(true);

        if (!wasFull && !_disposed && item.Span is not null && item.Full.IsDue(DateTimeOffset.UtcNow))
        {
            await EventsOnceAsync(box, item, full: true, force: false).ConfigureAwait(true);
        }
    }

    private async Task ThenWindowRoundAsync(Task quick, bool ranQuick, CalendarBox box, CalendarItem item, CursorKind kind, bool force)
    {
        await quick.ConfigureAwait(true);

        // A first draw that failed has said why and set its back-off; the round waits for that too.
        if (_disposed || (ranQuick && item.Problem != SyncProblem.None))
        {
            return;
        }

        var target = new CalendarTarget(box.AccountId, item.CalendarId);
        await RunAsync(item.Near, () => item.Problem, p => item.Problem = p, async () =>
        {
            item.Span = await SyncWindowAsync(target, item.Span, kind, CancellationToken.None).ConfigureAwait(true);

            // A round answers for the whole window: the full refresh's clock moves with it.
            item.Full.Succeeded(DateTimeOffset.UtcNow);
        }, box.Save, force).ConfigureAwait(true);
    }

    private Task EventsOnceAsync(CalendarBox box, CalendarItem item, bool full, bool force)
    {
        var schedule = full ? item.Full : item.Near;
        var today = DateOnly.FromDateTime(DateTime.Now);
        var (from, to) = full ? CalendarSync.Window(today, TimeZoneInfo.Local) : CalendarSync.Near(today, TimeZoneInfo.Local);
        var target = new CalendarTarget(box.AccountId, item.CalendarId);

        return RunAsync(schedule, () => item.Problem, p => item.Problem = p, async () =>
        {
            var events = await LoadEventsAsync(target, from, to, CancellationToken.None).ConfigureAwait(true);
            item.Span = CalendarSync.Merge(item.Span, item.CalendarId, events, from, to, DateTimeOffset.UtcNow, full);

            // A full sync is a near sync too; a near sync leaves the full one's clock alone.
            if (full)
            {
                item.Near.Succeeded(DateTimeOffset.UtcNow);
            }
        }, box.Save, force);
    }

    private Task SyncBirthdaysAsync(CalendarBox box, bool force)
    {
        if (box.BirthdaysFlight is { IsCompleted: false } flight)
        {
            return flight;
        }

        box.BirthdaysFlight = RunAsync(box.BirthdaysSchedule, () => box.BirthdaysProblem, p => box.BirthdaysProblem = p, async () =>
        {
            box.Birthdays = await LoadBirthdaysAsync(box.AccountId, CancellationToken.None).ConfigureAwait(true);
            box.BirthdaysAt = DateTimeOffset.UtcNow;
        }, box.Save, force);

        return box.BirthdaysFlight;
    }

    private Task SyncContactsAsync(ContactsBox box, bool force)
    {
        if (box.Flight is { IsCompleted: false } flight)
        {
            return flight;
        }

        box.Flight = RunAsync(box.Schedule, () => box.Problem, p => box.Problem = p, async () =>
        {
            // [S-2685] The changes since the book's cursor, or the book in full.
            (box.People, box.Cursor) = await SyncBookAsync(box.AccountId, box.People, box.Cursor, CancellationToken.None).ConfigureAwait(true);
            box.At = DateTimeOffset.UtcNow;
        }, box.Save, force);

        return box.Flight;
    }

    /// <summary>
    /// One sync step with its bookkeeping: single-flight through the schedule, the outcome recorded
    /// and logged on a change, a refusal held ([S-588]), the shelf saved and the widgets told when
    /// something new came. Never throws.
    /// </summary>
    private async Task RunAsync(SyncSchedule schedule, Func<SyncProblem> problem, Action<SyncProblem> setProblem, Func<Task> load, Action save, bool force)
    {
        if (schedule.Busy)
        {
            return;
        }

        if (force)
        {
            schedule.Force(DateTimeOffset.UtcNow);
        }

        schedule.Busy = true;
        var before = problem();

        try
        {
            await load().ConfigureAwait(true);
            schedule.Succeeded(DateTimeOffset.UtcNow);
            setProblem(SyncProblem.None);
            save();
        }
        catch (AccountException ex)
        {
            var next = SyncProblems.From(ex.Error);
            var now = DateTimeOffset.UtcNow;
            schedule.Failed(ex, now);
            schedule.Hold(SyncProblems.HoldFor(next), now);
            setProblem(next);

            if (next != before)
            {
                Log.Warn(Scope, $"{next} ({ex.Message})");
            }
        }
        catch (Exception ex)
        {
            // Anything else — a body that did not parse, a bug in a mapper — must not fault a shared
            // flight several widgets await; it is logged in full and shown as "something unexpected".
            Log.Error(Scope, "a calendar or contacts sync failed", ex);
            schedule.Failed(FetchStatus.BadResponse, ex.Message, DateTimeOffset.UtcNow);
            setProblem(SyncProblem.BadResponse);
        }
        finally
        {
            schedule.Busy = false;
        }

        Raise();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _accounts.AccountsChanged -= OnAccountsChanged;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= OnNetworkAvailable;
        _tick?.Dispose();
        _tick = null;
        _calendars.Clear();
        _contacts.Clear();
        Changed = null;
    }

    // -----------------------------------------------------------------------------------------
    // What is held
    // -----------------------------------------------------------------------------------------

    /// <summary>One calendar of one account.</summary>
    private sealed class CalendarItem(string? calendarId)
    {
        public string? CalendarId { get; } = calendarId;

        public CalendarSpan? Span { get; set; }

        public SyncSchedule Near { get; } = new(CalendarSync.NearInterval);

        public SyncSchedule Full { get; } = new(CalendarSync.FullInterval);

        public SyncProblem Problem { get; set; }

        /// <summary>Shown by a widget in some view; only these are synced on the timer.</summary>
        public bool Wanted { get; set; }

        public Task? Flight { get; set; }

        /// <summary>The first step of the flight: what a widget waiting on a first sync awaits.</summary>
        public Task? FirstStep { get; set; }
    }

    /// <summary>One calendar account's shelf in memory.</summary>
    private sealed class CalendarBox(AccountOfficeSource owner, string accountId)
    {
        private readonly Dictionary<string, CalendarItem> _items = new(StringComparer.Ordinal);

        public string AccountId { get; } = accountId;

        public IEnumerable<CalendarItem> Items => _items.Values;

        public IReadOnlyList<CalendarInfo>? CalendarList { get; set; }

        public DateTimeOffset? CalendarsAt { get; set; }

        public SyncSchedule CalendarsSchedule { get; } = new(CalendarSync.CalendarsInterval);

        public SyncProblem CalendarsProblem { get; set; }

        public IReadOnlyList<Birthday>? Birthdays { get; set; }

        public DateTimeOffset? BirthdaysAt { get; set; }

        public SyncSchedule BirthdaysSchedule { get; } = new(CalendarSync.BirthdaysInterval);

        public SyncProblem BirthdaysProblem { get; set; }

        public bool WantsBirthdays { get; set; }

        public Task? BirthdaysFlight { get; set; }

        public CalendarItem Item(string? calendarId)
        {
            var key = calendarId ?? string.Empty;
            if (!_items.TryGetValue(key, out var item))
            {
                item = new CalendarItem(calendarId);
                _items[key] = item;
            }

            return item;
        }

        public static CalendarBox Load(AccountOfficeSource owner, string accountId)
        {
            var box = new CalendarBox(owner, accountId);
            var read = owner._sync.Vault.Load(SyncKind.Calendar, accountId, SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version);
            if (read.Value is not { } stored)
            {
                return box;
            }

            var dropped = 0;
            var shelf = OfficeShelves.Validate(stored, ref dropped);
            if (dropped > 0)
            {
                Log.Warn(Scope, $"{accountId}: {dropped} unusable calendar entries dropped from the store");
            }

            var now = DateTimeOffset.UtcNow;

            if (shelf.Calendars is { } calendars)
            {
                box.CalendarList = [.. calendars.Select(OfficeShelves.Restore)];
                box.CalendarsAt = shelf.CalendarsAt;
                box.CalendarsSchedule.Seed(shelf.CalendarsAt, now);
            }

            foreach (var span in shelf.Spans)
            {
                var item = box.Item(span.CalendarId);
                item.Span = span;
                item.Near.Seed(span.NearAt, now);
                item.Full.Seed(span.FullAt == default ? null : span.FullAt, now);
            }

            if (shelf.Birthdays is { } birthdays)
            {
                box.Birthdays = [.. birthdays.Select(OfficeShelves.Restore)];
                box.BirthdaysAt = shelf.BirthdaysAt;
                box.BirthdaysSchedule.Seed(shelf.BirthdaysAt, now);
            }

            // [S-3823] What is stored, for ⚙ › Accounts.
            owner._sync.Report(accountId, SyncStats.Of(shelf));

            return box;
        }

        public void Save()
        {
            // An account signed out mid-sync must not be written back after its purge.
            if (owner._accounts.Find(AccountId) is null)
            {
                return;
            }

            var shelf = new CalendarShelf
            {
                Owner = AccountId,
                Calendars = CalendarList is { } list ? [.. list.Take(OfficeShelves.MaxCalendars).Select(OfficeShelves.Store)] : null,
                CalendarsAt = CalendarList is null ? null : CalendarsAt,
                Spans = [.. _items.Values.Where(i => i.Span is not null).Select(i => i.Span!)],
                Birthdays = Birthdays is { } held ? [.. held.Take(OfficeShelves.MaxBirthdays).Select(OfficeShelves.Store)] : null,
                BirthdaysAt = Birthdays is null ? null : BirthdaysAt,
            };

            owner._sync.Vault.SaveSoon(SyncKind.Calendar, AccountId, shelf, SyncJsonContext.Default.CalendarShelf);
            owner._sync.Report(AccountId, SyncStats.Of(shelf));
        }
    }

    /// <summary>One address book's shelf in memory.</summary>
    private sealed class ContactsBox(AccountOfficeSource owner, string accountId)
    {
        public string AccountId { get; } = accountId;

        public IReadOnlyList<Contact>? People { get; set; }

        /// <summary>[S-2680] Where the provider's changes continue from; null when none can follow.</summary>
        public SyncCursor? Cursor { get; set; }

        public DateTimeOffset? At { get; set; }

        public SyncSchedule Schedule { get; } = new(ContactsInterval);

        public SyncProblem Problem { get; set; }

        public bool Wanted { get; set; }

        public Task? Flight { get; set; }

        public static ContactsBox Load(AccountOfficeSource owner, string accountId)
        {
            var box = new ContactsBox(owner, accountId);
            var read = owner._sync.Vault.Load(SyncKind.Contacts, accountId, SyncJsonContext.Default.ContactsShelf, ContactsShelf.Version);
            if (read.Value is not { } stored)
            {
                return box;
            }

            var dropped = 0;
            var shelf = OfficeShelves.Validate(stored, ref dropped);
            if (dropped > 0)
            {
                Log.Warn(Scope, $"{accountId}: {dropped} unusable contacts dropped from the store");
            }

            if (shelf.At is { } at)
            {
                box.People = [.. shelf.Contacts.Select(c => OfficeShelves.Restore(c, accountId))];
                box.Cursor = shelf.Cursor;
                box.At = at;
                box.Schedule.Seed(at, DateTimeOffset.UtcNow);
                owner._sync.Report(accountId, SyncStats.Of(shelf));
            }

            return box;
        }

        public void Save()
        {
            if (owner._accounts.Find(AccountId) is null || People is null)
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

            owner._sync.Vault.SaveSoon(SyncKind.Contacts, AccountId, shelf, SyncJsonContext.Default.ContactsShelf);
            owner._sync.Report(AccountId, SyncStats.Of(shelf));
        }
    }
}
