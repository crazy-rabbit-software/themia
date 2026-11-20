using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-7513] The calendar's options, all on its page ([S-3719]): under Style only what the
/// style uses ([S-3601], [S-6070]: the week's first column and length, the agenda's range and
/// length); then Appearance ([S-3723]), every calendar widget's accounts and calendars with their
/// colours ([S-561], the SDK's <see cref="OfficeMenuLines"/>) and, after a divider, birthdays while
/// a chosen account has contacts ([S-575]). The week start is every widget's regional setting
/// ([S-3770]): a style laid out in weeks (month, week, year) links to it under Style ([S-3749]),
/// never sets its own. It has no settings page.
/// </summary>
internal static class CalendarMenu
{
    /// <param name="office">The host's office source (<see cref="SettingsHost.Office"/>), null before it has started.</param>
    public static void Lines(WidgetMenuLines menu, CalendarWidgetConfig calendar, IOfficeSource? office)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(calendar);

        foreach (var setting in calendar.ShownSettings)
        {
            menu.StyleLines.AddRange(StyleLines(menu, calendar, setting));
        }

        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, calendar, office));

        // [S-575] What the view shows besides the events, apart from where they come from; only
        // while a chosen account has the contacts the birthdays are read from.
        if (OfficeMenuLines.Birthdays(menu, calendar, office) is { } birthdays)
        {
            menu.Lines.Add(ItemMenuEntry.Separator);
            menu.Lines.Add(birthdays);
        }
    }

    /// <summary>[S-3601] A style's own lines for one setting: what it shows and so how large it is ([S-11540]..[S-11546]).</summary>
    private static IEnumerable<ItemMenuEntry> StyleLines(WidgetMenuLines menu, CalendarWidgetConfig config, CalendarSetting setting) => setting switch
    {
        // [S-11540] The agenda's rows and their lines (the kit's rows contract).
        CalendarSetting.AgendaRows => KitMenuLines.Rows(menu, config),

        // [S-11542] The year's days: squares, or the day numbers (how large is the card's).
        CalendarSetting.YearNumbers => [menu.Toggle(null, CalendarStr.Calendar_YearNumbers.Text(), config.YearNumbers, on => config.YearNumbers = on)],

        // [S-11544] A month day's events: their titles as chips, or dots. [S-14410] How many a day lists, a day's or a
        // week's hours and a week's days are the card's: a resize sets them, no line does.
        CalendarSetting.MonthEvents => [MonthEventsLine(menu, config)],

        // [S-11546] Titles in the week's columns (each 96).
        CalendarSetting.WeekTitles => [menu.Toggle(null, CalendarStr.Calendar_WeekTitles.Text(), config.WeekTitles, on => config.WeekTitles = on)],

        // [S-11545] The chosen day's rows under the day strip (the height follows).
        CalendarSetting.WeekRows => [KitMenuLines.Count(menu, null, Str.Kit_Rows.Text(), CalendarSizing.ClampWeekRows(config.WeekRows), 1, CalendarSizing.MaxWeekRows, 1, v => config.WeekRows = v)],

        _ => [StyleLine(menu, config, setting)],
    };

    /// <summary>[S-11544] The month's Events as: Dots or Titles.</summary>
    private static ItemMenuEntry MonthEventsLine(WidgetMenuLines menu, CalendarWidgetConfig config)
        => menu.Choice(null, CalendarStr.Calendar_MonthEvents.Text(),
            [(MonthEvents.Dots, CalendarStr.Calendar_MonthEvents_Dots.Text()), (MonthEvents.Chips, CalendarStr.Calendar_MonthEvents_Chips.Text())],
            config.MonthEvents, value => config.MonthEvents = value);

    /// <summary>[S-3601] One of a style's own lines: the week's first column and length, the agenda's range and count.</summary>
    private static ItemMenuEntry StyleLine(WidgetMenuLines menu, CalendarWidgetConfig config, CalendarSetting setting) => setting switch
    {
        // [S-3637] Where the columns start.
        CalendarSetting.WeekFrom => menu.Choice(Glyphs.Calendar, CalendarStr.WidgetsA_Calendar_Settings_WeekFrom_Label.Text(),
            [
                (WeekFirstDay.Week, CalendarStr.WidgetsA_Calendar_Settings_WeekFrom_WeekStart.Format(("day", CalendarText.WeekdayName(CalendarDays.FirstDay(config.EffectiveWeekStart))))),
                (WeekFirstDay.Today, Str.Common_Today.Text()),
            ],
            config.WeekFrom, value => config.WeekFrom = value),

        CalendarSetting.AgendaRange => AgendaRange(menu, config),

        // [S-571] How many entries the agenda lists.
        _ => KitMenuLines.Count(menu, Glyphs.Count, CalendarStr.Calendar_AgendaEvents_Label.Text(), config.EffectiveAgendaCount, 1, 100, 1, value => config.AgendaCount = value),
    };

    /// <summary>
    /// [S-3636] How far ahead the agenda looks: Today, Today and tomorrow, 3, 7 or 30 days, or
    /// Custom…, which asks for a number of days of its own (1 to 365) and holds the dot while the
    /// range is not one of the others.
    /// </summary>
    private static ItemMenuEntry AgendaRange(WidgetMenuLines menu, CalendarWidgetConfig agenda)
    {
        var text = CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Label.Text();
        var days = agenda.EffectiveAgendaDays;
        var preset = CalendarPeriod.AgendaDayPresets.Contains(days);

        var lines = new List<ItemMenuEntry>();
        foreach (var option in CalendarPeriod.AgendaDayPresets)
        {
            lines.Add(new(null, DaysText(option), () =>
            {
                if (option != days)
                {
                    agenda.AgendaDays = option;
                    menu.Save();
                }
            }, Checked: option == days, Group: text));
        }

        var custom = CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Custom.Text();
        // [S-3726] The box says the range in days, as a number line's does in its own unit.
        var reason = Str.Menu_NumberRange.Format(
            ("min", CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(CalendarPeriod.AgendaMinDays)),
            ("max", CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(CalendarPeriod.AgendaMaxDays)));
        lines.Add(ItemMenuEntry.Separator);
        lines.Add(WidgetMenuLines.Ask(null, custom + "…", new WidgetTextAsk(
            CalendarStr.WidgetsA_Calendar_Settings_Days_Label.Text(),
            reason,
            Str.Common_Save.Text(),
            typed => Days(typed, out _) ? null : reason,
            typed =>
            {
                if (Days(typed, out var number) && number != days)
                {
                    agenda.AgendaDays = number;
                    menu.Save();
                }
            },
            Initial: LocaleFormat.Integer(days))) with { Checked = !preset, Group = text, Value = preset ? null : DaysText(days) });

        return WidgetMenuLines.Submenu(Glyphs.Calendar, text, lines) with { Value = DaysText(days) };
    }

    /// <summary>[S-3636] A typed number of days: 1 to 365, whole.</summary>
    internal static bool Days(string typed, out int days)
        => WidgetMenuLines.ParseNumber(typed, CalendarPeriod.AgendaMinDays, CalendarPeriod.AgendaMaxDays, 1, out days);

    private static string DaysText(int days) => days switch
    {
        1 => Str.Common_Today.Text(),
        2 => CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_TodayTomorrow.Text(),
        _ => CalendarStr.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(days),
    };
}
