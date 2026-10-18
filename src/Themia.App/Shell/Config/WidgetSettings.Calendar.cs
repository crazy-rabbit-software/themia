using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Themia.Core.Office;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.App.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-561] [S-3601] Every calendar widget's settings, one builder for the calendar, Next meeting and
/// the week planner: accounts, and under each one its calendars — shown or hidden, and a colour each
/// ([S-566], [S-567]) — then only what the calendar's style uses (the week's columns, the agenda's
/// range and length), and birthdays. The week start is every widget's
/// regional setting ([S-3770]).
/// </summary>
internal static partial class WidgetSettings
{
    public static void Calendar(WidgetSettingsRows rows, CalendarConfig config)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);

        OfficeSettingsRows.CalendarAccounts(rows, config);

        rows.Rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        // [S-3601] Only what the calendar's style uses; picking another style rebuilds the page
        // ([S-6071]). The week start is every calendar's ([S-3770]).
        if (config is CalendarWidgetConfig calendar)
        {
            foreach (var setting in calendar.ShownSettings)
            {
                CalendarStyleRow(rows.Rows, calendar, setting, rows.Save);
            }
        }

        OfficeSettingsRows.Birthdays(rows, config);
    }

    /// <summary>[S-3601] One of a style's own rows: the week's first column and length, the agenda's range and count.</summary>
    private static void CalendarStyleRow(List<FrameworkElement> rows, CalendarWidgetConfig config, CalendarSetting setting, Action save)
    {
        switch (setting)
        {
            case CalendarSetting.WeekFrom:
                // [S-3637] Where the columns start.
                var weekStart = CalendarText.WeekdayName(CalendarDays.FirstDay(config.EffectiveWeekStart));
                rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.WidgetsA_Calendar_Settings_WeekFrom_Label.Text(),
                    Str.WidgetsA_Calendar_Settings_WeekFrom_Desc.Text(),
                    FluentLayout.Choice<WeekFirstDay>(
                        [
                            (WeekFirstDay.Week, Str.WidgetsA_Calendar_Settings_WeekFrom_WeekStart.Format(("day", weekStart))),
                            (WeekFirstDay.Today, Str.Common_Today.Text()),
                        ],
                        config.WeekFrom,
                        value => { config.WeekFrom = value; save(); })));
                break;

            case CalendarSetting.WeekDays:
                // [S-3637] How many columns there are.
                rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsA_Calendar_Settings_Days_Label.Text(),
                    Str.WidgetsA_Calendar_Settings_WeekDays_Desc.Format(
                        ("week", LocaleFormat.Integer(CalendarPeriod.WeekDefaultDays)),
                        ("workweek", LocaleFormat.Integer(CalendarPeriod.WorkWeekDays))),
                    FluentLayout.Slider(config.EffectiveWeekDays, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays, 1, string.Empty,
                        value => { config.WeekDays = (int)Math.Round(value); save(); })));
                break;

            case CalendarSetting.AgendaRange:
                AgendaRangeRows(rows, config, save);
                break;

            case CalendarSetting.AgendaCount:
                rows.Add(FluentLayout.Row(Glyphs.Count, Str.Calendar_AgendaEvents_Label.Text(),
                    Str.WidgetsA_Calendar_Settings_AgendaCount_Desc.Text(),
                    FluentLayout.Slider(config.EffectiveAgendaCount, 1, 100, 1, string.Empty,
                        value => { config.AgendaCount = (int)Math.Round(value); save(); })));
                break;
        }
    }

    /// <summary>
    /// [S-3636] How far ahead the agenda looks: Today, Today and tomorrow, 3, 7 or 30 days, or Custom,
    /// which shows a number of days of its own (1 to 365) under it.
    /// </summary>
    private static void AgendaRangeRows(List<FrameworkElement> rows, CalendarWidgetConfig agenda, Action save)
    {
        const int Custom = 0;
        var options = new List<(int Value, string Label)>();
        foreach (var days in CalendarPeriod.AgendaDayPresets)
        {
            options.Add((days, days switch
            {
                1 => Str.Common_Today.Text(),
                2 => Str.WidgetsA_Calendar_Settings_AgendaRange_TodayTomorrow.Text(),
                _ => Str.WidgetsA_Calendar_Settings_AgendaRange_Days.Plural(days),
            }));
        }

        options.Add((Custom, Str.WidgetsA_Calendar_Settings_AgendaRange_Custom.Text()));

        var preset = CalendarPeriod.AgendaDayPresets.Contains(agenda.EffectiveAgendaDays);
        var stepper = FluentLayout.Stepper(agenda.EffectiveAgendaDays, CalendarPeriod.AgendaMinDays, 1, value => { agenda.AgendaDays = value; save(); });
        stepper.Maximum = CalendarPeriod.AgendaMaxDays;
        var custom = FluentLayout.Row(Glyphs.Count, Str.WidgetsA_Calendar_Settings_Days_Label.Text(), null, stepper);
        custom.Visibility = preset ? Visibility.Collapsed : Visibility.Visible;

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.WidgetsA_Calendar_Settings_AgendaRange_Label.Text(),
            Str.WidgetsA_Calendar_Settings_AgendaRange_Desc.Text(),
            FluentLayout.Choice<int>(options, preset ? agenda.EffectiveAgendaDays : Custom, value =>
            {
                custom.Visibility = value == Custom ? Visibility.Visible : Visibility.Collapsed;
                if (value != Custom)
                {
                    agenda.AgendaDays = value;
                    save();
                }
            })));
        rows.Add(custom);
    }
}
