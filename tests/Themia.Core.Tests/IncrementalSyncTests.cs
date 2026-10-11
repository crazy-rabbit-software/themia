using System.Text;
using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Tasks;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2680]..[S-2689] Incremental sync: cursors, change pages, and folding changes into the bounded shelves.</summary>
public sealed class IncrementalSyncTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 10, 42, 0, TimeSpan.Zero);

    private const string GraphLink = "https://graph.microsoft.com/v1.0/me/calendarView/delta?$deltatoken=abc";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-delta-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CalendarEvent Event(string id, int day, string subject = "x") => new()
    {
        Id = id,
        Subject = subject,
        Start = Now.AddDays(day),
        End = Now.AddDays(day).AddHours(1),
    };

    private static CalendarSpan Span(params CalendarEvent[] events) => new()
    {
        From = Now.AddDays(-365),
        To = Now.AddDays(365),
        NearAt = Now.AddHours(-1),
        FullAt = Now.AddHours(-1),
        Events = [.. events.Select(OfficeShelves.Store)],
    };

    private static MailMessage Mail(string id, int minutesAgo, bool read = false, MailFolder folder = MailFolder.Inbox)
        => new("a", AccountProvider.Microsoft, folder, id, "S " + id, "N", "n@example.com", Now.AddMinutes(-minutesAgo), read, "p", string.Empty);

    private static MailMessage Imap(uint uid, bool read = false)
        => new("a", AccountProvider.Imap, MailFolder.Inbox, new ImapMessageId(MailFolder.Inbox, uid, 7).ToString(), "S", "N", "n@example.com", Now, read, "p", string.Empty);

    private static Contact Person(string id, string name) => new() { Id = id, Name = name };

    private static TaskItem Task(string id, bool done = false, string title = "t") => new(id, "L", title, done, false, null);

    private static ImapResponse Parse(string response) => ImapParser.Parse(Encoding.Latin1.GetBytes(response));

    // ---- [S-2680] cursors on the shelves -----------------------------------------------------------

    [Fact]
    public async Task S2680_a_cursor_is_kept_sealed_with_what_it_describes()
    {
        var vault = new SyncVault(_dir, Enumerable.Repeat((byte)3, SealedBlob.KeySize).ToArray());
        var span = Span(Event("e1", 1));
        span.Cursor = SyncCursors.Start(CursorKind.GraphDelta, GraphLink, "w", Now);
        vault.SaveSoon(SyncKind.Calendar, "a", new CalendarShelf { Owner = "a", Spans = [span] }, SyncJsonContext.Default.CalendarShelf);
        await vault.FlushAsync();

        var read = vault.Load(SyncKind.Calendar, "a", SyncJsonContext.Default.CalendarShelf, CalendarShelf.Version).Value!;
        var dropped = 0;
        var cursor = OfficeShelves.Validate(read, ref dropped).Spans[0].Cursor;

        Assert.Equal(0, dropped);
        Assert.Equal(GraphLink, cursor!.Value);
        Assert.Equal(CursorKind.GraphDelta, cursor.Kind);
        Assert.DoesNotContain("deltatoken", File.ReadAllText(vault.ShelfPath(SyncKind.Calendar, "a")!), StringComparison.Ordinal);
    }

    [Fact]
    public void S2680_advancing_a_cursor_keeps_the_full_fetch_it_grew_from()
    {
        var start = SyncCursors.Start(CursorKind.GoogleSyncToken, "t1", "s", Now.AddHours(-5));
        var next = SyncCursors.Advance(start, "t2", Now);

        Assert.Equal("t2", next.Value);
        Assert.Equal(Now.AddHours(-5), next.Since);
        Assert.Equal(Now, next.At);
        Assert.Equal("t1", start.Value);
    }

    // ---- [S-2681] validation and fallback -----------------------------------------------------------

    [Theory]
    [InlineData("https://evil.example.com/v1.0/me/calendarView/delta?$deltatoken=a")]
    [InlineData("http://graph.microsoft.com/v1.0/me/calendarView/delta")]
    [InlineData("https://user:pw@graph.microsoft.com/v1.0/x")]
    [InlineData("https://graph.microsoft.com:8443/v1.0/x")]
    [InlineData("not a url")]
    public void S2681_a_graph_cursor_that_would_send_the_token_elsewhere_is_dropped(string link)
    {
        var dropped = 0;
        var cursor = new SyncCursor { Kind = CursorKind.GraphDelta, Value = link, Since = Now, At = Now };

        Assert.Null(SyncCursors.Validate(cursor, ref dropped));
        Assert.Equal(1, dropped);
    }

    [Fact]
    public void S2681_a_cursor_of_another_format_kind_or_shape_is_dropped()
    {
        SyncCursor Good() => SyncCursors.Start(CursorKind.GoogleSyncToken, "tok", "s", Now);
        var bad = new List<SyncCursor>
        {
            new() { Format = 99, Kind = CursorKind.GoogleSyncToken, Value = "t", Since = Now, At = Now },
            new() { Kind = (CursorKind)42, Value = "t", Since = Now, At = Now },
            new() { Kind = CursorKind.GoogleSyncToken, Value = "", Since = Now, At = Now },
            new() { Kind = CursorKind.GoogleSyncToken, Value = new string('x', SyncCursors.MaxValue + 1), Since = Now, At = Now },
            new() { Kind = CursorKind.GoogleSyncToken, Value = "t", Since = default, At = Now },
            new() { Kind = CursorKind.GoogleSyncToken, Value = "t", Since = Now, At = Now.AddHours(-1) },
            new() { Kind = CursorKind.GoogleUpdatedMin, Value = "yesterday", Since = Now, At = Now },
            new() { Kind = CursorKind.Dav, Value = "ctag", Since = Now, At = Now },
            new() { Kind = CursorKind.Dav, Value = "ctag", Since = Now, At = Now, Resources = [new DavTag { Href = "/a", Etag = "1" }, new DavTag { Href = "/a", Etag = "2" }] },
            new() { Kind = CursorKind.Imap, Since = Now, At = Now },
            new() { Kind = CursorKind.Imap, Since = Now, At = Now, Imap = new ImapMark { UidValidity = 0, UidNext = 5 } },
        };

        var dropped = 0;
        Assert.All(bad, c => Assert.Null(SyncCursors.Validate(c, ref dropped)));
        Assert.Equal(bad.Count, dropped);
        Assert.NotNull(SyncCursors.Validate(Good(), ref dropped));
    }

    [Fact]
    public void S2681_a_bad_cursor_on_a_shelf_costs_the_cursor_not_the_data()
    {
        var shelf = new MailShelf
        {
            Owner = "a",
            Folders = [new MailFolderShelf { Folder = MailFolder.Inbox, At = Now, Count = 1, Messages = [new StoredMail { RawId = "m1" }], Cursor = new SyncCursor { Kind = CursorKind.GraphDelta, Value = "https://x.example/", Since = Now, At = Now } }],
        };

        var dropped = 0;
        var valid = MailShelves.Validate(shelf, ref dropped);

        Assert.Equal(1, dropped);
        Assert.Null(valid.Folders[0].Cursor);
        Assert.Equal("m1", Assert.Single(valid.Folders[0].Messages).RawId);
    }

    [Fact]
    public void S2681_missing_other_scope_and_other_kind_mean_a_full_fetch()
    {
        var cursor = SyncCursors.Start(CursorKind.GraphDelta, GraphLink, "2025-10-11|2027-10-11", Now);

        Assert.Equal(CursorCheck.Missing, SyncCursors.Check(null, CursorKind.GraphDelta, "s", Now));
        Assert.Equal(CursorCheck.Usable, SyncCursors.Check(cursor, CursorKind.GraphDelta, "2025-10-11|2027-10-11", Now.AddMinutes(5)));
        Assert.Equal(CursorCheck.OtherScope, SyncCursors.Check(cursor, CursorKind.GraphDelta, "2025-10-12|2027-10-12", Now));
        Assert.Equal(CursorCheck.OtherScope, SyncCursors.Check(cursor, CursorKind.GoogleSyncToken, "2025-10-11|2027-10-11", Now));
    }

    [Theory]
    [InlineData(410, null, true)]
    [InlineData(410, "Sync token is no longer valid, a full sync is required.", true)]
    [InlineData(400, "Sync token is expired. Clear local cache and retry call without the sync token.", true)]
    [InlineData(400, "Badly formed token.", true)]
    [InlineData(400, "Invalid filter clause", false)]
    [InlineData(400, null, false)]
    [InlineData(401, "token expired", false)]
    [InlineData(404, "sync", false)]
    [InlineData(429, null, false)]
    public void S2681_a_refused_cursor_is_told_from_any_other_failure(int status, string? message, bool rejected)
        => Assert.Equal(rejected, DeltaRules.Rejected(status, message));

    // ---- [S-2682] the daily safety net --------------------------------------------------------------

    [Fact]
    public void S2682_a_chain_a_day_old_is_stale_however_recently_it_moved()
    {
        var cursor = SyncCursors.Advance(SyncCursors.Start(CursorKind.GoogleSyncToken, "t", "s", Now.AddHours(-24)), "t9", Now.AddMinutes(-1));

        Assert.Equal(CursorCheck.Stale, SyncCursors.Check(cursor, CursorKind.GoogleSyncToken, "s", Now));
        Assert.Equal(CursorCheck.Usable, SyncCursors.Check(cursor, CursorKind.GoogleSyncToken, "s", Now.AddHours(-1)));
    }

    [Fact]
    public void S2682_a_chain_from_the_future_is_stale()
    {
        var cursor = SyncCursors.Start(CursorKind.GoogleSyncToken, "t", "s", Now.AddDays(2));
        Assert.Equal(CursorCheck.Stale, SyncCursors.Check(cursor, CursorKind.GoogleSyncToken, "s", Now));
    }

    // ---- [S-2683] change pages ----------------------------------------------------------------------

    [Fact]
    public void S2683_a_graph_page_carries_changes_removals_and_the_next_link()
    {
        const string body = """
            {
              "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/calendarView/delta?$skiptoken=s1",
              "value": [
                { "id": "e1", "subject": "Standup", "isCancelled": false, "start": { "dateTime": "2026-10-12T09:00:00.0000000", "timeZone": "UTC" }, "end": { "dateTime": "2026-10-12T09:15:00.0000000", "timeZone": "UTC" } },
                { "@odata.type": "#microsoft.graph.event", "id": "e2", "@removed": { "reason": "deleted" } },
                42,
                { "@removed": { "reason": "deleted" } }
              ]
            }
            """;

        var page = DeltaPages.Read(body, DeltaDialect.Graph, OfficeJsonContext.Default.GraphEvent);

        Assert.Equal("e1", Assert.Single(page.Items).Id);
        Assert.Equal(["e2"], page.Removed);
        Assert.NotNull(page.NextLink);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public void S2683_the_last_graph_page_carries_the_delta_link_and_only_on_graph()
    {
        var last = DeltaPages.Read("""{"@odata.deltaLink":"https://graph.microsoft.com/v1.0/me/contactFolders/f/contacts/delta?$deltatoken=d","value":[]}""",
            DeltaDialect.Graph, OfficeJsonContext.Default.GraphContact);
        var foreign = DeltaPages.Read("""{"@odata.deltaLink":"https://graph.attacker.example/delta?$deltatoken=d","value":[]}""",
            DeltaDialect.Graph, OfficeJsonContext.Default.GraphContact);

        Assert.StartsWith("https://graph.microsoft.com/", last.Cursor, StringComparison.Ordinal);
        Assert.Null(foreign.Cursor);
    }

    [Fact]
    public void S2683_a_google_events_page_reads_cancelled_as_removed_and_the_sync_token()
    {
        const string body = """
            {
              "kind": "calendar#events",
              "nextSyncToken": "CPDAlvWDx70CEPDAlvWDx70CGAU=",
              "items": [
                { "id": "g1", "status": "confirmed", "summary": "Lunch", "start": { "dateTime": "2026-10-12T12:00:00Z" }, "end": { "dateTime": "2026-10-12T13:00:00Z" } },
                { "id": "g2_20261013", "status": "cancelled" }
              ]
            }
            """;

        var page = DeltaPages.Read(body, DeltaDialect.GoogleEvents, OfficeJsonContext.Default.GoogleEvent);

        Assert.Equal("g1", Assert.Single(page.Items).Id);
        Assert.Equal(["g2_20261013"], page.Removed);
        Assert.Equal("CPDAlvWDx70CEPDAlvWDx70CGAU=", page.Cursor);
    }

    [Fact]
    public void S2683_a_sync_token_beside_a_next_page_is_not_the_end_of_the_round()
    {
        var page = DeltaPages.Read("""{"nextPageToken":"p2","nextSyncToken":"early","items":[]}""", DeltaDialect.GoogleEvents, OfficeJsonContext.Default.GoogleEvent);

        Assert.Equal("p2", page.NextPageToken);
        Assert.Null(page.Cursor);
    }

    [Fact]
    public void S2683_a_people_page_reads_deleted_connections_by_resource_name()
    {
        const string body = """
            {
              "connections": [
                { "resourceName": "people/c1", "names": [ { "displayName": "Ada Lovelace" } ] },
                { "resourceName": "people/c2", "metadata": { "deleted": true } }
              ],
              "nextSyncToken": "ps1",
              "totalPeople": 1
            }
            """;

        var page = DeltaPages.Read(body, DeltaDialect.GooglePeople, OfficeJsonContext.Default.GooglePersonRecord);

        Assert.Equal("people/c1", Assert.Single(page.Items).ResourceName);
        Assert.Equal(["people/c2"], page.Removed);
        Assert.Equal("ps1", page.Cursor);
    }

    [Fact]
    public void S2683_a_google_tasks_page_reads_deleted_and_hidden_as_removed_and_has_no_token()
    {
        const string body = """
            {
              "items": [
                { "id": "t1", "title": "Milk", "status": "needsAction" },
                { "id": "t2", "title": "Gone", "deleted": true },
                { "id": "t3", "title": "Cleared", "status": "completed", "hidden": true }
              ],
              "nextSyncToken": "never"
            }
            """;

        var page = DeltaPages.Read(body, DeltaDialect.GoogleTasks, TaskWireContext.Default.GoogleTask);

        Assert.Equal("t1", Assert.Single(page.Items).Id);
        Assert.Equal(["t2", "t3"], page.Removed);
        Assert.Null(page.Cursor);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("<html>proxy</html>")]
    public void S2683_a_page_that_is_not_an_object_is_refused(string body)
        => Assert.ThrowsAny<JsonException>(() => DeltaPages.Read(body, DeltaDialect.Graph, OfficeJsonContext.Default.GraphEvent));

    // ---- [S-2684] calendar ---------------------------------------------------------------------------

    [Fact]
    public void S2684_changes_replace_by_id_removals_leave_the_rest_stays()
    {
        var held = Span(Event("a", 1, "old"), Event("b", 2), Event("c", 3));

        var span = DeltaMerge.Events(held, [Event("a", 1, "new"), Event("d", 4)], ["b"], Now);

        Assert.Equal(["a", "c", "d"], span.Events.Select(e => e.Id));
        Assert.Equal("new", span.Events[0].Subject);
        Assert.Equal(held.From, span.From);
        Assert.Equal(held.To, span.To);
        Assert.Equal(Now, span.NearAt);
        Assert.Equal(Now, span.FullAt);
    }

    [Fact]
    public void S2684_an_event_moved_out_of_the_window_leaves_and_one_outside_never_joins()
    {
        var held = Span(Event("a", 1));

        var span = DeltaMerge.Events(held, [Event("a", 400), Event("far", -500)], [], Now);

        Assert.Empty(span.Events);
    }

    [Fact]
    public void S2684_a_removal_and_a_change_of_the_same_id_is_a_removal()
    {
        var span = DeltaMerge.Events(Span(Event("a", 1)), [Event("a", 2)], ["a"], Now);
        Assert.Empty(span.Events);
    }

    [Fact]
    public void S2684_a_page_naming_an_event_twice_means_its_later_state()
    {
        var span = DeltaMerge.Events(Span(), [Event("a", 1, "first"), Event("a", 1, "second")], [], Now);
        Assert.Equal("second", Assert.Single(span.Events).Subject);
    }

    [Fact]
    public void S2684_a_delta_keeps_the_window_bound()
    {
        var many = Enumerable.Range(0, OfficeShelves.MaxEvents + 10).Select(i => Event("e" + i, i % 300)).ToList();
        var span = DeltaMerge.Events(Span(), many, [], Now);
        Assert.Equal(OfficeShelves.MaxEvents, span.Events.Count);
    }

    // ---- [S-2685] contacts --------------------------------------------------------------------------

    [Fact]
    public void S2685_contacts_upsert_remove_and_stay_in_name_order()
    {
        var (people, whole) = DeltaMerge.Contacts(
            [Person("1", "Bea"), Person("2", "Cy")],
            [Person("2", "Al"), Person("3", "Dee")],
            ["1"],
            OfficeShelves.MaxContacts);

        Assert.Equal(["Al", "Dee"], people.Select(p => p.Name));
        Assert.True(whole);
    }

    [Fact]
    public void S2685_a_book_at_its_bound_is_not_whole_so_no_cursor_follows()
    {
        var held = Enumerable.Range(0, 3).Select(i => Person("p" + i, "N" + i)).ToList();

        var (people, whole) = DeltaMerge.Contacts(held, [Person("x", "A")], [], 3);

        Assert.Equal(3, people.Count);
        Assert.False(whole);
        Assert.Equal("A", people[0].Name);
    }

    // ---- [S-2686] tasks -----------------------------------------------------------------------------

    [Fact]
    public void S2686_a_changed_task_keeps_its_place_and_a_new_one_joins_at_the_end()
    {
        var (tasks, whole) = DeltaMerge.Tasks([Task("1"), Task("2"), Task("3")], [Task("4"), Task("2", title: "renamed")], ["3"], includeCompleted: false, 400);

        Assert.Equal(["1", "2", "4"], tasks.Select(t => t.Id));
        Assert.Equal("renamed", tasks[1].Title);
        Assert.True(whole);
    }

    [Fact]
    public void S2686_a_task_finished_elsewhere_leaves_a_list_held_without_finished_ones_and_comes_back_reopened()
    {
        var (done, _) = DeltaMerge.Tasks([Task("1"), Task("2")], [Task("1", done: true)], [], includeCompleted: false, 400);
        var (reopened, _) = DeltaMerge.Tasks(done, [Task("1")], [], includeCompleted: false, 400);
        var (kept, _) = DeltaMerge.Tasks([Task("1"), Task("2")], [Task("1", done: true)], [], includeCompleted: true, 400);

        Assert.Equal(["2"], done.Select(t => t.Id));
        Assert.Equal(["2", "1"], reopened.Select(t => t.Id));
        Assert.True(kept[0].Completed);
    }

    [Fact]
    public void S2686_a_list_at_its_bound_is_not_whole()
    {
        var (tasks, whole) = DeltaMerge.Tasks([], [Task("1"), Task("2"), Task("3")], [], includeCompleted: true, 2);
        Assert.Equal(2, tasks.Count);
        Assert.False(whole);
    }

    [Fact]
    public void S2686_a_queued_change_still_wins_over_a_delta_that_left_before_it()
    {
        // The provider's state, folded in…
        var (tasks, _) = DeltaMerge.Tasks([Task("1")], [Task("1", title: "server")], [], includeCompleted: false, 400);

        // …and the user's queued rename laid on top when read ([S-2653]).
        var edits = new TaskEdits();
        var stamp = edits.FetchStarted();
        edits.Begin(TaskEdit.Rename("1", "mine"));
        edits.Landed(tasks, stamp);
        var shown = edits.Current;

        Assert.Equal("mine", Assert.Single(shown).Title);
    }

    [Fact]
    public void S2686_cursor_scopes_tell_lists_and_finished_tasks_apart()
    {
        Assert.NotEqual(TaskShelves.CursorScope("L", true), TaskShelves.CursorScope("L", false));
        Assert.NotEqual(TaskShelves.CursorScope("L", true), TaskShelves.CursorScope("M", true));
    }

    [Fact]
    public void S2686_google_tasks_since_asks_for_finished_hidden_and_deleted_ones()
    {
        var url = TaskWire.GoogleTasksSince("list/1", new DateTimeOffset(2026, 10, 11, 8, 0, 0, TimeSpan.Zero)).AbsoluteUri;

        Assert.Contains("lists/list%2F1/tasks", url, StringComparison.Ordinal);
        Assert.Contains("showDeleted=true", url, StringComparison.Ordinal);
        Assert.Contains("showHidden=true", url, StringComparison.Ordinal);
        Assert.Contains("showCompleted=true", url, StringComparison.Ordinal);
        Assert.Contains("updatedMin=2026-10-11T08%3A00%3A00.000Z", url, StringComparison.Ordinal);
    }

    // ---- [S-2687] mail ------------------------------------------------------------------------------

    [Fact]
    public void S2687_new_mail_joins_on_top_and_the_oldest_falls_off_the_bound()
    {
        var held = new[] { Mail("m1", 10), Mail("m2", 20), Mail("m3", 30) };

        var (messages, refill) = DeltaMerge.Mail(held, [Mail("m0", 1)], [], 3, DeltaMerge.ByReceived);

        Assert.Equal(["m0", "m1", "m2"], messages.Select(m => m.RawId));
        Assert.False(refill);
    }

    [Fact]
    public void S2687_a_read_flag_from_the_provider_changes_the_held_message()
    {
        var (messages, _) = DeltaMerge.Mail([Mail("m1", 10)], [Mail("m1", 10, read: true)], [], 50, DeltaMerge.ByReceived);
        Assert.True(Assert.Single(messages).IsRead);
    }

    [Fact]
    public void S2687_a_change_to_a_message_older_than_the_list_does_not_push_in()
    {
        var held = new[] { Mail("m1", 10), Mail("m2", 20) };

        var (messages, _) = DeltaMerge.Mail(held, [Mail("old", 999, read: true)], [], 2, DeltaMerge.ByReceived);

        Assert.Equal(["m1", "m2"], messages.Select(m => m.RawId));
    }

    [Fact]
    public void S2687_a_removal_from_a_full_folder_needs_a_refill()
    {
        var held = new[] { Mail("m1", 10), Mail("m2", 20) };

        Assert.True(DeltaMerge.Mail(held, [], ["m1"], 2, DeltaMerge.ByReceived).Refill);
        Assert.True(DeltaMerge.Mail(held, [], ["m1"], 2, DeltaMerge.ByReceived, total: 40).Refill);
        Assert.False(DeltaMerge.Mail(held, [], ["m1"], 2, DeltaMerge.ByReceived, total: 1).Refill);
        Assert.False(DeltaMerge.Mail([Mail("m1", 10)], [], ["m1"], 5, DeltaMerge.ByReceived).Refill);
    }

    [Fact]
    public void S2687_a_queued_change_wins_over_a_stale_delta()
    {
        // The delta says unread; the user's mark-read waits in the queue ([S-2622]).
        var (messages, _) = DeltaMerge.Mail([Mail("m1", 10, read: true)], [Mail("m1", 10, read: false)], [], 50, DeltaMerge.ByReceived);
        var pending = new PendingWrite("w", PendingKind.MailMarkRead, "m1", MailFolder.Inbox.Wire(), null, null, Now);

        Assert.True(Assert.Single(MailShelves.Overlay(messages, [pending], MailFolder.Inbox)).IsRead);
    }

    [Fact]
    public void S2687_the_graph_delta_is_bounded_to_the_oldest_held_only_when_the_folder_is_full()
    {
        var full = new[] { Mail("m1", 10), Mail("m2", 20) };

        Assert.Equal(Now.AddMinutes(-20), DeltaMerge.MailFilter(full, 2));
        Assert.Null(DeltaMerge.MailFilter(full, 5));
        Assert.Null(DeltaMerge.MailFilter([Mail("m1", 10) with { Received = null }], 1));

        var url = MailWire.GraphMessagesDelta(MailFolder.Inbox, new DateTimeOffset(2026, 10, 1, 7, 0, 0, TimeSpan.Zero)).AbsoluteUri;
        Assert.Contains("/mailFolders/inbox/messages/delta?", url, StringComparison.Ordinal);
        Assert.Contains("$filter=receivedDateTime%20ge%202026-10-01T07:00:00Z", url, StringComparison.Ordinal);
        Assert.DoesNotContain("$filter", MailWire.GraphMessagesDelta(MailFolder.Inbox, null).AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void S2687_a_folder_cursor_is_for_its_folder_and_count()
    {
        Assert.NotEqual(MailShelves.CursorScope(MailFolder.Inbox, 10), MailShelves.CursorScope(MailFolder.Inbox, 20));
        Assert.NotEqual(MailShelves.CursorScope(MailFolder.Inbox, 10), MailShelves.CursorScope(MailFolder.Sent, 10));
    }

    // ---- [S-2688] CalDAV / CardDAV ------------------------------------------------------------------

    [Fact]
    public void S2688_the_collection_tag_is_the_ctag_else_the_sync_token()
    {
        const string body = """
            <d:multistatus xmlns:d="DAV:" xmlns:cs="http://calendarserver.org/ns/">
              <d:response><d:href>/cal/work/</d:href>
                <d:propstat><d:prop><cs:getctag>"ctag-7"</cs:getctag><d:sync-token>http://sabre.io/ns/sync/12</d:sync-token></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
            </d:multistatus>
            """;
        const string tokenOnly = """
            <d:multistatus xmlns:d="DAV:" xmlns:cs="http://calendarserver.org/ns/">
              <d:response><d:href>/cal/work/</d:href>
                <d:propstat><d:prop><d:sync-token>tok-3</d:sync-token></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
                <d:propstat><d:prop><cs:getctag/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat>
              </d:response>
            </d:multistatus>
            """;

        var (ctag, token) = DavDelta.CollectionTag(body);
        Assert.Equal("\"ctag-7\"", ctag);
        Assert.Equal("http://sabre.io/ns/sync/12", token);
        Assert.Equal("\"ctag-7\"", DavDelta.Tag(ctag, token));

        var (none, only) = DavDelta.CollectionTag(tokenOnly);
        Assert.Null(none);
        Assert.Equal("tok-3", DavDelta.Tag(none, only));
        Assert.Equal((null, null), DavDelta.CollectionTag("<html/>"));
    }

    [Fact]
    public void S2688_a_sync_collection_answer_reads_changed_gone_and_the_next_token()
    {
        const string body = """
            <d:multistatus xmlns:d="DAV:">
              <d:response><d:href>/cal/work/a.ics</d:href>
                <d:propstat><d:prop><d:getetag>"2"</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
              <d:response><d:href>/cal/work/b.ics</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response>
              <d:response><d:href>/cal/work/</d:href>
                <d:propstat><d:prop><d:getetag>"c"</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat>
              </d:response>
              <d:sync-token>http://sabre.io/ns/sync/13</d:sync-token>
            </d:multistatus>
            """;

        var changes = DavDelta.SyncChanges(body);

        Assert.Equal([("/cal/work/a.ics", "\"2\"")], changes.Changed);
        Assert.Equal(["/cal/work/b.ics"], changes.Gone);
        Assert.Equal("http://sabre.io/ns/sync/13", changes.Token);
    }

    [Theory]
    [InlineData(403, true)]
    [InlineData(409, true)]
    [InlineData(507, true)]
    [InlineData(401, false)]
    [InlineData(500, false)]
    public void S2688_a_refused_sync_token_falls_back_to_etags(int status, bool refused)
        => Assert.Equal(refused, DavDelta.TokenRefused(status));

    [Fact]
    public void S2688_an_etag_listing_names_the_new_the_changed_and_the_gone()
    {
        var held = new List<DavTag>
        {
            new() { Href = "/a.ics", Etag = "1", Ids = ["a"] },
            new() { Href = "/b.ics", Etag = "1", Ids = ["b"] },
            new() { Href = "/c.ics", Etag = "1", Ids = ["c"] },
        };

        var (changed, gone) = DeltaMerge.Diff(held, [("/a.ics", "1"), ("/b.ics", "2"), ("/d.ics", "1"), ("/e.ics", "")]);

        Assert.Equal(["/b.ics", "/d.ics", "/e.ics"], changed);
        Assert.Equal(["/c.ics"], gone);
    }

    [Fact]
    public void S2688_resources_read_again_replace_their_items_and_gone_ones_take_theirs()
    {
        var held = new List<DavTag>
        {
            new() { Href = "/a.ics", Etag = "1", Ids = ["a@1", "a@2"] },
            new() { Href = "/b.ics", Etag = "1", Ids = ["b"] },
            new() { Href = "/c.ics", Etag = "1", Ids = ["c"] },
        };

        var (tags, removed) = DeltaMerge.Resources(held, [new DavTag { Href = "/a.ics", Etag = "2", Ids = ["a@1"] }], ["/b.ics"]);

        Assert.Equal(["/c.ics", "/a.ics"], tags.Select(t => t.Href));
        Assert.Equal("2", tags[1].Etag);
        Assert.Equal(["a@2", "b"], removed);
    }

    [Fact]
    public void S2688_an_item_that_moved_to_another_resource_is_not_removed()
    {
        var held = new List<DavTag> { new() { Href = "/old.ics", Etag = "1", Ids = ["uid-1"] } };

        var (_, removed) = DeltaMerge.Resources(held, [new DavTag { Href = "/new.ics", Etag = "1", Ids = ["uid-1"] }], ["/old.ics"]);

        Assert.Empty(removed);
    }

    [Fact]
    public void S2688_query_answers_keep_hrefs_as_written_and_skip_the_collection()
    {
        const string body = """
            <d:multistatus xmlns:d="DAV:" xmlns:c="urn:ietf:params:xml:ns:caldav">
              <d:response><d:href>/cal/work/</d:href><d:propstat><d:prop><d:getetag>"x"</d:getetag></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>/cal/work/a%20b.ics</d:href><d:propstat><d:prop><d:getetag>"1"</d:getetag><c:calendar-data>BEGIN:VCALENDAR
            END:VCALENDAR</c:calendar-data></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>
              <d:response><d:href>/cal/work/gone.ics</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response>
            </d:multistatus>
            """;

        var items = DavDelta.Items(body, DavKind.Calendar);

        Assert.Equal(["/cal/work/a%20b.ics", "/cal/work/gone.ics"], items.Select(i => i.Href));
        Assert.Equal("\"1\"", items[0].Etag);
        Assert.NotNull(items[0].Data);
        Assert.Null(items[1].Data);
    }

    [Fact]
    public void S2688_a_multiget_escapes_hrefs_and_asks_for_the_window_expanded()
    {
        var body = DavDelta.CalendarMultiget(["/cal/a&b.ics", "/cal/<c>.ics"], Now, Now.AddDays(1));

        Assert.Contains("<D:href>/cal/a&amp;b.ics</D:href>", body, StringComparison.Ordinal);
        Assert.Contains("<D:href>/cal/&lt;c&gt;.ics</D:href>", body, StringComparison.Ordinal);
        Assert.Contains("<C:expand start=\"20261011T104200Z\" end=\"20261012T104200Z\"/>", body, StringComparison.Ordinal);
        Assert.NotNull(System.Xml.Linq.XDocument.Parse(body).Root);
        Assert.NotNull(System.Xml.Linq.XDocument.Parse(DavDelta.SyncCollection("http://x/sync?a=1&b=2")).Root);
        Assert.NotNull(System.Xml.Linq.XDocument.Parse(DavDelta.AddressMultiget(["/card/1.vcf"])).Root);
    }

    // ---- [S-2689] IMAP ------------------------------------------------------------------------------

    [Fact]
    public void S2689_no_mark_or_another_incarnation_is_a_full_fetch()
    {
        var mark = new ImapMark { UidValidity = 7, UidNext = 100, Exists = 50, HighestModSeq = 9 };

        Assert.Equal(ImapPlan.Full, ImapDelta.Plan(null, new ImapOpenedState(50, 7, 100, 9)));
        Assert.Equal(ImapPlan.Full, ImapDelta.Plan(mark, new ImapOpenedState(50, 8, 100, 9)));
        Assert.Equal(ImapPlan.Full, ImapDelta.Plan(mark, new ImapOpenedState(50, null, 100, 9)));
        Assert.Equal(ImapPlan.Full, ImapDelta.Plan(mark, new ImapOpenedState(50, 7, null, 9)));
        Assert.Equal(ImapPlan.Full, ImapDelta.Plan(mark, new ImapOpenedState(50, 7, 90, 9)));
    }

    [Fact]
    public void S2689_condstore_unchanged_means_no_fetch_and_anything_else_is_incremental()
    {
        var mark = new ImapMark { UidValidity = 7, UidNext = 100, Exists = 50, HighestModSeq = 9 };

        Assert.Equal(ImapPlan.Unchanged, ImapDelta.Plan(mark, new ImapOpenedState(50, 7, 100, 9)));
        Assert.Equal(ImapPlan.Incremental, ImapDelta.Plan(mark, new ImapOpenedState(50, 7, 100, 10)));
        Assert.Equal(ImapPlan.Incremental, ImapDelta.Plan(mark, new ImapOpenedState(49, 7, 100, 9)));
        Assert.Equal(ImapPlan.Incremental, ImapDelta.Plan(mark, new ImapOpenedState(51, 7, 101, 9)));

        // Without CONDSTORE nothing proves the flags stood: always read them.
        Assert.Equal(ImapPlan.Incremental, ImapDelta.Plan(new ImapMark { UidValidity = 7, UidNext = 100, Exists = 50 }, new ImapOpenedState(50, 7, 100, null)));
    }

    [Fact]
    public void S2689_an_imap_delta_folds_flags_expunges_and_arrivals_by_uid()
    {
        var held = new[] { Imap(30), Imap(20), Imap(10) };

        var (messages, refill) = DeltaMerge.Mail(held, [Imap(30, read: true), Imap(10), Imap(40)], [Imap(20).RawId], 3, DeltaMerge.ByImapUid, total: 3);

        Assert.Equal([40u, 30u, 10u], messages.Select(m => ImapMessageId.Parse(m.RawId)!.Value.Uid));
        Assert.True(messages[1].IsRead);
        Assert.False(refill);
    }

    [Fact]
    public void S2689_uid_sets_merge_runs()
    {
        Assert.Equal("3:5,9,12:13", ImapDelta.UidSet([13, 4, 3, 9, 5, 12, 4]));
        Assert.Equal("7", ImapDelta.UidSet([7]));
        Assert.Null(ImapDelta.UidSet([]));
        Assert.Null(ImapDelta.UidSet([0]));
    }

    [Fact]
    public void S2689_arrivals_are_bounded_below_and_to_the_newest()
    {
        // "UID SEARCH UID 100:*" answers the last message even when its UID is below 100.
        Assert.Equal([105u, 104u], ImapDelta.Arrived([99, 101, 104, 105], 100, 2));
        Assert.Empty(ImapDelta.Arrived([99], 100, 5));
    }

    [Fact]
    public void S2689_reads_uidnext_highestmodseq_capability_and_search()
    {
        Assert.Equal(4392u, ImapDelta.UidNext(Parse("* OK [UIDNEXT 4392] Predicted next UID\r\n")));
        Assert.Null(ImapDelta.UidNext(Parse("* OK [UIDVALIDITY 3857529045] UIDs valid\r\n")));
        Assert.Equal(715194045007ul, ImapDelta.HighestModSeq(Parse("* OK [HIGHESTMODSEQ 715194045007] Highest\r\n")));
        Assert.Null(ImapDelta.HighestModSeq(Parse("* OK [NOMODSEQ] Sorry, this mailbox format doesn't support modsequences\r\n")));
        Assert.True(ImapDelta.HasCapability(Parse("* CAPABILITY IMAP4rev1 UIDPLUS CONDSTORE MOVE\r\n"), "condstore"));
        Assert.False(ImapDelta.HasCapability(Parse("* CAPABILITY IMAP4rev1 UIDPLUS\r\n"), "CONDSTORE"));
        Assert.Equal([2u, 84u, 882u], ImapDelta.Search(Parse("* SEARCH 2 84 882\r\n")));
        Assert.Empty(ImapDelta.Search(Parse("* 3 EXISTS\r\n")));
    }

    [Fact]
    public void S2689_a_mark_needs_validity_and_uidnext()
    {
        Assert.Null(ImapDelta.Mark(new ImapOpenedState(3, null, 5, null)));
        Assert.Null(ImapDelta.Mark(new ImapOpenedState(3, 7, null, null)));

        var mark = ImapDelta.Mark(new ImapOpenedState(3, 7, 5, 11))!;
        Assert.Equal((7u, 5u, 3L, (ulong?)11), (mark.UidValidity, mark.UidNext, mark.Exists, mark.HighestModSeq));
    }
}
