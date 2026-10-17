using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Accounts;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>Provider mapping, merging, the refresh rule, the screens, the links and the config.</summary>
public class MailFeedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private static MailMessage Message(string account, string id, DateTimeOffset? at, bool read = true)
        => new(account, HostProviders.Microsoft, MailFolder.Inbox, id, "Subject " + id, "Sender", "s@x.com", at, read, string.Empty, string.Empty);

    [Fact]
    public void S545_graph_message_maps_every_field()
    {
        const string json = """
            {"id":"AAMkAD=","subject":"Quarterly numbers","from":{"emailAddress":{"name":"Dana Ruiz","address":"dana@contoso.com"}},
             "receivedDateTime":"2026-10-04T09:30:00Z","isRead":false,"bodyPreview":"Hi all,\r\n  attached are the numbers.","webLink":"https://outlook.office365.com/owa/?ItemID=AAMkAD%3D"}
            """;

        var graph = JsonSerializer.Deserialize(json, MailWireContext.Default.GraphMailMessage)!;
        var message = MailWire.FromGraph(graph, "acc-1", MailFolder.Inbox)!;

        Assert.Equal("AAMkAD=", message.RawId);
        Assert.Equal("Quarterly numbers", message.Subject);
        Assert.Equal("Dana Ruiz", message.FromName);
        Assert.Equal("dana@contoso.com", message.FromAddress);
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 9, 30, 0, TimeSpan.Zero), message.Received);
        Assert.False(message.IsRead);
        Assert.Equal("Hi all, attached are the numbers.", message.Preview);
        Assert.Equal(HostProviders.Microsoft, message.Provider);

        Assert.Null(MailWire.FromGraph(new GraphMailMessage(), "acc-1", MailFolder.Inbox));
        Assert.Equal("(no subject)", MailWire.FromGraph(new GraphMailMessage { Id = "x" }, "a", MailFolder.Inbox)!.SubjectLine);
    }

    [Fact]
    public void S545_gmail_message_maps_headers_labels_and_link()
    {
        const string json = """
            {"id":"18f0","threadId":"18ef","labelIds":["INBOX","UNREAD"],"snippet":"It&#39;s ready &amp; waiting","internalDate":"1790000000000",
             "payload":{"headers":[{"name":"Subject","value":"Your order"},{"name":"From","value":"\"Shop Team\" <team@shop.example>"}]}}
            """;

        var gmail = JsonSerializer.Deserialize(json, MailWireContext.Default.GmailMessage)!;
        var message = MailWire.FromGmail(gmail, "google:1", "me@gmail.com", MailFolder.Inbox)!;

        Assert.Equal("Your order", message.Subject);
        Assert.Equal("Shop Team", message.FromName);
        Assert.Equal("team@shop.example", message.FromAddress);
        Assert.False(message.IsRead);
        Assert.Equal("It's ready & waiting", message.Preview);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_790_000_000_000), message.Received);
        Assert.Equal("https://mail.google.com/mail/?authuser=me%40gmail.com#all/18ef", message.WebLink);

        var read = MailWire.FromGmail(new GmailMessage { Id = "1", InternalDate = "garbage" }, "g", "a@b", MailFolder.Inbox)!;
        Assert.True(read.IsRead);
        Assert.Null(read.Received);
        Assert.Equal(string.Empty, read.WebLink);
    }

    [Fact]
    public void S545_request_addresses()
    {
        Assert.Equal(
            "https://graph.microsoft.com/v1.0/me/mailFolders/junkemail/messages?$top=10&$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink&$orderby=receivedDateTime%20desc",
            MailWire.GraphMessages(MailFolder.Junk, 10).OriginalString);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/messages?labelIds=SENT&maxResults=5", MailWire.GmailList(MailFolder.Sent, 5).OriginalString);
        Assert.Equal("https://gmail.googleapis.com/gmail/v1/users/me/labels/TRASH", MailWire.GmailLabelCounts(MailFolder.Deleted).OriginalString);
        Assert.Equal("{\"destinationId\":\"archive\"}", MailWire.GraphMoveBody(MailFolder.Archive));
    }

    [Fact]
    public void S548_merge_takes_the_newest_n_then_orders_them()
    {
        var a = new[] { Message("a", "a1", Now.AddMinutes(-1)), Message("a", "a2", Now.AddHours(-5)) };
        var b = new[] { Message("b", "b1", Now.AddMinutes(-30)), Message("b", "b2", null), Message("b", "b3", Now.AddDays(-3)) };

        var newest = MailList.Merge([a, b], 3, MailSortOrder.NewestFirst);
        Assert.Equal(["a1", "b1", "a2"], newest.Select(m => m.RawId));

        // Oldest first is the same three, turned round — not the three oldest in the mailbox.
        var oldest = MailList.Merge([a, b], 3, MailSortOrder.OldestFirst);
        Assert.Equal(["a2", "b1", "a1"], oldest.Select(m => m.RawId));

        // Undated sorts last.
        Assert.Equal("b2", MailList.Merge([a, b], 10, MailSortOrder.NewestFirst)[^1].RawId);
        Assert.Empty(MailList.Merge([a, b], 0, MailSortOrder.NewestFirst));
    }

    [Fact]
    public void S548_unread_count()
        => Assert.Equal(2, MailList.Unread([Message("a", "1", Now, read: false), Message("a", "2", Now), Message("a", "3", Now, read: false)]));

    [Fact]
    public void S546_fetch_only_when_the_counts_move_or_the_list_is_old()
    {
        var counts = new MailFolderStatus(100, 3);

        Assert.True(MailRefresh.NeedsFetch(null, counts, null, Now));
        Assert.True(MailRefresh.NeedsFetch(counts, counts, null, Now));
        Assert.False(MailRefresh.NeedsFetch(counts, counts, Now.AddMinutes(-5), Now));
        Assert.True(MailRefresh.NeedsFetch(counts, new MailFolderStatus(101, 4), Now.AddMinutes(-5), Now));
        Assert.True(MailRefresh.NeedsFetch(counts, new MailFolderStatus(100, 2), Now.AddMinutes(-5), Now));
        Assert.True(MailRefresh.NeedsFetch(counts, counts, Now - MailRefresh.FullRefreshAfter, Now));

        // A clock that went backwards: fetch rather than trust a list "from the future".
        Assert.True(MailRefresh.NeedsFetch(counts, counts, Now.AddMinutes(5), Now));
    }

    private static readonly MailAccount Work = new("w", HostProviders.Microsoft, "Work", "me@contoso.com", null);
    private static readonly MailAccount Home = new("h", HostProviders.Imap, "Home", "me@fastmail.com", null);

    private static MailAccountOutcome Ok(string id) => new(id, MailAccountState.Ok, FetchStatus.Ok, null);

    private static MailAccountOutcome Failed(string id, FetchStatus status) => new(id, MailAccountState.Failed, status, "detail");

    [Fact]
    public void S1862_no_account_at_all_is_left_to_the_set_up_button()
    {
        var screen = MailScreens.Decide([], [], [], 0, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.NoAccounts, screen.Kind);
        Assert.False(screen.OffersAccounts);
        Assert.Equal(string.Empty, screen.Text);
    }

    [Fact]
    public void S549_an_empty_choice_reads_every_account_and_a_named_one_only_that()
    {
        Assert.Equal([Work, Home], MailScreens.Targets([Work, Home], []));
        Assert.Equal([Home], MailScreens.Targets([Work, Home], ["h", "gone"]));

        var gone = MailScreens.Decide([Work], ["gone"], [], 0, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.AccountsGone, gone.Kind);
        Assert.True(gone.OffersAccounts);
    }

    [Fact]
    public void S549_needs_sign_in_names_the_account()
    {
        var screen = MailScreens.Decide([Work with { Problem = "revoked" }], [], [], 0, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.SignInRequired, screen.Kind);
        Assert.Equal("Work needs you to sign in again.", screen.Text);
        Assert.True(screen.OffersAccounts);

        // One broken, one fine and listing: the list shows, with a warning naming the broken one.
        var partial = MailScreens.Decide([Work with { Problem = "revoked" }, Home], [], [Ok("h")], 4, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.Messages, partial.Kind);
        Assert.Equal("Work needs you to sign in again.", partial.Warning);
    }

    [Fact]
    public void S549_loading_offline_rate_limited_and_empty()
    {
        Assert.Equal(MailScreenKind.Loading, MailScreens.Decide([Work], [], [], 0, MailFolder.Inbox).Kind);

        var offline = MailScreens.Decide([Work], [], [Failed("w", FetchStatus.Offline)], 0, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.Failed, offline.Kind);
        Assert.StartsWith("No network.", offline.Text, StringComparison.Ordinal);
        Assert.False(offline.OffersAccounts);

        var limited = MailScreens.Decide([Work], [], [Failed("w", FetchStatus.RateLimited)], 0, MailFolder.Inbox);
        Assert.Contains("slow down", limited.Text, StringComparison.Ordinal);

        var signIn = MailScreens.Decide([Work], [], [Failed("w", FetchStatus.NotConfigured)], 0, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.SignInRequired, signIn.Kind);

        var empty = MailScreens.Decide([Work], [], [Ok("w")], 0, MailFolder.Junk);
        Assert.Equal(MailScreenKind.Empty, empty.Kind);
        Assert.Equal("No messages in Junk.", empty.Text);

        // One account still loading while the other is empty: still loading, not "empty".
        Assert.Equal(MailScreenKind.Loading, MailScreens.Decide([Work, Home], [], [Ok("w")], 0, MailFolder.Inbox).Kind);
    }

    [Fact]
    public void S549_a_failed_account_beside_a_working_one_warns_over_the_list()
    {
        var screen = MailScreens.Decide([Work, Home], [], [Ok("w"), Failed("h", FetchStatus.Unreachable)], 3, MailFolder.Inbox);
        Assert.Equal(MailScreenKind.Messages, screen.Kind);
        Assert.Equal("Could not refresh Home: can't reach the mail server.", screen.Warning);
    }

    [Fact]
    public void S548_relative_times()
    {
        var us = Locale.En;

        Assert.Equal(string.Empty, MailTime.Short(null, Now, us));
        Assert.Equal("now", MailTime.Short(Now.AddSeconds(-20), Now, us));
        Assert.Equal("now", MailTime.Short(Now.AddMinutes(3), Now, us));
        Assert.Equal("5m", MailTime.Short(Now.AddMinutes(-5), Now, us));
        Assert.Equal("3h", MailTime.Short(Now.AddHours(-3), Now, us));
        Assert.Equal("2d", MailTime.Short(Now.AddDays(-2), Now, us));
        Assert.Equal("Sep 20", MailTime.Short(new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero), Now, us));

        Assert.Equal("9:05\u202FAM", MailTime.Full(new DateTimeOffset(2026, 10, 4, 9, 5, 0, TimeSpan.Zero), Now, us).Replace(' ', '\u202F'));
        Assert.Equal("Fri", MailTime.Full(new DateTimeOffset(2026, 10, 2, 9, 5, 0, TimeSpan.Zero), Now, us));
        Assert.Equal("Sep 20", MailTime.Full(new DateTimeOffset(2026, 9, 20, 9, 5, 0, TimeSpan.Zero), Now, us));
    }

    /// <summary>[S-548] The same times in German: its compact units, its 24-hour clock, day before month.</summary>
    [Fact]
    public void S548_relative_times_follow_the_language()
    {
        var de = Locale.De;

        Assert.Equal("jetzt", MailTime.Short(Now.AddSeconds(-20), Now, de));
        Assert.Equal("5 Min.", MailTime.Short(Now.AddMinutes(-5), Now, de));
        Assert.Equal("3 Std.", MailTime.Short(Now.AddHours(-3), Now, de));
        Assert.Equal("2 T.", MailTime.Short(Now.AddDays(-2), Now, de));
        Assert.Equal("09:05", MailTime.Full(new DateTimeOffset(2026, 10, 4, 9, 5, 0, TimeSpan.Zero), Now, de));
        Assert.Equal(LocaleFormat.Date(de, new DateOnly(2026, 9, 20), DateStyle.MonthDayShort), MailTime.Short(new DateTimeOffset(2026, 9, 20, 8, 0, 0, TimeSpan.Zero), Now, de));
    }

    /// <summary>[S-548] The screens and warnings in German, with the account name and folder filled in.</summary>
    [Fact]
    public void S548_screens_follow_the_language()
    {
        var empty = MailScreens.Decide(Locale.De, [Work], [], [new MailAccountOutcome("w", MailAccountState.Ok, FetchStatus.Ok, null)], 0, MailFolder.Junk);
        Assert.Equal("Keine Nachrichten in Spam.", empty.Text);
    }

    [Fact]
    public void S547_open_and_reply_links()
    {
        var graph = Message("w", "AAMk/AD=", Now) with { WebLink = "https://outlook.office365.com/owa/?ItemID=1" };

        Assert.Equal("ms-outlook://emails/AAMk%2FAD%3D", MailLinks.Open(graph, outlookInstalled: true));
        Assert.Equal("https://outlook.office365.com/owa/?ItemID=1", MailLinks.Open(graph, outlookInstalled: false));
        Assert.Equal("ms-outlook://emails/AAMk%2FAD%3D/reply", MailLinks.Reply(graph, outlookInstalled: true));

        // issue #45a: IMAP has no web view — nothing to open, never an empty shell call.
        var imap = graph with { Provider = HostProviders.Imap, WebLink = string.Empty, Subject = "Lunch?" };
        Assert.Null(MailLinks.Open(imap, outlookInstalled: true));
        Assert.Equal("mailto:s@x.com?subject=Re%3A%20Lunch%3F", MailLinks.Reply(imap, outlookInstalled: true));
        Assert.Equal("mailto:s@x.com?subject=RE%3A%20x", MailLinks.Reply(imap with { Subject = "RE: x" }, false));

        // A sender address that could smuggle URL syntax gets no reply link at all.
        Assert.Null(MailLinks.Reply(imap with { FromAddress = "a@b.com?cc=evil@x" }, false));
        Assert.Null(MailLinks.Open(imap with { WebLink = "javascript:alert(1)" }, false));
    }

    [Fact]
    public void S558_compose_opens_each_providers_own_new_message()
    {
        // Work or school: Graph's web link is on outlook.office365.com, so is its compose page.
        var work = Message("w", "1", Now) with { WebLink = "https://outlook.office365.com/owa/?ItemID=1" };
        Assert.Equal("https://outlook.office.com/mail/deeplink/compose", MailLinks.Compose(Work, [work]));

        // A personal account: Graph says outlook.live.com, whatever the address's domain.
        var personal = Work with { Address = "me@example.org" };
        Assert.Equal("https://outlook.live.com/mail/0/deeplink/compose", MailLinks.Compose(personal, [work with { WebLink = "https://outlook.live.com/owa/?ItemID=1" }]));

        // Another account's messages say nothing about this one.
        Assert.Equal("https://outlook.office.com/mail/deeplink/compose", MailLinks.Compose(personal, [work with { AccountId = "x", WebLink = "https://outlook.live.com/owa/?ItemID=1" }]));

        // Before any message is held, the Outlook.com domains are personal.
        Assert.Equal("https://outlook.live.com/mail/0/deeplink/compose", MailLinks.Compose(Work with { Address = "me@hotmail.co.uk" }, []));
        Assert.Equal("https://outlook.live.com/mail/0/deeplink/compose", MailLinks.Compose(Work with { Address = "me@outlook.com" }, []));
        Assert.Equal("https://outlook.office.com/mail/deeplink/compose", MailLinks.Compose(Work with { Address = "me@outlook.contoso.com.au" }, []));

        // Gmail: that address's compose window, the address encoded like the message links.
        var gmail = new MailAccount("g", HostProviders.Google, "Personal", "me+x@gmail.com", null);
        Assert.Equal("https://mail.google.com/mail/?authuser=me%2Bx%40gmail.com&view=cm&fs=1", MailLinks.Compose(gmail, []));
        Assert.Equal("https://mail.google.com/mail/?view=cm&fs=1", MailLinks.Compose(gmail with { Address = "not an address" }, []));

        // IMAP: the default mail app, with nobody filled in.
        Assert.Equal("mailto:", MailLinks.Compose(Home, []));

        // Every one of them passes the launcher's gate.
        foreach (var link in new[] { MailLinks.Compose(Work, []), MailLinks.Compose(personal with { Address = "a@live.com" }, []), MailLinks.Compose(gmail, []), MailLinks.Compose(Home, []) })
        {
            Assert.True(MailLinks.IsLaunchable(link), link);
        }
    }

    [Fact]
    public void S559_refresh_says_how_old_the_oldest_list_is()
    {
        Assert.Null(MailScreens.UpdatedAgo(Locale.En, [], Now));
        Assert.Null(MailScreens.UpdatedAgo(Locale.En, [new MailAccountOutcome("w", MailAccountState.Pending, FetchStatus.Loading, null)], Now));

        MailAccountOutcome[] two =
        [
            new("w", MailAccountState.Ok, FetchStatus.Ok, null, At: Now.AddMinutes(-3)),
            new("h", MailAccountState.Failed, FetchStatus.Offline, null, At: Now.AddMinutes(-12)),
        ];
        Assert.Equal("12 min. ago", MailScreens.UpdatedAgo(Locale.En, two, Now));
        Assert.Equal("vor 12 Min.", MailScreens.UpdatedAgo(Locale.De, two, Now));

        // Under a minute, and a provider clock running ahead, are both "now", never "in 2 min.".
        Assert.Equal("now", MailScreens.UpdatedAgo(Locale.En, [two[0] with { At = Now.AddSeconds(-20) }], Now));
        Assert.Equal("now", MailScreens.UpdatedAgo(Locale.En, [two[0] with { At = Now.AddMinutes(2) }], Now));
    }

    [Theory]
    [InlineData("mailto:", true)]
    [InlineData("mailto:?subject=x", false)]
    [InlineData("https://mail.google.com/mail/#all/1", true)]
    [InlineData("ms-outlook://emails/AAMk%2FAD%3D", true)]
    [InlineData("ms-outlook://emails/AAMk%2FAD%3D/reply", true)]
    [InlineData("mailto:a@b.com?subject=Re%3A%20hi", true)]
    [InlineData("ms-outlook://emails/a b", false)]
    [InlineData("ms-outlook://settings", false)]
    [InlineData("mailto:a@b.com?subject=x&cc=evil@x", false)]
    [InlineData("http://example.com", false)]
    [InlineData("file:///C:/Windows/notepad.exe", false)]
    [InlineData("C:\\Windows\\notepad.exe", false)]
    [InlineData("", false)]
    public void S547_only_the_three_link_shapes_are_launchable(string link, bool expected)
        => Assert.Equal(expected, MailLinks.IsLaunchable(link));

    [Fact]
    public void S6403_the_preview_is_the_list_styles_own_line_and_the_fields_are_subject_and_account()
    {
        var config = new EmailConfig();
        Assert.True(config.Shows(MailListField.Subject));
        Assert.True(config.ShowsPreview);
        Assert.True(config.Shows(MailListField.Account));

        config.Style = EmailStyle.Compact;
        Assert.False(config.ShowsPreview);
        Assert.True(config.Shows(MailListField.Account));

        config.Style = EmailStyle.Table;
        Assert.False(config.ShowsPreview);

        config.Style = EmailStyle.List;
        config.ListFields = [MailListField.Subject];
        Assert.False(config.Shows(MailListField.Account));
        Assert.Equal(EmailConfig.DefaultColumns, config.EffectiveColumns);
    }

    [Theory]
    [InlineData(EmailStyle.List, new[] { EmailSetting.ListFields, EmailSetting.PreviewLines })]
    [InlineData(EmailStyle.Compact, new[] { EmailSetting.ListFields })]
    [InlineData(EmailStyle.Table, new[] { EmailSetting.Columns })]
    public void S6403_the_page_offers_only_what_the_style_draws_with(EmailStyle style, EmailSetting[] expected)
        => Assert.Equal(expected, new EmailConfig { Style = style }.ShownSettings);

    [Fact]
    public void S6403_an_old_preview_field_in_a_hand_edited_file_is_dropped_not_refused()
    {
        var document = StateWithMail("""{"kind":"email","listFields":["subject","preview","account"]}""");

        var config = MailOf(AppStateStore.Parse(document.ToJsonString()).State!);

        Assert.Equal([MailListField.Subject, MailListField.Account], config.ListFields);
    }

    /// <summary>A state document holding one Mail widget whose config is <paramref name="configJson"/>, plus a clock.</summary>
    private static JsonNode StateWithMail(string configJson)
    {
        var state = AppState.CreateDefault();
        state.Views[0].Widgets.Add(WidgetInstance.Create(EmailConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2)));
        state.Views[0].Widgets.Add(WidgetInstance.Create(ClockConfig.Descriptor.Id, new FracRect(0.5, 0.1, 0.2, 0.2)));

        var document = JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        var mail = document["views"]![0]!["widgets"]!.AsArray().First(w => w!["config"]!["kind"]!.GetValue<string>() == EmailConfig.Descriptor.Id.Value)!;
        mail["config"] = JsonNode.Parse(configJson);
        return document;
    }

    private static EmailConfig MailOf(AppState state)
        => Assert.IsType<EmailConfig>(state.Views[0].Widgets.First(w => w.Kind == EmailConfig.Descriptor.Id).Config);

    [Fact]
    public void S540_config_round_trips_under_its_wire_names()
    {
        var state = AppState.CreateDefault();
        var widget = WidgetInstance.Create(EmailConfig.Descriptor.Id, new FracRect(0.1, 0.1, 0.2, 0.2));
        widget.Config = new EmailConfig
        {
            AccountIds = ["w"],
            Folder = MailFolder.Junk,
            Style = EmailStyle.Table,
            SortOrder = MailSortOrder.OldestFirst,
            ListFields = [MailListField.Account],
            RowColumns = [MailColumn.Date, MailColumn.From],
        };
        state.Views[0].Widgets.Add(widget);

        var json = SettingsTransfer.Serialize(state);

        Assert.Contains("\"folder\": \"junkemail\"", json, StringComparison.Ordinal);
        Assert.Contains("\"style\": \"table\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sortOrder\": \"asc\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("collectionId", json, StringComparison.Ordinal);

        var parsed = AppStateStore.Parse(json);
        var back = MailOf(parsed.State!);
        Assert.Equal(MailFolder.Junk, back.Folder);
        Assert.Equal([MailColumn.Date, MailColumn.From], back.RowColumns);
        Assert.Equal([MailListField.Account], back.ListFields);
        Assert.Equal(EmailStyle.Table, back.Style);
        Assert.Equal(["w"], back.AccountIds);
        Assert.DoesNotContain(parsed.Repairs, r => r.Contains("mail", StringComparison.Ordinal));
    }

    [Fact]
    public void S540_unknown_values_fall_back_instead_of_refusing_the_file()
    {
        var document = StateWithMail("""
            {"kind":"email","accountIds":"not-a-list","folder":"nonsense","style":7,"sortOrder":"sideways",
             "listFields":["subject","bogus"],"rowColumns":[],"previewLines":0}
            """);

        var parsed = AppStateStore.Parse(document.ToJsonString());

        Assert.NotNull(parsed.State);
        Assert.Equal(AppState.CreateDefault().Views[0].Widgets.Count + 2, parsed.State!.Views[0].Widgets.Count);
        var config = MailOf(parsed.State);
        Assert.Equal(MailFolder.Inbox, config.Folder);
        Assert.Equal(EmailStyle.List, config.Style);
        Assert.Equal(MailSortOrder.NewestFirst, config.SortOrder);
        Assert.Equal([MailListField.Subject], config.ListFields);
        Assert.Null(config.RowColumns);
        Assert.Empty(config.AccountIds!);
        Assert.Equal(1, config.PreviewLines);
        Assert.Contains(parsed.Repairs, r => r.Contains("mail", StringComparison.Ordinal));
    }
}

/// <summary>[S-556] Shedding detail to fit the tile.</summary>
public class MailLayoutTests
{
    [Fact]
    public void S556_rows_shed_account_then_preview_on_a_small_tile()
    {
        var config = new EmailConfig { PreviewLines = 3 };

        var large = MailLayout.Rows(config, 560, 380);
        Assert.Equal(new MailRowLayout(true, true, 3, true, true, Envelope: true), large);

        // Narrow: the account tag goes, the rest stays.
        Assert.False(MailLayout.Rows(config, 200, 380).Account);
        Assert.True(MailLayout.Rows(config, 200, 380).Preview);

        // Short: one preview line, then none.
        Assert.Equal(1, MailLayout.Rows(config, 480, 120).PreviewLines);
        Assert.False(MailLayout.Rows(config, 120, 80).Preview);

        // Too narrow for a time beside the sender.
        Assert.False(MailLayout.Rows(config, 100, 80).Time);

        // Compact view never previews.
        config.Style = EmailStyle.Compact;
        Assert.False(MailLayout.Rows(config, 560, 380).Preview);
    }

    [Fact]
    public void S556_row_view_drops_the_least_essential_columns_first()
    {
        MailColumn[] all = [MailColumn.From, MailColumn.Subject, MailColumn.Date, MailColumn.Preview, MailColumn.Account];

        Assert.Equal(all, MailLayout.Columns(all, 600, 8).Select(c => c.Column));
        Assert.Equal([MailColumn.From, MailColumn.Subject, MailColumn.Date], MailLayout.Columns(all, 260, 8).Select(c => c.Column));
        Assert.Equal([MailColumn.From], MailLayout.Columns(all, 50, 8).Select(c => c.Column));

        // Too narrow for a subject: the date still fits beside the sender, so it stays.
        Assert.Equal([MailColumn.From, MailColumn.Date], MailLayout.Columns(all, 168, 8).Select(c => c.Column));

        // The user's order is kept; the flexible column takes the slack.
        var fits = MailLayout.Columns([MailColumn.Date, MailColumn.Subject], 300, 8);
        Assert.Equal([MailColumn.Date, MailColumn.Subject], fits.Select(c => c.Column));
        Assert.Equal(1, fits[1].Star);

        // With no flexible column the first one stretches so the row still fills.
        Assert.Equal(1, MailLayout.Columns([MailColumn.Date, MailColumn.Account], 400, 8)[0].Star);
    }
}
