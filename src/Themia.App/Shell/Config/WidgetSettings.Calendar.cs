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

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-561] The calendar widget's settings: accounts, and under each one its calendars — shown or
/// hidden, and a colour each ([S-566], [S-567]) — then the layout, the week start, the agenda length
/// and birthdays. The classic widget's Source and Display tabs, on one page.
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
        OfficeAccountRows(rows, AccountFeatures.Calendar, Glyphs.Calendar,
            new OfficeAccountTexts(Str.WidgetsA_Calendar_NoAccount, Str.WidgetsA_Calendar_Settings_Every, Str.WidgetsA_Calendar_Settings_AddHint),
            () => config.AccountIds, ids => config.AccountIds = ids, SaveAndRedraw,
            account => CalendarPicker(account, config, SaveAndRedraw, pickers));

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        // [S-2700] The one layout the widget draws, at every size.
        rows.Add(FluentLayout.Row(Glyphs.View, Str.Calendar_View_Label.Text(),
            Str.WidgetsA_Calendar_Settings_Layout_Desc.Text(),
            FluentLayout.Choice<CalendarLayout>(
                [
                    (CalendarLayout.Month, Str.CtxMenu_Month.Text()),
                    (CalendarLayout.Week, Str.CtxMenu_Week.Text()),
                    (CalendarLayout.Day, Str.CtxMenu_Day.Text()),
                    (CalendarLayout.Year, Str.WidgetsA_Calendar_Layout_Year.Text()),
                    (CalendarLayout.Agenda, Str.CtxMenu_Agenda.Text()),
                ],
                config.EffectiveLayout,
                value => { config.Layout = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Calendar_WeekStart_Label.Text(), null,
            FluentLayout.Choice<WeekStartDay>(
                [(WeekStartDay.Monday, Str.Calendar_WeekStart_Monday.Text()), (WeekStartDay.Sunday, Str.Calendar_WeekStart_Sunday.Text())],
                config.EffectiveWeekStart,
                value => { config.WeekStart = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Count, Str.Calendar_AgendaEvents_Label.Text(),
            Str.WidgetsA_Calendar_Settings_AgendaCount_Desc.Text(),
            FluentLayout.Slider(config.EffectiveAgendaCount, 1, 100, 1, string.Empty,
                value => { config.AgendaCount = (int)Math.Round(value); save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Contact, Str.Calendar_ShowBirthdays_Label.Text(),
            Str.WidgetsA_Calendar_Settings_Birthdays_Desc.Text(),
            FluentLayout.Toggle(config.ShowBirthdays, value => { config.ShowBirthdays = value; save(); })));
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

            var selected = CalendarRules.SelectAccounts(config.AccountIds, OfficeSources.Current?.Accounts(AccountFeatures.Calendar).Select(a => a.Id).ToList() ?? []);
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
            presets.Add(new ColorPreset(CalendarRules.Palette[i], CalendarRules.PaletteNames[i]));
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
