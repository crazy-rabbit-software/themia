using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3800]..[S-3823] Rules the settings pages had and the edit bar's menus kept: reordering by
/// keyboard, a view's colour, and the synced-item numbers ⚙ › Accounts shows.
/// </summary>
public sealed class AppPagesTests
{
    // ---- [S-3802] reorder by keyboard ------------------------------------------------------------

    [Theory]
    [InlineData(ReorderArrow.Up, true, true, false, -1)]
    [InlineData(ReorderArrow.Down, true, true, false, 1)]
    [InlineData(ReorderArrow.Up, false, true, false, 0)]   // Shift+Up selects
    [InlineData(ReorderArrow.Up, true, false, false, 0)]   // Alt+Up is somebody else's
    [InlineData(ReorderArrow.Up, false, false, false, 0)]  // an arrow alone moves the focus
    [InlineData(ReorderArrow.Down, true, true, true, 0)]   // with Ctrl it is not the shortcut
    [InlineData(ReorderArrow.Left, true, true, false, 0)]
    public void S3802_alt_shift_up_and_down_move_the_focused_item(ReorderArrow arrow, bool alt, bool shift, bool ctrl, int delta)
        => Assert.Equal(delta, ReorderKeys.Delta(arrow, alt, shift, ctrl));

    [Theory]
    [InlineData(ReorderArrow.Up, false, false, true, -1)]
    [InlineData(ReorderArrow.Down, false, false, true, 1)]
    [InlineData(ReorderArrow.Up, true, true, false, -1)]   // the Windows lists' shortcut too
    [InlineData(ReorderArrow.Down, false, true, true, 0)]  // Ctrl+Shift is somebody else's
    [InlineData(ReorderArrow.Down, true, false, true, 0)]
    [InlineData(ReorderArrow.Up, false, false, false, 0)]  // an arrow alone moves the highlight
    [InlineData(ReorderArrow.Left, false, false, true, 0)]
    public void S3748_ctrl_up_and_down_move_a_menu_list_row(ReorderArrow arrow, bool alt, bool shift, bool ctrl, int delta)
        => Assert.Equal(delta, ReorderKeys.MenuDelta(arrow, alt, shift, ctrl));

    // ---- [S-3810] / [S-3811] a view's colour -------------------------------------------------------

    [Theory]
    [InlineData("#3366cc", "#3366CC")]
    [InlineData("36c", "#3366CC")]
    [InlineData(" #ABCDEF ", "#ABCDEF")]
    public void S3810_a_view_colour_is_stored_as_rrggbb(string typed, string stored)
    {
        var state = AppState.CreateDefault();
        var id = state.Views[0].Id;

        Assert.True(ViewOps.SetColor(state, id, typed));
        Assert.Equal(stored, state.Views[0].Color);
    }

    [Fact]
    public void S3810_none_clears_the_colour_and_a_bad_one_is_refused()
    {
        var state = AppState.CreateDefault();
        var id = state.Views[0].Id;
        ViewOps.SetColor(state, id, "#112233");

        Assert.False(ViewOps.SetColor(state, id, "red"));
        Assert.Equal("#112233", state.Views[0].Color);
        Assert.False(ViewOps.SetColor(state, id, "#112233"));
        Assert.False(ViewOps.SetColor(state, "no such view", "#112233"));

        Assert.True(ViewOps.SetColor(state, id, string.Empty));
        Assert.Null(state.Views[0].Color);
    }

    [Fact]
    public void S3810_a_duplicate_and_an_undo_keep_the_colour()
    {
        var state = AppState.CreateDefault();
        var source = state.Views[0];
        ViewOps.SetColor(state, source.Id, "#AA5500");

        Assert.Equal("#AA5500", ViewOps.Duplicate(state, source.Id)!.Color);

        var before = LayoutShape.Capture(state);
        ViewOps.SetColor(state, source.Id, null);
        Assert.False(before.SameLayout(LayoutShape.Capture(state)));

        before.RestoreInto(state);
        Assert.Equal("#AA5500", state.Views[0].Color);
    }

    [Theory]
    [InlineData("\"red\"")]
    [InlineData("\"#12345\"")]
    [InlineData("\"#GGHHII\"")]
    public void S3811_a_stored_colour_that_is_not_a_colour_is_dropped_loudly(string color)
    {
        var state = AppState.CreateDefault();
        var node = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState))!;
        node["views"]![0]!["color"] = System.Text.Json.Nodes.JsonNode.Parse(color);
        var json = node.ToJsonString();

        var result = AppStateStore.Parse(json);

        Assert.Null(result.State!.Views[0].Color);
        Assert.Contains(result.Repairs, r => r.Contains("colour", StringComparison.Ordinal));
    }

    [Fact]
    public void S3811_a_stored_colour_is_normalised_quietly()
    {
        var state = AppState.CreateDefault();
        state.Views[0].Color = "#abc";
        var json = System.Text.Json.JsonSerializer.Serialize(state, StateJson.AppState);

        var result = AppStateStore.Parse(json);

        Assert.Equal("#AABBCC", result.State!.Views[0].Color);
        Assert.DoesNotContain(result.Repairs, r => r.Contains("colour", StringComparison.Ordinal));
    }

    // ---- [S-3820]..[S-3823] synced items per account ----------------------------------------------

    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static PendingWrite Write(string id) => new(id, PendingKind.MailMarkRead, "raw", "inbox", null, null, T0);

    [Fact]
    public void S3820_mail_counts_messages_over_folders_and_the_queue()
    {
        var shelf = new MailShelf
        {
            Owner = "a",
            Folders =
            [
                new MailFolderShelf { At = T0, Messages = [new StoredMail(), new StoredMail()] },
                new MailFolderShelf { At = T0.AddMinutes(5), Messages = [new StoredMail()] },
            ],
            Pending = [Write("1"), Write("2")],
        };

        var stats = SyncStats.Of(shelf);

        Assert.Equal(new ShelfStats(SyncKind.Mail, 3, 2, T0.AddMinutes(5), 2), stats);
    }

    [Fact]
    public void S3820_calendar_counts_events_over_windows_and_reads_the_newest_refresh()
    {
        var shelf = new CalendarShelf
        {
            Owner = "a",
            Calendars = [new StoredCalendar(), new StoredCalendar(), new StoredCalendar()],
            CalendarsAt = T0,
            Spans =
            [
                new CalendarSpan { NearAt = T0.AddHours(1), Events = [new StoredEvent(), new StoredEvent()] },
                new CalendarSpan { NearAt = T0.AddHours(2), Events = [new StoredEvent()] },
            ],
            Birthdays = [new StoredBirthday()],
        };

        Assert.Equal(new ShelfStats(SyncKind.Calendar, 3, 3, T0.AddHours(2), 0), SyncStats.Of(shelf));
    }

    [Fact]
    public void S3820_an_empty_shelf_has_never_synced()
    {
        Assert.Equal(new ShelfStats(SyncKind.Calendar, 0, 0, null, 0), SyncStats.Of(new CalendarShelf { Owner = "a" }));
        Assert.Equal(new ShelfStats(SyncKind.Contacts, 0, 0, null, 0), SyncStats.Of(new ContactsShelf { Owner = "a" }));
        Assert.Equal(new ShelfStats(SyncKind.Mail, 0, 0, null, 0), SyncStats.Of(new MailShelf { Owner = "a" }));
    }

    [Fact]
    public void S3820_tasks_count_tasks_over_lists_and_the_queue()
    {
        var shelf = new TasksShelf
        {
            Owner = "a",
            Lists = [new StoredTaskList(), new StoredTaskList()],
            ListsAt = T0.AddDays(1),
            Held = [new TaskListShelf { At = T0, Tasks = [new StoredTask(), new StoredTask(), new StoredTask()] }],
            Pending = [Write("1")],
        };

        Assert.Equal(new ShelfStats(SyncKind.Tasks, 3, 2, T0.AddDays(1), 1), SyncStats.Of(shelf));
    }

    [Fact]
    public void S3821_an_account_lists_the_kinds_its_provider_feeds_in_one_order()
    {
        var mail = new ShelfStats(SyncKind.Mail, 10, 1, T0, 0);
        var tasks = new ShelfStats(SyncKind.Tasks, 4, 1, T0, 2);

        var microsoft = SyncStats.Summarize(HostProviders.Microsoft.Features(), [tasks, mail]);
        Assert.Equal([SyncKind.Mail, SyncKind.Calendar, SyncKind.Contacts, SyncKind.Tasks], microsoft.Select(r => r.Kind));
        Assert.Equal(mail, microsoft[0].Stats);
        Assert.Null(microsoft[1].Stats);
        Assert.Equal(2, SyncStats.PendingTotal(microsoft));

        // A stray shelf of a kind the provider does not feed is never listed.
        var imap = SyncStats.Summarize(HostProviders.Imap.Features(), [mail, tasks]);
        Assert.Equal([SyncKind.Mail], imap.Select(r => r.Kind));
        Assert.Equal(0, SyncStats.PendingTotal(imap));
    }

    [Fact]
    public void S3823_the_ledger_keeps_the_latest_numbers_per_owner_and_kind()
    {
        var ledger = new SyncLedger();
        var first = new ShelfStats(SyncKind.Mail, 1, 1, T0, 0);
        var later = first with { Items = 2 };

        Assert.True(ledger.Record("a", first));
        Assert.False(ledger.Record("a", first));
        Assert.True(ledger.Record("a", later));
        Assert.True(ledger.Record("b", first));
        Assert.True(ledger.Record("a", new ShelfStats(SyncKind.Contacts, 5, 0, T0, 0)));

        Assert.Equal(2, ledger.For("a").Count);
        Assert.Contains(later, ledger.For("a"));

        Assert.True(ledger.Forget("a"));
        Assert.Empty(ledger.For("a"));
        Assert.False(ledger.Forget("a"));
        Assert.Single(ledger.For("b"));
    }
}
