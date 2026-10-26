using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>[S-7513] The calendar's options on its properties menu ([S-3719]): what each style offers under Style, and what a line does.</summary>
public sealed class CalendarMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    /// <summary>A Microsoft account (calendars and contacts) and a CalDAV one (a calendar alone).</summary>
    private static readonly IReadOnlyList<AccountInfo> Accounts =
    [
        new("work", HostProviders.Microsoft, "work@example.com", "Work", null, null, null),
        new("dav", HostProviders.CalDav, "dav@example.com", "Dav", null, "https://dav.example.com/", null),
    ];

    private WidgetMenuLines Build(CalendarWidgetConfig config, IOfficeSource? office = null)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        CalendarMenu.Lines(lines, config, office ?? new Office(Accounts));
        return lines;
    }

    /// <summary>The accounts, and no calendar lists yet (the Calendars line says it is looking).</summary>
    private sealed class Office(IReadOnlyList<AccountInfo> accounts) : IOfficeSource
    {
        public event Action? Changed { add { } remove { } }

        public IReadOnlyList<AccountInfo> Accounts(FeatureId feature) => accounts;

        public Task<Synced<IReadOnlyList<CalendarEvent>>> EventsAsync(CalendarTarget target, DateTimeOffset from, DateTimeOffset to, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<CalendarInfo>>> CalendarsAsync(string accountId, bool force, CancellationToken cancellation)
            => new TaskCompletionSource<Synced<IReadOnlyList<CalendarInfo>>>().Task;

        public Task<Synced<IReadOnlyList<Birthday>>> BirthdaysAsync(string accountId, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();

        public Task<Synced<IReadOnlyList<Contact>>> ContactsAsync(string accountId, int max, bool force, CancellationToken cancellation)
            => throw new NotSupportedException();
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    private static string Range => CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Label.Text();

    [Theory]
    [InlineData(CalendarStyle.Month)]
    [InlineData(CalendarStyle.Day)]
    [InlineData(CalendarStyle.Year)]
    public void S7513_a_month_a_day_and_a_year_offer_nothing_under_style(CalendarStyle style)
        => Assert.Empty(Build(new CalendarWidgetConfig { Style = style }).StyleLines);

    [Fact]
    public void S7513_the_week_offers_its_first_column_and_its_days_and_the_agenda_its_range_and_length()
    {
        Assert.Equal(
            [CalendarStr.WidgetsA_Calendar_Settings_WeekFrom_Label.Text(), CalendarStr.WidgetsA_Calendar_Settings_Days_Label.Text()],
            Build(new CalendarWidgetConfig { Style = CalendarStyle.Week }).StyleLines.Select(l => l.Text));
        Assert.Equal(
            [Range, CalendarStr.Calendar_AgendaEvents_Label.Text()],
            Build(new CalendarWidgetConfig { Style = CalendarStyle.Agenda }).StyleLines.Select(l => l.Text));
    }

    [Fact]
    public void S7513_the_first_level_is_appearance_the_accounts_and_calendars_then_after_a_divider_birthdays()
    {
        var lines = Build(new CalendarWidgetConfig());

        Assert.Equal(
            [Str.WidgetUi_AccountsLabel.Text(), Str.WidgetsA_Calendar_Settings_Calendars.Text(), string.Empty, Str.Calendar_ShowBirthdays_Label.Text()],
            lines.Lines.Select(l => l.IsSeparator ? string.Empty : l.Text));
    }

    [Fact]
    public void S7513_birthdays_are_offered_while_a_chosen_account_has_contacts_and_not_for_a_caldav_calendar_alone()
    {
        var birthdays = Str.Calendar_ShowBirthdays_Label.Text();

        Assert.Contains(Build(new CalendarWidgetConfig { AccountIds = ["work"] }).Lines, l => l.Text == birthdays);
        Assert.DoesNotContain(Build(new CalendarWidgetConfig { AccountIds = ["dav"] }).Lines, l => l.Text == birthdays);
        Assert.DoesNotContain(Build(new CalendarWidgetConfig(), new Office([])).Lines, l => l.Text == birthdays);
        Assert.DoesNotContain(Build(new CalendarWidgetConfig { AccountIds = ["dav"] }).Lines, l => l.IsSeparator);
    }

    [Fact]
    public void S7513_the_week_is_one_to_fourteen_days_as_dots()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Week };
        var days = Line(Build(config).StyleLines, CalendarStr.WidgetsA_Calendar_Settings_Days_Label.Text());

        Assert.Equal(CalendarPeriod.WeekMaxDays, days.Children!.Count);
        Line(days.Children!, LocaleFormat.Integer(5)).Action!();
        Assert.Equal(5, config.WeekDays);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S7513_a_preset_range_holds_the_dot_and_picking_it_again_saves_nothing()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda, AgendaDays = 7 };
        var range = Line(Build(config).StyleLines, Range).Children!;
        var week = Line(range, CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(7));

        Assert.True(week.Checked);
        week.Action!();
        Assert.Equal(0, _saves);

        Line(range, Str.Common_Today.Text()).Action!();
        Assert.Equal(1, config.AgendaDays);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    [InlineData("a week")]
    public void S7513_a_custom_range_refuses_what_is_not_one_to_365_days(string typed)
    {
        var range = Line(Build(new CalendarWidgetConfig { Style = CalendarStyle.Agenda }).StyleLines, Range).Children!;
        range[^1].Action!();

        Assert.NotNull(_asked!.Refuse(typed));
    }

    [Fact]
    public void S7513_a_custom_range_is_stored_and_then_holds_the_dot()
    {
        var config = new CalendarWidgetConfig { Style = CalendarStyle.Agenda };
        Line(Build(config).StyleLines, Range).Children![^1].Action!();
        _asked!.Done("45");

        Assert.Equal(45, config.AgendaDays);
        var custom = Line(Build(config).StyleLines, Range).Children![^1];
        Assert.True(custom.Checked);
        Assert.Equal(CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(45), custom.Value);
    }

    [Fact]
    public void S7513_birthdays_turn_off_and_save()
    {
        var config = new CalendarWidgetConfig();
        Line(Build(config).Lines, Str.Calendar_ShowBirthdays_Label.Text()).Action!();

        Assert.False(config.ShowBirthdays);
        Assert.Equal(1, _saves);
    }
}
