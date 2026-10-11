using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2600]..[S-2662] The offline sync store: sealing, shelves, the write-back queue, schedules and merges.</summary>
public sealed class OfflineSyncTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 10, 42, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-sync-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static byte[] Key(byte fill = 7) => Enumerable.Repeat(fill, SealedBlob.KeySize).ToArray();

    private static MailShelf Mail(string owner, params string[] ids) => new()
    {
        Owner = owner,
        Folders =
        [
            new MailFolderShelf
            {
                Folder = MailFolder.Inbox,
                At = Now,
                Count = ids.Length,
                Messages = [.. ids.Select(id => new StoredMail { RawId = id, Subject = "Subject " + id })],
            },
        ],
    };

    // ---- [S-2602] sealing ------------------------------------------------------------------------

    [Fact]
    public void S2602_a_sealed_blob_opens_with_its_key_and_context()
    {
        var plain = "{\"hello\":1}"u8.ToArray();
        var blob = SealedBlob.Seal(Key(), plain, "mail|a");

        Assert.Equal(SealOutcome.Opened, SealedBlob.TryOpen(Key(), blob, "mail|a", out var back));
        Assert.Equal(plain, back);
        Assert.False(blob.AsSpan().IndexOf("hello"u8) >= 0);
    }

    [Fact]
    public void S2602_a_flipped_byte_another_key_or_another_shelf_does_not_open()
    {
        var blob = SealedBlob.Seal(Key(), "secret"u8, "mail|a");

        var flipped = blob.ToArray();
        flipped[^1] ^= 0x01;

        Assert.Equal(SealOutcome.Tampered, SealedBlob.TryOpen(Key(), flipped, "mail|a", out _));
        Assert.Equal(SealOutcome.Tampered, SealedBlob.TryOpen(Key(9), blob, "mail|a", out _));
        Assert.Equal(SealOutcome.Tampered, SealedBlob.TryOpen(Key(), blob, "mail|b", out _));
    }

    [Fact]
    public void S2602_something_that_is_not_a_sealed_blob_says_so()
    {
        Assert.Equal(SealOutcome.NotSealed, SealedBlob.TryOpen(Key(), "{}"u8, "x", out _));
        Assert.Equal(SealOutcome.NotSealed, SealedBlob.TryOpen(Key(), new byte[64], "x", out _));
    }

    [Fact]
    public void S2602_two_seals_of_the_same_bytes_differ()
        => Assert.NotEqual(SealedBlob.Seal(Key(), "same"u8, "c"), SealedBlob.Seal(Key(), "same"u8, "c"));

    // ---- [S-2604] shelf names ----------------------------------------------------------------------

    [Fact]
    public void S2604_a_shelf_name_is_stable_per_kind_and_owner_and_never_names_the_owner()
    {
        var name = SyncVault.ShelfName(SyncKind.Mail, "imap:dana@example.com");

        Assert.Equal(name, SyncVault.ShelfName(SyncKind.Mail, "imap:dana@example.com"));
        Assert.NotEqual(name, SyncVault.ShelfName(SyncKind.Calendar, "imap:dana@example.com"));
        Assert.NotEqual(name, SyncVault.ShelfName(SyncKind.Mail, "imap:other@example.com"));
        Assert.DoesNotContain("dana", name, StringComparison.Ordinal);
        Assert.StartsWith("mail-", name, StringComparison.Ordinal);
    }

    // ---- [S-2605] [S-2606] the vault -------------------------------------------------------------

    [Fact]
    public void S2605_an_owner_never_synced_is_absent()
    {
        var vault = new SyncVault(_dir, Key());
        Assert.Equal(ShelfReadOutcome.Absent, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2606_a_saved_shelf_reads_back_and_leaves_no_temp_file()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "1", "2"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();

        var read = vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version);

        Assert.Equal(ShelfReadOutcome.Loaded, read.Outcome);
        Assert.Equal(["1", "2"], read.Value!.Folders[0].Messages.Select(m => m.RawId));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
        Assert.DoesNotContain("Subject", File.ReadAllText(vault.ShelfPath(SyncKind.Mail, "a")!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task S2606_saves_queued_together_keep_the_newest()
    {
        var vault = new SyncVault(_dir, Key());
        for (var i = 0; i < 20; i++)
        {
            vault.SaveSoon(SyncKind.Mail, "a", Mail("a", i.ToString(System.Globalization.CultureInfo.InvariantCulture)), SyncJsonContext.Default.MailShelf);
        }

        await vault.FlushAsync();

        var read = vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version);
        Assert.Equal("19", Assert.Single(read.Value!.Folders[0].Messages).RawId);
    }

    [Fact]
    public async Task S2605_a_damaged_shelf_is_set_aside_and_the_owner_starts_empty()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "1"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();
        var path = vault.ShelfPath(SyncKind.Mail, "a")!;
        File.WriteAllText(path, "not a shelf");

        var read = vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version);

        Assert.Equal(ShelfReadOutcome.Discarded, read.Outcome);
        Assert.Null(read.Value);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(path + ".corrupt"));
    }

    [Fact]
    public async Task S2605_a_shelf_sealed_with_another_key_is_discarded()
    {
        var first = new SyncVault(_dir, Key(1));
        first.SaveSoon(SyncKind.Mail, "a", Mail("a", "1"), SyncJsonContext.Default.MailShelf);
        await first.FlushAsync();

        var read = new SyncVault(_dir, Key(2)).Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version);

        Assert.Equal(ShelfReadOutcome.Discarded, read.Outcome);
    }

    [Fact]
    public async Task S2605_another_accounts_shelf_copied_over_this_one_does_not_open()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "b", Mail("b", "theirs"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();
        File.Copy(vault.ShelfPath(SyncKind.Mail, "b")!, vault.ShelfPath(SyncKind.Mail, "a")!);

        Assert.Equal(ShelfReadOutcome.Discarded, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2605_an_older_schema_is_a_stale_cache_resynced_not_set_aside_as_corrupt()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Calendar, "a", new CalendarShelf { Owner = "a", SchemaVersion = CalendarShelf.Version - 1 }, SyncJsonContext.Default.CalendarShelf);
        await vault.FlushAsync();
        var path = vault.ShelfPath(SyncKind.Calendar, "a")!;

        var read = vault.Load(SyncKind.Calendar, "a", SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version);

        Assert.Equal(ShelfReadOutcome.Stale, read.Outcome);
        Assert.Null(read.Value);
        Assert.True(File.Exists(path));
        Assert.False(File.Exists(path + ".corrupt"));

        // The resync's first save replaces it.
        vault.SaveSoon(SyncKind.Calendar, "a", new CalendarShelf { Owner = "a" }, SyncJsonContext.Default.CalendarShelf);
        await vault.FlushAsync();
        Assert.Equal(ShelfReadOutcome.Loaded, vault.Load(SyncKind.Calendar, "a", SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2632_a_calendar_shelf_from_before_the_event_fixes_is_dropped_whole_and_resynced()
    {
        // Version 1 shelves may hold Graph delta notices stored as untitled events and all-day
        // events stretched over two days: the bump makes the first sync of this build start over,
        // events and change cursors alike, while the account's other shelves stay.
        Assert.True(CalendarShelf.Version >= 2);

        var vault = new SyncVault(_dir, Key());
        var old = new CalendarShelf
        {
            Owner = "a",
            SchemaVersion = 1,
            Spans =
            [
                new CalendarSpan
                {
                    From = Now.AddDays(-1),
                    To = Now.AddDays(1),
                    NearAt = Now,
                    FullAt = Now,
                    Cursor = new SyncCursor { Kind = CursorKind.GraphDelta, Value = "https://graph.example/delta?token=x" },
                    Events = [new StoredEvent { Id = "e1", Subject = string.Empty, Start = Now, End = Now.AddDays(2), AllDay = true }],
                },
            ],
        };
        vault.SaveSoon(SyncKind.Calendar, "a", old, SyncJsonContext.Default.CalendarShelf);
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "1"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();

        var calendar = vault.Load(SyncKind.Calendar, "a", SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version);

        Assert.Equal(ShelfReadOutcome.Stale, calendar.Outcome);
        Assert.Null(calendar.Value);
        Assert.Equal(ShelfReadOutcome.Loaded, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
        Assert.Equal(ShelfReadOutcome.Absent, vault.Load(SyncKind.Contacts, "a", SyncJsonContext.Default.ContactsShelf, ContactsShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2605_another_schema_or_owner_inside_is_discarded()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "a", new MailShelf { Owner = "a", SchemaVersion = 99 }, SyncJsonContext.Default.MailShelf);
        vault.SaveSoon(SyncKind.Mail, "b", new MailShelf { Owner = "someone-else" }, SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();

        Assert.Equal(ShelfReadOutcome.Discarded, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
        Assert.Equal(ShelfReadOutcome.Discarded, vault.Load(SyncKind.Mail, "b", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
    }

    [Fact]
    public async Task S2601_a_detached_vault_reads_and_writes_nothing()
    {
        var vault = SyncVault.Detached();
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "1"), SyncJsonContext.Default.MailShelf);
        vault.Purge("a");
        await vault.FlushAsync();

        Assert.False(vault.Persists);
        Assert.Null(vault.ShelfPath(SyncKind.Mail, "a"));
        Assert.Equal(ShelfReadOutcome.Absent, vault.Load(SyncKind.Mail, "a", SyncJsonContext.Default.MailShelf, MailShelf.Version).Outcome);
    }

    // ---- [S-2607] purge ----------------------------------------------------------------------------

    [Fact]
    public async Task S2607_signing_out_purges_every_kind_and_the_set_aside_copy_and_wins_over_a_queued_save()
    {
        var vault = new SyncVault(_dir, Key());
        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "1"), SyncJsonContext.Default.MailShelf);
        vault.SaveSoon(SyncKind.Contacts, "a", new ContactsShelf { Owner = "a", At = Now }, SyncJsonContext.Default.ContactsShelf);
        vault.SaveSoon(SyncKind.Mail, "b", Mail("b", "keep"), SyncJsonContext.Default.MailShelf);
        await vault.FlushAsync();
        File.WriteAllText(vault.ShelfPath(SyncKind.Calendar, "a")! + ".corrupt", "old");

        vault.SaveSoon(SyncKind.Mail, "a", Mail("a", "2"), SyncJsonContext.Default.MailShelf);
        vault.Purge("a");
        await vault.FlushAsync();

        Assert.False(File.Exists(vault.ShelfPath(SyncKind.Mail, "a")));
        Assert.False(File.Exists(vault.ShelfPath(SyncKind.Contacts, "a")));
        Assert.False(File.Exists(vault.ShelfPath(SyncKind.Calendar, "a") + ".corrupt"));
        Assert.True(File.Exists(vault.ShelfPath(SyncKind.Mail, "b")));
    }

    // ---- [S-2609] schedules ------------------------------------------------------------------------

    [Fact]
    public void S2609_a_shelf_younger_than_the_interval_is_not_due_at_start()
    {
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(5));
        schedule.Seed(Now.AddMinutes(-1), Now);

        Assert.False(schedule.IsDue(Now));
        Assert.True(schedule.IsDue(Now.AddMinutes(4)));
        Assert.Equal(FetchStatus.Ok, schedule.Status);
    }

    [Fact]
    public void S2609_an_old_a_missing_or_a_future_stamp_is_due_now()
    {
        foreach (DateTimeOffset? stamp in new DateTimeOffset?[] { Now.AddHours(-1), null, Now.AddDays(1) })
        {
            var schedule = new SyncSchedule(TimeSpan.FromMinutes(5));
            schedule.Seed(stamp, Now);
            Assert.True(schedule.IsDue(Now));
        }
    }

    [Fact]
    public void S2609_a_failure_backs_off_and_the_network_coming_back_ends_only_a_network_wait()
    {
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(2));
        schedule.Failed(FetchStatus.Offline, "down", Now);
        schedule.Failed(FetchStatus.Offline, "down", Now);
        Assert.False(schedule.IsDue(Now.AddMinutes(1)));

        schedule.Wake();
        Assert.True(schedule.IsDue(Now));

        var limited = new SyncSchedule(TimeSpan.FromMinutes(2));
        limited.Failed(FetchStatus.RateLimited, null, Now);
        limited.Wake();
        limited.Force(Now);
        Assert.False(limited.IsDue(Now.AddMinutes(1)));
    }

    [Fact]
    public void S2609_refresh_goes_now_and_a_busy_schedule_is_never_due()
    {
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(5));
        schedule.Succeeded(Now);
        schedule.Force(Now);
        Assert.True(schedule.IsDue(Now));

        schedule.Busy = true;
        Assert.False(schedule.IsDue(Now));
    }

    [Fact]
    public void S2609_a_refusal_is_held()
    {
        var schedule = new SyncSchedule(TimeSpan.FromMinutes(5));
        schedule.Failed(FetchStatus.BadResponse, null, Now);
        schedule.Hold(TimeSpan.FromHours(1), Now);

        Assert.False(schedule.IsDue(Now.AddMinutes(59)));
        Assert.True(schedule.IsDue(Now.AddMinutes(61)));
    }

    // ---- [S-2610] [S-2611] the write-back queue --------------------------------------------------

    private static PendingWrite Write(string id, PendingKind kind, string target, string container = "list", string? text = null)
        => new(id, kind, target, container, text, null, Now);

    [Fact]
    public void S2610_only_the_head_goes_and_a_waiting_head_holds_the_rest()
    {
        var queue = new List<PendingWrite> { Write("1", PendingKind.TaskComplete, "t1"), Write("2", PendingKind.TaskComplete, "t2") };

        Assert.Equal("1", WriteQueue.Ready(queue, Now)!.Id);

        var waiting = WriteQueue.Settle(queue, "1", WriteResult.Retry, Now, "offline");
        Assert.Null(WriteQueue.Ready(waiting, Now));
        Assert.Equal(1, waiting[0].Attempts);
        Assert.Equal("offline", waiting[0].LastError);
        Assert.Equal("1", WriteQueue.Ready(waiting, Now + WriteQueue.Delay(1))!.Id);
        Assert.Equal("1", WriteQueue.Ready(WriteQueue.Wake(waiting), Now)!.Id);
    }

    [Fact]
    public void S2610_done_and_refused_leave_the_queue()
    {
        var queue = new List<PendingWrite> { Write("1", PendingKind.TaskComplete, "t1"), Write("2", PendingKind.TaskRename, "t2", text: "x") };

        Assert.Equal(["2"], WriteQueue.Settle(queue, "1", WriteResult.Done, Now).Select(w => w.Id));
        Assert.Equal(["1"], WriteQueue.Settle(queue, "2", WriteResult.Refused, Now).Select(w => w.Id));
    }

    [Theory]
    [InlineData(AccountError.Offline, WriteResult.Retry)]
    [InlineData(AccountError.TimedOut, WriteResult.Retry)]
    [InlineData(AccountError.RateLimited, WriteResult.Retry)]
    [InlineData(AccountError.ServerError, WriteResult.Retry)]
    [InlineData(AccountError.SignInRequired, WriteResult.Retry)]
    [InlineData(AccountError.Forbidden, WriteResult.Refused)]
    [InlineData(AccountError.NotFound, WriteResult.Refused)]
    [InlineData(AccountError.BadResponse, WriteResult.Refused)]
    public void S2610_what_time_can_mend_waits_and_what_the_provider_refused_is_taken_back(AccountError error, WriteResult expected)
        => Assert.Equal(expected, WriteQueue.Classify(error));

    [Fact]
    public void S2610_the_wait_doubles_from_half_a_minute_to_half_an_hour()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), WriteQueue.Delay(1));
        Assert.Equal(TimeSpan.FromMinutes(1), WriteQueue.Delay(2));
        Assert.Equal(TimeSpan.FromMinutes(2), WriteQueue.Delay(3));
        Assert.Equal(WriteQueue.MaxDelay, WriteQueue.Delay(50));
    }

    [Fact]
    public void S2610_a_full_queue_refuses_a_new_change()
    {
        var queue = Enumerable.Range(0, WriteQueue.MaxPending).Select(i => Write($"w{i}", PendingKind.TaskComplete, $"t{i}")).ToList();
        Assert.Null(WriteQueue.Add(queue, Write("new", PendingKind.TaskComplete, "other"), headInFlight: false));
    }

    [Fact]
    public void S2610_a_stored_queue_is_validated()
    {
        var dropped = 0;
        var queue = WriteQueue.Validate(
            [
                Write("1", PendingKind.TaskComplete, "t"),
                Write("1", PendingKind.TaskReopen, "t"),
                Write("", PendingKind.TaskComplete, "t"),
                Write("3", PendingKind.TaskComplete, ""),
                null,
                Write("4", (PendingKind)99, "t"),
                Write("5", PendingKind.TaskDelete, "t") with { Attempts = -3 },
            ],
            ref dropped);

        Assert.Equal(["1", "5"], queue.Select(w => w.Id));
        Assert.Equal(0, queue[1].Attempts);
        Assert.Equal(5, dropped);
    }

    [Fact]
    public void S2611_a_second_tick_replaces_the_first_and_a_rename_replaces_a_rename()
    {
        var queue = WriteQueue.Add([], Write("1", PendingKind.TaskComplete, "t"), false)!;
        queue = WriteQueue.Add(queue, Write("2", PendingKind.TaskReopen, "t"), false)!;
        queue = WriteQueue.Add(queue, Write("3", PendingKind.TaskRename, "t", text: "a"), false)!;
        queue = WriteQueue.Add(queue, Write("4", PendingKind.TaskRename, "t", text: "b"), false)!;
        queue = WriteQueue.Add(queue, Write("5", PendingKind.TaskComplete, "other"), false)!;

        Assert.Equal(["2", "4", "5"], queue.Select(w => w.Id));
    }

    [Fact]
    public void S2611_a_delete_swallows_what_was_queued_for_the_task_and_cancels_an_unsent_create()
    {
        var queue = WriteQueue.Add([], Write("1", PendingKind.TaskComplete, "t"), false)!;
        queue = WriteQueue.Add(queue, Write("2", PendingKind.TaskDelete, "t"), false)!;
        Assert.Equal(["2"], queue.Select(w => w.Id));

        var created = WriteQueue.Add([], Write("c", PendingKind.TaskCreate, "pending-1", text: "new"), false)!;
        created = WriteQueue.Add(created, Write("r", PendingKind.TaskRename, "pending-1", text: "newer"), false)!;
        created = WriteQueue.Add(created, Write("d", PendingKind.TaskDelete, "pending-1"), false)!;
        Assert.Empty(created);
    }

    [Fact]
    public void S2611_the_head_on_the_wire_is_never_folded_away()
    {
        var queue = WriteQueue.Add([], Write("1", PendingKind.TaskComplete, "t"), false)!;
        queue = WriteQueue.Add(queue, Write("2", PendingKind.TaskReopen, "t"), headInFlight: true)!;

        Assert.Equal(["1", "2"], queue.Select(w => w.Id));
    }

    [Fact]
    public void S2611_mail_read_flags_fold_but_a_move_does_not_swallow_them()
    {
        var queue = WriteQueue.Add([], Write("1", PendingKind.MailMarkRead, "m", "inbox"), false)!;
        queue = WriteQueue.Add(queue, Write("2", PendingKind.MailMarkUnread, "m", "inbox"), false)!;
        queue = WriteQueue.Add(queue, Write("3", PendingKind.MailArchive, "m", "inbox"), false)!;

        Assert.Equal(["2", "3"], queue.Select(w => w.Id));
    }

    [Fact]
    public void S2611_a_confirmed_create_retargets_what_followed_it()
    {
        var queue = new List<PendingWrite> { Write("1", PendingKind.TaskRename, "pending-1", text: "x"), Write("2", PendingKind.TaskComplete, "other") };
        Assert.Equal(["real", "other"], WriteQueue.Retarget(queue, "pending-1", "real").Select(w => w.Target));
    }

    // ---- [S-2612] staleness ------------------------------------------------------------------------

    [Fact]
    public void S2612_a_stored_copy_says_why_it_is_stored_and_when_it_is_from()
    {
        var at = new DateTimeOffset(2026, 10, 11, 10, 42, 0, TimeSpan.Zero);
        var now = at.AddHours(1);

        var line = SyncFreshness.Line(Locale.En, FetchStatus.Offline, at, now);

        Assert.Equal($"{FetchState.Message(Locale.En, FetchStatus.Offline)} · updated {LocaleFormat.Time(Locale.En, at.DateTime)}", line);
        Assert.Null(SyncFreshness.Line(Locale.En, FetchStatus.Ok, at, now));
        Assert.Null(SyncFreshness.Line(Locale.En, FetchStatus.Offline, null, now));
    }

    [Fact]
    public void S2612_an_older_copy_names_its_weekday_then_its_date()
    {
        var now = new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero);

        Assert.StartsWith(LocaleFormat.WeekdayName(Locale.En, DayOfWeek.Friday, NameWidth.Short), SyncFreshness.When(Locale.En, now.AddDays(-2), now), StringComparison.Ordinal);
        Assert.Equal(LocaleFormat.Date(Locale.En, new DateOnly(2026, 9, 30), DateStyle.MonthDayShort), SyncFreshness.When(Locale.En, new DateTimeOffset(2026, 9, 30, 8, 0, 0, TimeSpan.Zero), now));
    }

    [Fact]
    public void S2612_a_mail_account_that_could_not_refresh_says_when_its_list_is_from()
    {
        var account = new MailAccount("w", AccountProvider.Microsoft, "Work", "w@x.com", null);
        var at = DateTimeOffset.Now.AddMinutes(-5);
        var outcome = new MailAccountOutcome("w", MailAccountState.Failed, FetchStatus.Offline, null, null, at);

        var screen = MailScreens.Decide(Locale.En, [account], [], [outcome], 3, MailFolder.Inbox);

        Assert.Equal(MailScreenKind.Messages, screen.Kind);
        Assert.Contains("Updated " + SyncFreshness.When(Locale.En, at, DateTimeOffset.Now), screen.Warning, StringComparison.Ordinal);
    }

    // ---- [S-2620]..[S-2623] mail -------------------------------------------------------------------

    [Fact]
    public void S2621_a_mail_shelf_is_bounded_and_cleaned_on_load()
    {
        var shelf = new MailShelf
        {
            Owner = "a",
            Folders =
            [
                new MailFolderShelf { Folder = MailFolder.Inbox, Count = 500, Total = -1, Messages = [.. Enumerable.Range(0, 80).Select(i => new StoredMail { RawId = $"m{i}", Preview = new string('x', 5000) }).Append(new StoredMail { RawId = "m1" }).Append(new StoredMail { RawId = "" })] },
                new MailFolderShelf { Folder = MailFolder.Inbox },
                new MailFolderShelf { Folder = MailFolder.Archive },
            ],
            Pending = [Write("1", PendingKind.MailMarkRead, "m1", "inbox"), Write("2", PendingKind.TaskComplete, "t"), Write("3", PendingKind.MailJunk, "m2", "nowhere")],
        };

        var dropped = 0;
        var clean = MailShelves.Validate(shelf, ref dropped);

        var inbox = Assert.Single(clean.Folders);
        Assert.Equal(MailShelves.MaxPerFolder, inbox.Messages.Count);
        Assert.True(inbox.Messages.All(m => m.Preview.Length <= 400));
        Assert.Equal(MailShelves.MaxPerFolder, inbox.Count);
        Assert.Null(inbox.Total);
        Assert.Equal(["1"], clean.Pending.Select(w => w.Id));
        Assert.True(dropped > 0);
    }

    [Fact]
    public void S2622_queued_changes_show_on_top_of_what_the_provider_last_said()
    {
        var messages = new List<MailMessage>
        {
            new("a", AccountProvider.Microsoft, MailFolder.Inbox, "1", "One", "", "", Now, false, "", ""),
            new("a", AccountProvider.Microsoft, MailFolder.Inbox, "2", "Two", "", "", Now, true, "", ""),
            new("a", AccountProvider.Microsoft, MailFolder.Inbox, "3", "Three", "", "", Now, true, "", ""),
        };

        var shown = MailShelves.Overlay(messages,
            [Write("x", PendingKind.MailMarkRead, "1", "inbox"), Write("y", PendingKind.MailMarkUnread, "2", "inbox"), Write("z", PendingKind.MailDelete, "3", "inbox"), Write("w", PendingKind.MailDelete, "1", "junkemail")],
            MailFolder.Inbox);

        Assert.Equal(["1", "2"], shown.Select(m => m.RawId));
        Assert.True(shown[0].IsRead);
        Assert.False(shown[1].IsRead);
    }

    [Fact]
    public void S2620_a_message_round_trips_through_the_shelf_without_its_account()
    {
        var message = new MailMessage("a", AccountProvider.Imap, MailFolder.Sent, "Sent|4|9", "Hi", "Dana", "d@x.com", Now, true, "Preview", "");
        var back = MailShelves.Restore(MailShelves.Store(message), "a", AccountProvider.Imap, MailFolder.Sent);

        Assert.Equal(message, back);
    }

    // ---- [S-2630]..[S-2632] calendar ---------------------------------------------------------------

    private static CalendarEvent Event(string id, DateTimeOffset start, double hours = 1) => new() { Id = id, Subject = id, Start = start, End = start.AddHours(hours) };

    [Fact]
    public void S2631_the_window_reaches_a_year_each_way_and_the_near_weeks_sit_inside_it()
    {
        var today = new DateOnly(2026, 10, 11);
        var (from, to) = CalendarSync.Window(today, TimeZoneInfo.Utc);
        var (nearFrom, nearTo) = CalendarSync.Near(today, TimeZoneInfo.Utc);

        Assert.Equal(new DateTimeOffset(2025, 10, 11, 0, 0, 0, TimeSpan.Zero), from);
        Assert.Equal(new DateTimeOffset(2027, 10, 12, 0, 0, 0, TimeSpan.Zero), to);
        Assert.True(from < nearFrom && nearTo < to);
    }

    [Fact]
    public void S2631_a_near_refresh_replaces_what_it_covers_and_keeps_the_rest()
    {
        var full = CalendarSync.Merge(null, null,
            [Event("old-far", Now.AddDays(-100)), Event("moved", Now.AddDays(1)), Event("gone", Now.AddDays(2)), Event("future", Now.AddDays(200))],
            Now.AddDays(-365), Now.AddDays(365), Now, full: true);

        var merged = CalendarSync.Merge(full, null,
            [Event("moved", Now.AddDays(3)), Event("new", Now.AddDays(4))],
            Now.AddDays(-7), Now.AddDays(42), Now.AddMinutes(5), full: false);

        Assert.Equal(["old-far", "moved", "new", "future"], merged.Events.Select(e => e.Id));
        Assert.Equal(full.From, merged.From);
        Assert.Equal(full.To, merged.To);
        Assert.Equal(full.FullAt, merged.FullAt);
        Assert.Equal(Now.AddMinutes(5), merged.NearAt);
    }

    [Fact]
    public void S2631_a_refresh_that_does_not_touch_the_held_window_starts_again()
    {
        var held = CalendarSync.Merge(null, "c", [Event("a", Now)], Now.AddDays(-1), Now.AddDays(1), Now, full: true);
        var moved = CalendarSync.Merge(held, "c", [Event("b", Now.AddDays(30))], Now.AddDays(29), Now.AddDays(31), Now, full: false);

        Assert.Equal(["b"], moved.Events.Select(e => e.Id));
        Assert.Equal(Now.AddDays(29), moved.From);
    }

    [Fact]
    public void S2631_a_request_inside_the_window_is_answered_from_it()
    {
        var span = CalendarSync.Merge(null, null,
            [Event("in", Now.AddHours(2)), Event("out", Now.AddDays(10)), Event("across", Now.AddHours(-2), 4), new CalendarEvent { Id = "point", Start = Now, End = Now }],
            Now.AddDays(-30), Now.AddDays(30), Now, full: true);

        Assert.True(CalendarSync.Covers(span, Now, Now.AddDays(1)));
        Assert.False(CalendarSync.Covers(span, Now, Now.AddDays(31)));
        Assert.Equal(["across", "point", "in"], CalendarSync.Slice(span, Now, Now.AddDays(1)).Select(e => e.Id));
    }

    [Fact]
    public void S2630_a_window_keeps_the_events_nearest_the_fetch_when_it_overflows()
    {
        var many = Enumerable.Range(0, OfficeShelves.MaxEvents + 10).Select(i => Event($"e{i}", Now.AddHours(i))).ToList();
        var span = CalendarSync.Merge(null, null, many, Now, Now.AddYears(2), Now, full: true);

        Assert.Equal(OfficeShelves.MaxEvents, span.Events.Count);
        Assert.Equal("e0", span.Events[0].Id);
    }

    [Fact]
    public void S2632_a_calendar_shelf_is_cleaned_on_load()
    {
        var shelf = new CalendarShelf
        {
            Owner = "a",
            Calendars = [new StoredCalendar { Id = "c", Name = "Work", Color = "#ff000080" }, new StoredCalendar { Id = "c" }, new StoredCalendar { Id = "" }],
            Spans =
            [
                new CalendarSpan { CalendarId = null, From = Now, To = Now.AddDays(1), Events = [new StoredEvent { Id = "e", Start = Now, End = Now.AddHours(-1) }, new StoredEvent { Id = "e" }, new StoredEvent { Id = "" }] },
                new CalendarSpan { CalendarId = null, From = Now, To = Now.AddDays(1) },
                new CalendarSpan { CalendarId = "x", From = Now, To = Now },
            ],
            Birthdays = [new StoredBirthday { Name = "Ann", Month = 2, Day = 29 }, new StoredBirthday { Name = "Bad", Month = 2, Day = 30 }, new StoredBirthday { Name = " ", Month = 1, Day = 1 }],
        };

        var dropped = 0;
        var clean = OfficeShelves.Validate(shelf, ref dropped);

        Assert.Equal("#ff0000", Assert.Single(clean.Calendars!).Color);
        var span = Assert.Single(clean.Spans);
        var item = Assert.Single(span.Events);
        Assert.Equal(item.Start, item.End);
        Assert.Equal("Ann", Assert.Single(clean.Birthdays!).Name);
        Assert.Equal(8, dropped);
    }

    [Fact]
    public void S2630_an_event_round_trips_through_the_shelf()
    {
        var all = CalendarEvent.AllDayEvent("d", "Holiday", new DateOnly(2026, 12, 24), new DateOnly(2026, 12, 27)) with { Location = "Home", WebLink = "https://x.example/e" };
        var back = OfficeShelves.Restore(OfficeShelves.Store(all));

        Assert.Equal(all, back);
        Assert.Equal(new DateOnly(2026, 12, 24), back.FirstDay);
    }

    // ---- [S-2640] [S-2641] contacts ----------------------------------------------------------------

    [Fact]
    public void S2641_a_contacts_shelf_keeps_people_with_an_id_and_a_name_once()
    {
        var shelf = new ContactsShelf
        {
            Owner = "a",
            At = Now,
            Contacts = [new StoredContact { Id = "1", Name = "Ann" }, new StoredContact { Id = "1", Name = "Again" }, new StoredContact { Id = "2", Name = "" }, new StoredContact { Id = "", Name = "X" }],
        };

        var dropped = 0;
        var clean = OfficeShelves.Validate(shelf, ref dropped);

        Assert.Equal("Ann", Assert.Single(clean.Contacts).Name);
        Assert.Equal(3, dropped);
        Assert.Equal("a", OfficeShelves.Restore(clean.Contacts[0], "a").AccountId);
    }

    // ---- [S-2650]..[S-2652] tasks ------------------------------------------------------------------

    [Fact]
    public void S2651_a_task_shelf_is_cleaned_on_load()
    {
        var shelf = new TasksShelf
        {
            Owner = "a",
            Lists = [new StoredTaskList { Id = "l", Name = "Inbox", IsDefault = true }, new StoredTaskList { Id = "l" }],
            Held =
            [
                new TaskListShelf { ListId = "l", Tasks = [new StoredTask { Id = "t", Title = "One" }, new StoredTask { Id = "t" }, new StoredTask { Id = "" }] },
                new TaskListShelf { ListId = TaskRules.TodayListId },
                new TaskListShelf { ListId = "l" },
            ],
            Pending =
            [
                Write("1", PendingKind.TaskRename, "t", "l", text: " "),
                Write("2", PendingKind.TaskCreate, "pending-x", "l", text: "New"),
                Write("3", PendingKind.MailMarkRead, "m", "inbox"),
                Write("4", PendingKind.TaskComplete, "t", ""),
            ],
        };

        var dropped = 0;
        var clean = TaskShelves.Validate(shelf, ref dropped);

        Assert.Single(clean.Lists!);
        var held = Assert.Single(clean.Held);
        Assert.Equal("One", Assert.Single(held.Tasks).Title);
        Assert.Equal(["2"], clean.Pending.Select(w => w.Id));
        Assert.Equal(8, dropped);
    }

    [Fact]
    public void S2652_today_reads_every_list_and_finished_tasks_only_where_shown()
    {
        var lists = new List<TaskListInfo> { new("a", "A", false), new("b", "B", true), new("c", "C", false) };

        var wanted = TaskShelves.Wanted(lists, [("a", true), (TaskRules.TodayListId, false), ("", false), ("gone", false)]);

        Assert.Equal([("a", true), ("b", false), ("c", false)], wanted.OrderBy(w => w.ListId, StringComparer.Ordinal));
    }

    [Fact]
    public void S2652_the_default_list_stands_for_an_empty_list_id()
        => Assert.Equal([("b", false)], TaskShelves.Wanted([new("a", "A", false), new("b", "B", true)], [("", false)]));

    [Fact]
    public void S2650_a_task_round_trips_through_the_shelf()
    {
        var task = new TaskItem("t", "l", "Call", true, true, "2026-10-12T00:00:00.0000000");
        Assert.Equal(task, TaskShelves.Restore(TaskShelves.Store(task), "l"));
    }

    // ---- [S-2608] interests ------------------------------------------------------------------------

    [Fact]
    public void S2608_the_needs_of_every_view_are_collected_and_keyed()
    {
        var state = new AppState
        {
            Views =
            [
                new ViewState
                {
                    Id = "v1",
                    Name = "One",
                    Widgets =
                    [
                        new WidgetInstance { Id = "m", Kind = WidgetKind.Email, Config = new EmailConfig { Folder = MailFolder.Junk } },
                        new WidgetInstance { Id = "t", Kind = WidgetKind.Tasks, Config = new TasksConfig { AccountId = "acc", ListId = "l", ShowCompleted = true } },
                        new WidgetInstance { Id = "local", Kind = WidgetKind.Tasks, Config = new TasksConfig { Source = TaskSource.Local } },
                    ],
                },
                new ViewState
                {
                    Id = "v2",
                    Name = "Two",
                    Widgets =
                    [
                        new WidgetInstance { Id = "c", Kind = WidgetKind.MonthCalendar, Config = new MonthCalendarConfig { AccountIds = ["acc"] } },
                        new WidgetInstance { Id = "p", Kind = WidgetKind.Contacts, Config = new ContactsConfig() },
                    ],
                },
            ],
        };

        var interests = SyncInterests.From(state);

        Assert.Equal(MailFolder.Junk, Assert.Single(interests.Mail).Folder);
        var task = Assert.Single(interests.Tasks);
        Assert.Equal(("acc", "l", true), (task.AccountId, task.ListId, task.ShowCompleted));
        Assert.Equal(["acc"], Assert.Single(interests.Calendar).AccountIds);
        Assert.Single(interests.Contacts);
        Assert.Equal(interests.Key(), SyncInterests.From(state).Key());

        ((EmailConfig)state.Views[0].Widgets[0].Config).Folder = MailFolder.Sent;
        Assert.NotEqual(interests.Key(), SyncInterests.From(state).Key());
    }

    [Fact]
    public void S2660_the_web_shelf_carries_the_cache_document_unchanged()
    {
        var shelf = new WebShelf { Owner = WebShelf.NetworkOwner, Json = "{\"schemaVersion\":1,\"feeds\":[],\"quotes\":[]}" };
        var back = JsonSerializer.Deserialize(JsonSerializer.SerializeToUtf8Bytes(shelf, SyncJsonContext.Default.WebShelf), SyncJsonContext.Default.WebShelf)!;

        Assert.Equal(shelf.Json, back.Json);
        Assert.Equal(WebShelf.Version, back.SchemaVersion);
    }
}
