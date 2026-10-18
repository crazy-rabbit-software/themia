using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-561] [S-580] The settings rows the account widgets share, in the SDK so each widget's page in its
/// own project builds from them: the accounts a widget reads (the kit's account rows over the
/// accounts the host's office source offers) and, for every calendar widget (the calendar, Next
/// meeting, the week planner), each account's calendars with their colours and the birthdays row.
/// <para>
/// Accounts are signed in on the Accounts page, not here ([S-537] of the classic widgets): this
/// lists the accounts already signed in that can feed the widget, and links there, so there is one
/// place an account comes into being.
/// </para>
/// </summary>
public static class OfficeSettingsRows
{
    private const string CalendarScope = "calendar-settings";

    /// <summary>
    /// [S-561] A calendar widget's accounts, and under each chosen one its calendars, shown or hidden,
    /// and a colour each ([S-566], [S-567]).
    /// </summary>
    public static void CalendarAccounts(WidgetSettingsRows rows, CalendarConfig config)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);

        // Every calendar picker on the page, so a change that moves the palette indices (a calendar
        // shown or hidden, an account chosen) can redraw the "Automatic" colours of all of them.
        var pickers = new List<Action>();
        void SaveAndRedraw()
        {
            rows.Save();
            foreach (var redraw in pickers)
            {
                redraw();
            }
        }

        rows.Rows.Add(FluentLayout.Header(Str.WidgetsA_Calendar_Settings_Accounts.Text()));
        Accounts(rows, HostFeatures.Calendar, Glyphs.Calendar,
            new OfficeAccountTexts(Str.WidgetsA_Calendar_NoAccount, Str.WidgetsA_Calendar_Settings_Every, Str.WidgetsA_Calendar_Settings_AddHint),
            () => config.AccountIds, ids => config.AccountIds = ids, SaveAndRedraw,
            account => CalendarPicker(account, config, SaveAndRedraw, pickers));
    }

    /// <summary>[S-575] Whether the calendar shows the accounts' contacts' birthdays.</summary>
    public static void Birthdays(WidgetSettingsRows rows, CalendarConfig config)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);

        rows.Rows.Add(FluentLayout.Row(Glyphs.Contact, Str.Calendar_ShowBirthdays_Label.Text(),
            Str.WidgetsA_Calendar_Settings_Birthdays_Desc.Text(),
            FluentLayout.Toggle(config.ShowBirthdays, value => { config.ShowBirthdays = value; rows.Save(); })));
    }

    /// <summary>
    /// "Every account", or a choice of them — the Mail widget's pattern, so the three account
    /// widgets read alike. Every is the default and means accounts added later join too; turning it
    /// off starts from the accounts there are now, so nothing disappears; the last chosen account
    /// cannot be turned off, because an empty list means "every".
    /// </summary>
    /// <param name="perAccount">Rows to show under an account while it is chosen (the calendar picker).</param>
    public static void Accounts(
        WidgetSettingsRows rows,
        FeatureId feature,
        string glyph,
        OfficeAccountTexts texts,
        Func<List<string>> getIds,
        Action<List<string>> setIds,
        Action save,
        Func<AccountInfo, FrameworkElement>? perAccount)
    {
        var accounts = SettingsHost.Office?.Accounts(feature) ?? [];
        var rowsOf = accounts.Select(a => new KitAccountRow(a.Id, a.Label, a.Problem ?? $"{ProviderCatalog.Of(a.Provider).Label.Text()} · {a.Address}")).ToList();

        // [S-7063] The kit's account rows: one path for every widget that reads accounts.
        KitRows.Accounts(rows.Rows, glyph, rowsOf, new KitAccountTexts(texts.None.Text(), texts.Every.Text(), texts.AddHint.Text()),
            getIds, setIds, save, rows.ShowAccounts,
            perAccount is null ? null : row => perAccount(accounts.First(a => a.Id == row.Id)));
    }

    /// <summary>The sentences that name the account kind: whole sentences per widget, never a noun glued in.</summary>
    public readonly record struct OfficeAccountTexts(Str None, Str Every, Str AddHint);

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

            var selected = CalendarRules.SelectAccounts(config.AccountIds, SettingsHost.Office?.Accounts(HostFeatures.Calendar).Select(a => a.Id).ToList() ?? []);
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
            if (SettingsHost.Office is not { } source)
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

        return ColorChoice.Create(
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
