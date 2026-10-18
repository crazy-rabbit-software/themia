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
    private const string CalendarScope = "calendar-settings";

    public static void Calendar(List<FrameworkElement> rows, CalendarConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        // Every calendar picker on the page, so a change that moves the palette indices (a calendar
        // shown or hidden, an account chosen) can redraw the "Automatic" colours of all of them.
        var pickers = new List<Action>();
        void RedrawPickers()
        {
            foreach (var redraw in pickers)
            {
                redraw();
            }
        }

        void SaveAndRedraw()
        {
            save();
            RedrawPickers();
        }

        rows.Add(FluentLayout.Header(Str.WidgetsA_Calendar_Settings_Accounts.Text()));
        OfficeAccountRows(rows, HostFeatures.Calendar, Glyphs.Calendar,
            new OfficeAccountTexts(Str.WidgetsA_Calendar_NoAccount, Str.WidgetsA_Calendar_Settings_Every, Str.WidgetsA_Calendar_Settings_AddHint),
            () => config.AccountIds, ids => config.AccountIds = ids, SaveAndRedraw,
            account => CalendarPicker(account, config, SaveAndRedraw, pickers));

        // [S-4826] Next meeting chooses its accounts and calendars like every calendar, and has no
        // display rows: birthdays are all-day, never a meeting.
        if (config is NextMeetingConfig)
        {
            return;
        }

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        // [S-3601] Only what the calendar's style uses; picking another style rebuilds the page
        // ([S-6071]). The week start is every calendar's ([S-3770]).
        if (config is CalendarWidgetConfig calendar)
        {
            foreach (var setting in calendar.ShownSettings)
            {
                CalendarStyleRow(rows, calendar, setting, save);
            }
        }

        rows.Add(FluentLayout.Row(Glyphs.Contact, Str.Calendar_ShowBirthdays_Label.Text(),
            Str.WidgetsA_Calendar_Settings_Birthdays_Desc.Text(),
            FluentLayout.Toggle(config.ShowBirthdays, value => { config.ShowBirthdays = value; save(); })));
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

    /// <summary>
    /// [S-567] One account's calendars: each with a colour, and — when there is more than one — a
    /// switch. The default calendar starts on; turning the last one off goes back to the default.
    /// Filled when the list arrives; the account's own default works meanwhile.
    /// </summary>
    private static FrameworkElement CalendarPicker(AccountInfo account, CalendarConfig config, Action save, List<Action> pickers)
    {
        var host = new StackPanel { Spacing = Fluent.Space1, Margin = new Thickness(Fluent.Space6, 0, 0, Fluent.Space2) };
        IReadOnlyList<CalendarInfo>? calendars = null;

        var loading = FluentLayout.Note(Str.WidgetsA_Calendar_Settings_Listing.Text());
        host.Children.Add(loading);

        void Redraw()
        {
            host.Children.Clear();

            if (calendars is null)
            {
                host.Children.Add(loading);
                return;
            }

            var selected = CalendarRules.SelectAccounts(config.AccountIds, OfficeSources.Current?.Accounts(HostFeatures.Calendar).Select(a => a.Id).ToList() ?? []);
            var targets = CalendarRules.Targets(selected, config.Calendars);

            foreach (var calendar in calendars)
            {
                // The palette index is the calendar's place among everything the widget fetches,
                // exactly as the widget resolves it, so the swatch here is the chip there.
                var index = -1;
                for (var i = 0; i < targets.Count; i++)
                {
                    if (targets[i].AccountId == account.Id
                        && (targets[i].CalendarId == calendar.Id || (targets[i].CalendarId is null && calendar.IsDefault)))
                    {
                        index = i;
                        break;
                    }
                }

                var key = index >= 0 ? CalendarRules.ColorKey(account.Id, targets[index].CalendarId) : calendar.Id;
                var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space3 };

                if (index >= 0)
                {
                    controls.Children.Add(CalendarColor(config, key, index, calendar.Name, save));
                }

                if (calendars.Count > 1)
                {
                    var calendarId = calendar.Id;
                    controls.Children.Add(FluentLayout.Toggle(CalendarRules.IsCalendarShown(config.Calendars, account.Id, calendar), _ =>
                    {
                        config.Calendars = CalendarRules.ToggleCalendar(config.Calendars, account.Id, calendarId, calendars);
                        save();
                    }));
                }

                host.Children.Add(FluentLayout.Row(Glyphs.Calendar, calendar.Name, calendar.IsDefault ? Str.WidgetsA_Calendar_Settings_DefaultCalendar.Text() : null, controls));
            }
        }

        pickers.Add(Redraw);
        _ = LoadCalendarsAsync();
        return host;

        async Task LoadCalendarsAsync()
        {
            if (OfficeSources.Current is not { } source)
            {
                return;
            }

            try
            {
                var result = await source.CalendarsAsync(account.Id, force: false, CancellationToken.None).ConfigureAwait(true);
                calendars = result.Value ?? [];

                if (result.Value is null)
                {
                    loading.Text = Str.WidgetsA_Calendar_Settings_ListFailedWhy.Format(("problem", SyncProblems.Message(result.Problem, SyncSubject.Calendar, Strings.Current)));
                    host.Children.Clear();
                    host.Children.Add(loading);
                    return;
                }

                Redraw();
            }
            catch (Exception ex)
            {
                // Started from building the page: nothing above may see an exception.
                Log.Error(CalendarScope, $"could not list the calendars of {account.Id}", ex);
                loading.Text = Str.WidgetsA_Calendar_Settings_ListFailed.Text();
            }
        }
    }

    /// <summary>
    /// [S-566] Automatic (the palette colour at the calendar's index), one of the six, or any colour
    /// from the shared picker ([S-1549]) — never typed.
    /// </summary>
    private static DropDownButton CalendarColor(CalendarConfig config, string key, int index, string name, Action save)
    {
        var automatic = CalendarRules.PaletteColor(index);
        var presets = new List<ColorPreset>();
        for (var i = 0; i < CalendarRules.Palette.Count; i++)
        {
            presets.Add(new ColorPreset(CalendarRules.Palette[i], CalendarRules.PaletteNames[i].Local()));
        }

        var chosen = config.CalendarColors.TryGetValue(key, out var hex) && CalendarRules.IsHexColor(hex) ? hex : string.Empty;

        return Shell.ColorChoice.Create(
            chosen,
            value =>
            {
                var colors = new Dictionary<string, string>(config.CalendarColors, StringComparer.Ordinal);
                if (value.Length == 0)
                {
                    colors.Remove(key);
                }
                else
                {
                    colors[key] = value.ToLowerInvariant();
                }

                config.CalendarColors = colors;
                save();
            },
            new ColorChoiceOptions
            {
                Presets = presets,
                PresetsTitle = Str.WidgetsA_Calendar_Settings_Colors.Text(),
                DefaultLabel = Str.WidgetsA_Calendar_Settings_ColorAuto.Text(),
                DefaultColor = () => HexColor.TryParse(automatic, out var r, out var g, out var b)
                    ? Windows.UI.Color.FromArgb(255, r, g, b)
                    : null,
            },
            Str.WidgetsA_Calendar_Settings_ColorOf.Format(("name", name)));
    }
}
