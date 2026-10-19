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
/// [S-7513] The calendar's options, all on its properties menu ([S-3719]): under Style only what the
/// style uses ([S-3601], [S-6070]: the week's first column and length, the agenda's range and
/// length); then Appearance ([S-3723]), every calendar widget's accounts and calendars with their
/// colours ([S-561], the SDK's <see cref="OfficeMenuLines"/>) and, after a divider, birthdays while
/// a chosen account has contacts ([S-575]). The week start is every
/// widget's regional setting ([S-3770]). It has no settings page.
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
            menu.StyleLines.Add(StyleLine(menu, calendar, setting));
        }

        menu.PlaceCard();
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, calendar, office));

        // [S-575] What the view shows besides the events, apart from where they come from; only
        // while a chosen account has the contacts the birthdays are read from.
        if (OfficeMenuLines.Birthdays(menu, calendar, office) is { } birthdays)
        {
            menu.Lines.Add(ItemMenuEntry.Separator);
            menu.Lines.Add(birthdays);
        }
    }

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

        // [S-3637] How many columns there are.
        CalendarSetting.WeekDays => menu.Number(Glyphs.Count, CalendarStr.WidgetsA_Calendar_Settings_Days_Label.Text(),
            config.EffectiveWeekDays, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays, 1, v => LocaleFormat.Integer(v), value => config.WeekDays = value),

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
        lines.Add(new(null, custom + "…", () => menu.Ask(new WidgetTextAsk(
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
            Initial: LocaleFormat.Integer(days))), Checked: !preset, Group: text, Value: preset ? null : DaysText(days)));

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
