using Themia.Core.Developer;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1106] The text logic of the clock, calendar, contacts, developer and desk widgets in a language
/// other than English. Every call pins its locale, so these never depend on (or race with) the
/// app-wide <see cref="Strings.Current"/>.
/// </summary>
public sealed class WidgetsALocaleTests
{
    private static ClockConfig Utc(Action<ClockConfig>? change = null)
    {
        var config = new ClockConfig { TimeZoneId = "Etc/UTC", ShowDate = true };
        change?.Invoke(config);
        return config;
    }

    // Monday 9 March 2026, 14:07 UTC.
    private static readonly DateTimeOffset Monday = new(2026, 3, 9, 14, 7, 0, TimeSpan.Zero);

    [Fact]
    public void S1106_the_clock_date_is_written_in_the_language_order()
    {
        Assert.Equal("Monday, March 9", ClockFormatter.Read(Monday, Utc(), Locale.En).Date);
        Assert.Equal("Montag, 9. März", ClockFormatter.Read(Monday, Utc(), Locale.De).Date);
    }

    [Fact]
    public void S1106_the_meridiem_comes_from_the_language()
    {
        Assert.Equal("PM", ClockFormatter.Read(Monday, Utc(c => c.Use24Hour = false), Locale.En).Meridiem);
        Assert.Equal("午後", ClockFormatter.Read(Monday, Utc(c => c.Use24Hour = false), Locale.Ja).Meridiem);
        Assert.Null(ClockFormatter.Read(Monday, Utc(c => c.Use24Hour = true), Locale.De).Meridiem);
    }

    [Fact]
    public void S1106_the_clock_details_are_translated()
    {
        var details = ClockFormatter.Details(new DateTimeOffset(2026, 10, 4, 13, 52, 0, TimeSpan.Zero), Utc(), Locale.De);

        Assert.Equal("KW 40 · Tag 277", details.Week);
        Assert.Equal("So., 4. Okt.", details.ShortDate);
    }

    [Fact]
    public void S1106_the_local_offset_is_translated_and_utc_stays_utc()
    {
        Assert.Equal("wie Ortszeit", ClockFormatter.OffsetText(TimeSpan.FromHours(2), TimeSpan.FromHours(2), ClockOffsetMode.Local, Locale.De));
        Assert.Equal("+8 h zur Ortszeit", ClockFormatter.OffsetText(TimeSpan.FromHours(9), TimeSpan.FromHours(1), ClockOffsetMode.Local, Locale.De));
        Assert.Equal("UTC+2", ClockFormatter.OffsetText(TimeSpan.FromHours(2), TimeSpan.Zero, ClockOffsetMode.Utc, Locale.De));
    }

    private static readonly DateTimeOffset Noon = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(Locale.De, "3 Tage")]
    [InlineData(Locale.Pl, "3 dni")]
    [InlineData(Locale.Ja, "3日")]
    public void S1106_a_countdown_counts_days_with_the_language_plural(Locale locale, string expected)
        => Assert.Equal(expected, CountdownFormatter.Read(Noon + TimeSpan.FromDays(3), Noon, CountdownFormat.Days, locale).Text);

    [Fact]
    public void S1106_a_day_count_in_the_past_takes_its_own_form_not_a_wrapper()
    {
        // "vor 3 Tagen", never "vor 3 Tage": the bare plural is not the dative German needs here.
        Assert.Equal("vor 3 Tagen", CountdownFormatter.Read(Noon - TimeSpan.FromDays(3), Noon, CountdownFormat.Days, Locale.De).Text);
        Assert.Equal("vor 2 Std.", CountdownFormatter.Read(Noon - TimeSpan.FromHours(2), Noon, CountdownFormat.Auto, Locale.De).Text);
        Assert.Equal("gerade eben", CountdownFormatter.Read(Noon - TimeSpan.FromSeconds(5), Noon, CountdownFormat.Auto, Locale.De).Text);
    }

    [Fact]
    public void S1106_the_next_line_reads_in_german()
    {
        var timers = new List<CountdownTimer>
        {
            CountdownTimer.Create("Urlaub", Noon + TimeSpan.FromDays(12), Noon),
            CountdownTimer.Create(string.Empty, Noon + TimeSpan.FromSeconds(20), Noon),
        };

        Assert.Equal("Danach: Urlaub in 12 Tagen", CountdownLayout.NextLine(timers, 1, Noon, Locale.De));
        Assert.Equal("Danach: Ohne Titel gleich", CountdownLayout.NextLine(timers, 0, Noon, Locale.De));
    }

    [Fact]
    public void S1106_the_countdown_target_follows_the_language_date_and_clock()
    {
        var target = new DateTimeOffset(2026, 6, 15, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal("15. Juni, 22:30", CountdownFormatter.TargetLabel(target, Noon, TimeZoneInfo.Utc, Locale.De));
        Assert.Equal("Montag, 15. Juni 2026 um 12:00", CountdownFormatter.FullTargetLabel(Noon, TimeZoneInfo.Utc, Locale.De));
        Assert.Equal("Tage · Std. · Min.", CountdownFormatter.FormatLabel(CountdownFormat.DaysHoursMinutes, Locale.De));
    }

    [Fact]
    public void S1106_github_ages_and_comment_counts_follow_the_language()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("5 Min.", GitHubRules.Ago(now.AddMinutes(-5), now, Locale.De));
        Assert.Equal("4. März", GitHubRules.Ago(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero), now, Locale.De));
        Assert.Equal("2025年3月", GitHubRules.Ago(new DateTimeOffset(2025, 3, 4, 9, 0, 0, TimeSpan.Zero), now, Locale.Ja));
        Assert.Equal("rosa · 3 komentarze", GitHubRules.ItemDetail("rosa", 3, Locale.Pl));
        Assert.Equal("rosa · 5 komentarzy", GitHubRules.ItemDetail("rosa", 5, Locale.Pl));
        Assert.Equal("erfolgreich", GitHubRules.RunLabel(RunState.Success, Locale.De));
    }

    [Fact]
    public void S1106_a_developer_widget_explains_itself_in_german()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(2));
        var limited = new DevOutcome("gh:work", FetchStatus.RateLimited, null, new DateTimeOffset(2026, 10, 4, 10, 45, 0, TimeSpan.Zero));

        Assert.Equal("GitHub hat Themia gebremst; neuer Versuch um 12:45", DevScreens.Explain("GitHub", limited, now, Locale.De));
        Assert.Equal("Melde dich unter „Konten“ bei GitHub an.", DevScreens.Decide("GitHub", [], ["gh:gone"], true, [], false, string.Empty, now, Locale.De, anonymous: true).Text);
    }

    [Fact]
    public void S1106_calendar_titles_and_problems_are_translated()
    {
        var sunday = new DateOnly(2026, 10, 4);

        Assert.Equal("Oktober 2026", CalendarRules.Title(CalendarViewMode.Month, sunday, WeekStartDay.Monday, Locale.De));
        Assert.Equal("28. Sept. – 4. Okt.", CalendarRules.Title(CalendarViewMode.Week, sunday, WeekStartDay.Monday, Locale.De));
        Assert.Equal("Der Zugriff auf die Kontakte wurde nicht gewährt.", SyncProblems.Message(SyncProblem.Forbidden, SyncSubject.Contacts, Locale.De));
        Assert.Equal(["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"], CalendarText.WeekdayHeaders(DayOfWeek.Monday, Locale.De));
        Assert.Equal("Telefon", ContactRules.ColumnTitle(ContactColumn.Phone, Locale.De));
    }

    [Fact]
    public void S1106_desk_and_system_texts_are_translated()
    {
        Assert.Equal("72 %", BluetoothRules.BatteryLabel(72, Locale.De));
        Assert.Equal("Bluetooth ist ausgeschaltet", BluetoothRules.StatusMessage(BluetoothStatus.RadioOff, Locale.De));
        Assert.Equal("5 znaków", ClipboardFormatting.Describe("hello", Locale.Pl));
        Assert.Equal("2 Zeilen", ClipboardFormatting.Describe("a\nb", Locale.De));
        Assert.Equal("Sonstige", InstalledAppRules.CategoryTitle(string.Empty, Locale.De));
        Assert.Equal("Klammer auf", CalculatorKeypad.Name(KeypadKey.OpenParen, Locale.De));
    }

    [Fact]
    public void S1106_claude_usage_resets_are_worded_per_language()
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal("Reset in 3 h 20 min", ClaudeUsageRules.ResetLabel(now.AddHours(3).AddMinutes(20), now, Locale.De));
        Assert.Equal("über dem Tempo", ClaudeUsageRules.PaceText(UsagePace.Ahead, Locale.De));
        Assert.Equal("Sitzung (5 Std.)", ClaudeUsageRules.BuildBars(new ClaudeUsageReport(UsageWindow.Unused, UsageWindow.Unused, null, null), false, false, Locale.De)[0].Label);
    }
}
