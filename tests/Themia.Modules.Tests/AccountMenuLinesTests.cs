using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-7063] [S-561]..[S-567] The account widgets' shared lines on the properties menu: the kit's
/// Accounts submenu and the calendars' lines.
/// </summary>
public sealed class AccountMenuLinesTests
{
    private int _saves;
    private int _shown;

    private WidgetMenuLines Lines(bool host = true)
        => new(() => _saves++, _ => { }) { ShowAccounts = host ? () => _shown++ : null };

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    // ---- the kit's Accounts submenu ----------------------------------------------------------

    private static readonly KitAccountWords Words = new("No calendar account yet", "Every calendar account");

    private static readonly IReadOnlyList<KitAccountLine> Two = [new("a", "Work", "Microsoft"), new("b", "Home", "Google")];

    private ItemMenuEntry Accounts(IReadOnlyList<KitAccountLine> accounts, List<string> ids)
        => KitMenuLines.Accounts(Lines(), null, accounts, Words, ids, next =>
        {
            ids.Clear();
            ids.AddRange(next);
        });

    [Fact]
    public void S7063_every_account_is_ticked_by_default_then_each_account_then_manage_accounts()
    {
        var lines = Accounts(Two, []).Children!.Where(l => !l.IsSeparator).ToList();

        Assert.Equal(["Every calendar account", "Work", "Home", Str.WidgetsA_Office_ManageAccounts.Text()], lines.Select(l => l.Text));
        Assert.All(lines.Take(3), l => Assert.True(l.Checked));
        Assert.Equal("Microsoft", lines[1].Value);
    }

    [Fact]
    public void S7063_turning_an_account_off_from_every_keeps_the_others_and_the_last_cannot_go()
    {
        var ids = new List<string>();
        Line(Accounts(Two, ids).Children!, "Work").Action!();

        Assert.Equal(["b"], ids);
        Assert.Equal(1, _saves);

        var menu = Accounts(Two, ids);
        Assert.False(Line(menu.Children!, "Every calendar account").Checked);
        Assert.True(Line(menu.Children!, "Home").Disabled);
        Assert.Equal("Home", menu.Value);
    }

    [Fact]
    public void S7063_turning_every_off_starts_from_every_account_there_is_and_on_again_empties_the_choice()
    {
        var ids = new List<string>();
        Line(Accounts(Two, ids).Children!, "Every calendar account").Action!();
        Assert.Equal(["a", "b"], ids);

        Line(Accounts(Two, ids).Children!, "Every calendar account").Action!();
        Assert.Empty(ids);
    }

    [Fact]
    public void S7063_an_account_turned_on_joins_in_the_accounts_order()
        => Assert.Equal(["a", "b"], KitMenuLines.Toggled(["a", "b"], ["b"], "a"));

    [Fact]
    public void S7063_with_no_account_it_says_so_and_offers_to_add_one()
    {
        var lines = Accounts([], []).Children!.Where(l => !l.IsSeparator).ToList();

        Assert.True(lines[0].Note);
        Assert.Equal("No calendar account yet", lines[0].Text);
        lines[1].Action!();
        Assert.Equal(Str.WidgetsA_Office_AddAccount.Text(), lines[1].Text);
        Assert.Equal(1, _shown);
    }

    // ---- the calendars' lines ----------------------------------------------------------------

    private sealed class Office(IReadOnlyList<AccountInfo> accounts, Func<string, IReadOnlyList<CalendarInfo>?> calendars) : IOfficeSource
    {
        public event Action? Changed;

        public IReadOnlyList<AccountInfo> Accounts(FeatureId feature) => accounts;

        public Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation)
            => Task.FromResult(new Synced<IReadOnlyList<CalendarInfo>>(calendars(accountId), calendars(accountId) is null ? SyncProblem.Offline : SyncProblem.None, null, null));

        public Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public void Raise() => Changed?.Invoke();
    }

    private static AccountInfo Account(string id, string name) => new(id, HostProviders.Microsoft, $"{id}@example.com", name, null, null, null);

    private static readonly IReadOnlyList<CalendarInfo> WorkCalendars = [new("cal-1", "Calendar", true, null), new("cal-2", "Team", false, null)];

    private List<ItemMenuEntry> CalendarLines(CalendarConfig config, IOfficeSource? office)
        => [.. OfficeMenuLines.CalendarAccounts(Lines(), config, office)];

    [Fact]
    public void S567_one_account_lists_its_calendars_straight_in_the_calendars_submenu()
    {
        var office = new Office([Account("a", "Work")], _ => WorkCalendars);
        var lines = CalendarLines(new NextMeetingConfigProbe(), office);

        Assert.Equal([Str.WidgetUi_AccountsLabel.Text(), Str.WidgetsA_Calendar_Settings_Calendars.Text()], lines.Select(l => l.Text));
        var calendars = lines[1].Children!;
        Assert.True(Line(calendars, "Calendar").Checked);
        Assert.False(Line(calendars, "Team").Checked);
        Assert.Equal(Str.WidgetsA_Calendar_Settings_DefaultCalendar.Text(), Line(calendars, "Calendar").Value);
        Assert.NotNull(Line(Line(calendars, Str.WidgetsA_Calendar_Settings_Colors.Text()).Children!, "Calendar").Colour);
    }

    [Fact]
    public void S567_turning_a_second_calendar_on_keeps_the_default_and_saves()
    {
        var config = new NextMeetingConfigProbe();
        var office = new Office([Account("a", "Work")], _ => WorkCalendars);

        Line(CalendarLines(config, office)[1].Children!, "Team").Action!();

        Assert.Equal(["cal-1", "cal-2"], config.Calendars["a"]);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S567_several_accounts_get_a_submenu_each_and_a_list_that_failed_says_why()
    {
        var office = new Office([Account("a", "Work"), Account("b", "Home")], id => id == "a" ? WorkCalendars : null);
        var calendars = CalendarLines(new NextMeetingConfigProbe(), office)[1].Children!;

        Assert.Equal(["Work", "Home"], calendars.Select(c => c.Text));
        var failed = Assert.Single(Line(calendars, "Home").Children!);
        Assert.True(failed.Note);
        Assert.Contains(SyncProblems.Message(SyncProblem.Offline, SyncSubject.Calendar, Strings.Current), failed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void S567_no_calendars_line_without_an_office_source_or_an_account()
    {
        Assert.Single(CalendarLines(new NextMeetingConfigProbe(), null));
        Assert.Single(CalendarLines(new NextMeetingConfigProbe(), new Office([], _ => WorkCalendars)));
    }

    [Fact]
    public void S566_without_colours_one_calendar_has_nothing_to_choose_and_several_are_switches_only()
    {
        var one = new Office([Account("a", "Work")], _ => [new CalendarInfo("cal-1", "Calendar", true, null)]);
        Assert.Single(OfficeMenuLines.CalendarAccounts(Lines(), new NextMeetingConfigProbe(), one, colours: false));

        var several = new Office([Account("a", "Work")], _ => WorkCalendars);
        var calendars = OfficeMenuLines.CalendarAccounts(Lines(), new NextMeetingConfigProbe(), several, colours: false).ToList()[1].Children!;
        Assert.Equal(["Calendar", "Team"], calendars.Select(c => c.Text));
        Assert.All(calendars, c => Assert.Null(c.Colour));
    }

    [Fact]
    public void S566_without_colours_an_account_with_one_calendar_has_no_submenu_among_several()
    {
        var office = new Office([Account("a", "Work"), Account("b", "Home")], id => id == "a" ? WorkCalendars : [new CalendarInfo("cal-9", "Calendar", true, null)]);
        var calendars = OfficeMenuLines.CalendarAccounts(Lines(), new NextMeetingConfigProbe(), office, colours: false).ToList()[1].Children!;

        Assert.Equal(["Work"], calendars.Select(c => c.Text));
    }

    [Fact]
    public void S575_birthdays_are_offered_while_a_chosen_calendar_account_has_contacts()
    {
        IReadOnlyList<AccountInfo> accounts = [Account("a", "Work"), new("d", HostProviders.CalDav, "d@example.com", "Dav", null, "https://dav.example.com/", null)];

        Assert.True(OfficeMenuLines.OffersBirthdays([], accounts));
        Assert.True(OfficeMenuLines.OffersBirthdays(["a"], accounts));
        Assert.False(OfficeMenuLines.OffersBirthdays(["d"], accounts));
        Assert.False(OfficeMenuLines.OffersBirthdays([], []));
        Assert.Null(OfficeMenuLines.Birthdays(Lines(), new NextMeetingConfigProbe(), null));
    }

    [Fact]
    public void S566_a_colour_is_stored_lower_case_and_automatic_takes_it_out()
    {
        var colours = OfficeMenuLines.SetColour(new Dictionary<string, string>(), "a", "#AABBCC");
        Assert.Equal("#aabbcc", colours["a"]);
        Assert.Empty(OfficeMenuLines.SetColour(colours, "a", string.Empty));
    }

    /// <summary>A calendar config of no widget's own: the lines are every calendar widget's.</summary>
    private sealed class NextMeetingConfigProbe : CalendarConfig
    {
        public override CalendarStyle Layout => CalendarStyle.Agenda;

        public override WidgetConfig Clone() => CopySharedTo(new NextMeetingConfigProbe());
    }
}
