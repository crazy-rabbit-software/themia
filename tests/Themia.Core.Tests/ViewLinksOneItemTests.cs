using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11965] [S-11966] One item, one light (owner, 2026-11-19: "when hovering over an email it highlights multiple
/// emails. it should only highglight exactly the same item, not all from the sender"). In the widget the pointer is
/// on, the part under it alone is lit (`LinkPartsState`, what `LinkLights` draws); in the other widgets of the view, the
/// part that is that same item; a part is keyed by what it is (a mail by its message), never by what it carries.
/// </summary>
public sealed class ViewLinksOneItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Two mails from Ana, one from Ben, in one account; the first id again in another account.</summary>
    private static readonly MailMessage AnaFirst = Mail("work", "m1", "Ana Silva", "ana@lumen.example");
    private static readonly MailMessage AnaSecond = Mail("work", "m2", "Ana Silva", "ana@lumen.example");
    private static readonly MailMessage Ben = Mail("work", "m3", "Ben Ode", "ben@lumen.example");
    private static readonly MailMessage AnaFirstAtHome = Mail("home", "m1", "Ana Silva", "ana@lumen.example");

    private static MailMessage Mail(string account, string id, string name, string address)
        => new(account, HostProviders.Microsoft, MailFolder.Inbox, id, "Hello", name, address, Now, false, "Preview", string.Empty);

    /// <summary>A mail widget's rows on <paramref name="board"/>, each keyed as the mail widget keys it.</summary>
    private static (LinkPartsState<object, MessageLink> State, Dictionary<MailMessage, object> Rows) MailWidget(LinkBoard board, params MailMessage[] messages)
    {
        var member = board.Join();
        var state = new LinkPartsState<object, MessageLink>(() => member.Get(LinkTopics.HoveredMessage), value => member.Set(LinkTopics.HoveredMessage, value));
        member.Watch(LinkTopics.HoveredMessage, _ => state.Heard());
        var rows = new Dictionary<MailMessage, object>();
        foreach (var message in messages)
        {
            var row = new object();
            rows[message] = row;
            state.Set(row, LinkKeys.Message(message.AccountId, message.RawId));
        }

        return (state, rows);
    }

    [Fact]
    public void S11965_hovering_one_of_two_mails_from_the_same_sender_lights_exactly_one_row()
    {
        var board = new LinkBoard();
        var (mail, rows) = MailWidget(board, AnaFirst, AnaSecond, Ben);

        Assert.True(mail.Enter(rows[AnaFirst]));

        Assert.Equal(LinkLight.Pointed, mail.Light(rows[AnaFirst]));
        Assert.Equal(LinkLight.None, mail.Light(rows[AnaSecond]));
        Assert.Equal(LinkLight.None, mail.Light(rows[Ben]));
        Assert.Equal(LinkKeys.Message("work", "m1"), board.Peek(LinkTopics.HoveredMessage));

        mail.Leave(rows[AnaFirst]);

        Assert.All(rows.Values, row => Assert.Equal(LinkLight.None, mail.Light(row)));
        Assert.Null(board.Peek(LinkTopics.HoveredMessage));
    }

    [Fact]
    public void S11965_a_linked_widget_lights_only_the_identical_item()
    {
        var board = new LinkBoard();
        var (inbox, inboxRows) = MailWidget(board, AnaFirst, AnaSecond, Ben);
        var (unread, unreadRows) = MailWidget(board, AnaSecond, AnaFirst, AnaFirstAtHome);

        inbox.Enter(inboxRows[AnaFirst]);

        // The same message in the other mail widget lights; Ana's other mail and the same raw id in another account do not.
        Assert.Equal(LinkLight.Pointed, unread.Light(unreadRows[AnaFirst]));
        Assert.Equal(LinkLight.None, unread.Light(unreadRows[AnaSecond]));
        Assert.Equal(LinkLight.None, unread.Light(unreadRows[AnaFirstAtHome]));
    }

    [Fact]
    public void S11965_in_the_widget_under_the_pointer_a_part_that_carries_the_same_value_stays_dark()
    {
        // Two parts with one value in one widget (a multi-day event's two rows, two shortcuts' rows): the one under the
        // pointer alone; another widget lights every part that is the value.
        var board = new LinkBoard();
        var member = board.Join();
        var state = new LinkPartsState<object, EventLink>(() => member.Get(LinkTopics.HoveredEvent), value => member.Set(LinkTopics.HoveredEvent, value));
        var monday = new object();
        var tuesday = new object();
        var trip = new EventLink("work", null, "trip");
        state.Set(monday, trip);
        state.Set(tuesday, trip);

        state.Enter(monday);

        Assert.Equal(LinkLight.Pointed, state.Light(monday));
        Assert.Equal(LinkLight.None, state.Light(tuesday));

        // The pointer moves onto the other one before it is told it left the first: the new one is lit, and the late
        // leave of the first takes nothing away.
        state.Enter(tuesday);
        state.Leave(monday);

        Assert.Equal(LinkLight.None, state.Light(monday));
        Assert.Equal(LinkLight.Pointed, state.Light(tuesday));
        Assert.Equal(trip, board.Peek(LinkTopics.HoveredEvent));

        var other = board.Join();
        var echo = new LinkPartsState<object, EventLink>(() => other.Get(LinkTopics.HoveredEvent), value => other.Set(LinkTopics.HoveredEvent, value));
        var first = new object();
        var second = new object();
        echo.Set(first, trip);
        echo.Set(second, trip);

        Assert.Equal(LinkLight.Pointed, echo.Light(first));
        Assert.Equal(LinkLight.Pointed, echo.Light(second));
    }

    [Fact]
    public void S11965_the_keyboard_cursor_publishes_and_lights_nothing_in_its_own_widget()
    {
        var board = new LinkBoard();
        var (mail, rows) = MailWidget(board, AnaFirst, AnaSecond);
        var (other, otherRows) = MailWidget(board, AnaFirst);

        mail.Point(LinkKeys.Message("work", "m1"));

        Assert.All(rows.Values, row => Assert.Equal(LinkLight.None, mail.Light(row)));
        Assert.Equal(LinkLight.Pointed, other.Light(otherRows[AnaFirst]));

        mail.Point(null);

        Assert.Equal(LinkLight.None, other.Light(otherRows[AnaFirst]));
    }

    [Fact]
    public void S11965_a_mark_under_the_pointer_stays_whole_and_every_other_goes_to_six_tenths()
    {
        var board = new LinkBoard();
        var member = board.Join();
        var cells = new LinkPartsState<object, DateLink>(() => member.Get(LinkTopics.HoveredDate), value => member.Set(LinkTopics.HoveredDate, value));
        var day = new DateLink(new DateOnly(2026, 11, 19));
        var under = new object();
        var same = new object();
        var other = new object();
        cells.Set(under, day);
        cells.Set(same, day);
        cells.Set(other, new DateLink(new DateOnly(2026, 11, 20)));

        Assert.Equal(1, cells.Strength(under));

        cells.Enter(under);

        Assert.Equal(1, cells.Strength(under));
        Assert.Equal(FamilyShape.LinkPointed, cells.Strength(same));
        Assert.Equal(FamilyShape.LinkPointed, cells.Strength(other));
    }

    [Fact]
    public void S11965_a_pick_lights_every_part_that_is_it_and_wins_over_the_pointer()
    {
        var board = new LinkBoard();
        var member = board.Join();
        var apps = new LinkPartsState<object, AppLink>(() => member.Get(LinkTopics.HoveredApp), value => member.Set(LinkTopics.HoveredApp, value));
        var code = new object();
        var edge = new object();
        apps.Set(code, new AppLink("code"));
        apps.Set(edge, new AppLink("msedge"));
        apps.Picked = new AppLink("msedge");

        apps.Enter(edge);

        Assert.Equal(LinkLight.Picked, apps.Light(edge));
        Assert.Equal(LinkLight.None, apps.Light(code));

        // A pick the widget does not show dims nothing ([S-11900]).
        apps.Picked = new AppLink("spotify");
        apps.Leave(edge);

        Assert.Equal(1, apps.Strength(code));
    }

    [Fact]
    public void S11965_a_part_forgotten_under_the_pointer_lights_nothing_and_a_part_of_nothing_never_publishes()
    {
        var board = new LinkBoard();
        var (mail, rows) = MailWidget(board, AnaFirst, AnaSecond);
        var blank = new object();
        mail.Set(blank, null);

        Assert.False(mail.Enter(blank));
        Assert.Null(board.Peek(LinkTopics.HoveredMessage));

        mail.Enter(rows[AnaFirst]);
        mail.Forget(rows[AnaFirst]);

        Assert.Equal(LinkLight.None, mail.Light(rows[AnaSecond]));
        Assert.False(mail.Leave(rows[AnaFirst]));
    }

    [Fact]
    public void S11966_a_message_is_its_account_and_id_never_its_sender()
    {
        Assert.NotEqual(LinkKeys.Message(AnaFirst.AccountId, AnaFirst.RawId), LinkKeys.Message(AnaSecond.AccountId, AnaSecond.RawId));
        Assert.NotEqual(LinkKeys.Message(AnaFirst.AccountId, AnaFirst.RawId), LinkKeys.Message(AnaFirstAtHome.AccountId, AnaFirstAtHome.RawId));
        Assert.Equal(new MessageLink("work", "m1"), LinkKeys.Message("work", "m1"));
        Assert.Null(LinkKeys.Message("work", " "));
        Assert.Null(LinkKeys.Message(null, "m1"));
    }

    [Theory]
    [InlineData(@"C:\", "C:")]
    [InlineData("c:", "C:")]
    [InlineData("D:/", "D:")]
    [InlineData(@"C:\Users", null)]
    [InlineData(@"C:\Users\", null)]
    [InlineData(@"\\server\share", null)]
    [InlineData("", null)]
    public void S11965_a_folder_is_its_drive_only_at_the_drives_root(string path, string? drive)
        => Assert.Equal(drive is null ? null : new DriveLink(drive), LinkKeys.DriveRootLink(path));

    [Fact]
    public void S11965_one_row_per_process_or_per_audio_session_is_no_app()
    {
        ProcessSample[] samples =
        [
            new("chrome", 0.2, 100, 1, new ProcessIdentity(10, 1)),
            new("chrome", 0.1, 90, 1, new ProcessIdentity(11, 1)),
        ];

        Assert.All(ProcessTable.Rows(Localization.Locale.En, samples, ProcessSort.Cpu, 0, _ => @"C:\chrome.exe", grouped: false), row => Assert.Null(row.App));
        Assert.Equal("chrome", Assert.Single(ProcessTable.Rows(Localization.Locale.En, ProcessTable.Rank(samples, ProcessSort.Cpu, group: true), ProcessSort.Cpu, 0, _ => @"C:\chrome.exe", grouped: true)).App);

        var session = new MixerRow("{0.0.0}|chrome.exe%b{1}", "Chrome", @"C:\chrome.exe", 1, false, true, false, 1);
        Assert.Null(MixerRules.AppKey(session, grouped: false));
        Assert.Equal("chrome", MixerRules.AppKey(session, grouped: true));
    }
}
