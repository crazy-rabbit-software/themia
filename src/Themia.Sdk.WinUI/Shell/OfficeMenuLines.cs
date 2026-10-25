using System.Runtime.CompilerServices;
using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Widgets;
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
/// [S-561] [S-580] The properties-menu lines ([S-3719]) the account widgets share, in the SDK so each
/// widget's menu in its own project builds from them: the accounts a widget reads (the kit's
/// Accounts submenu over the signed-in accounts offering its need) and, for every calendar widget
/// (the calendar, Next meeting, the week planner), each chosen account's calendars, shown or hidden,
/// with a colour each, and the birthdays switch.
/// <para>
/// Accounts are signed in on the Accounts page, not here ([S-537] of the classic widgets): this lists
/// the accounts already signed in that can feed the widget, and opens that page, so there is one place
/// an account comes into being.
/// </para>
/// </summary>
public static class OfficeMenuLines
{
    /// <summary>[S-3746] Each office source's calendar lists as the menus last read them, by account.</summary>
    private static readonly ConditionalWeakTable<IOfficeSource, MenuLoads<CalendarListing>> Listings = new();

    /// <summary>[S-567] What an account's calendars are, as last read: the list, or why there is none.</summary>
    private sealed record CalendarListing(IReadOnlyList<CalendarInfo>? Calendars, string? Problem)
    {
        public static bool Same(CalendarListing a, CalendarListing b)
            => a.Problem == b.Problem
                && (a.Calendars ?? []).SequenceEqual(b.Calendars ?? []);
    }

    /// <summary>[S-7063] The signed-in accounts as the kit's account lines: the label, then the problem or the provider.</summary>
    public static IReadOnlyList<KitAccountLine> Lines(IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        return [.. accounts.Select(a => new KitAccountLine(a.Id, a.Label, a.Problem ?? ProviderCatalog.Of(a.Provider).Label.Text()))];
    }

    /// <summary>[S-561] [S-580] The Accounts submenu of an account widget over the accounts offering <paramref name="feature"/>.</summary>
    public static ItemMenuEntry Accounts(
        WidgetMenuLines menu,
        IReadOnlyList<AccountInfo> accounts,
        string? glyph,
        KitAccountWords words,
        IReadOnlyList<string> chosenIds,
        Action<List<string>> setIds)
        => KitMenuLines.Accounts(menu, glyph, Lines(accounts), words, chosenIds, setIds);

    /// <summary>
    /// [S-561] A calendar widget's Accounts submenu ([S-562]: "Every calendar account" or a choice),
    /// then its Calendars ([S-567]: each chosen account's calendars, a switch each when there are
    /// several, and a colour each, [S-566]), read through <paramref name="source"/> (the host's office
    /// source, <see cref="SettingsHost.Office"/>). No Calendars line while no account is chosen.
    /// </summary>
    /// <param name="colours">
    /// [S-566] Whether the widget draws its events in their calendar's colour. Without (Next meeting)
    /// there are no colour lines, and an account with one calendar has nothing to choose, so it is
    /// left out, and the Calendars line with it when no chosen account has two ([S-6070]).
    /// </param>
    public static IEnumerable<ItemMenuEntry> CalendarAccounts(WidgetMenuLines menu, CalendarConfig config, IOfficeSource? source, bool colours = true)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        var accounts = source?.Accounts(HostFeatures.Calendar) ?? [];
        yield return Accounts(menu, accounts, Glyphs.Contact,
            new KitAccountWords(Str.WidgetsA_Calendar_NoAccount.Text(), Str.WidgetsA_Calendar_Settings_Every.Text()),
            config.AccountIds, ids => config.AccountIds = ids);

        if (source is null || CalendarsLine(menu, config, source, accounts, colours) is not { } calendars)
        {
            yield break;
        }

        yield return calendars;
    }

    /// <summary>
    /// [S-575] Whether the calendar shows the accounts' contacts' birthdays: offered only while a
    /// chosen calendar account has an address book (<see cref="OffersBirthdays"/>), null otherwise.
    /// </summary>
    public static ItemMenuEntry? Birthdays(WidgetMenuLines menu, CalendarConfig config, IOfficeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        return OffersBirthdays(config.AccountIds, source?.Accounts(HostFeatures.Calendar) ?? [])
            ? menu.Toggle(Glyphs.Gift, Str.Calendar_ShowBirthdays_Label.Text(), config.ShowBirthdays, on => config.ShowBirthdays = on)
            : null;
    }

    /// <summary>
    /// [S-575] [S-6070] Whether the birthdays switch changes anything: the birthdays are read from
    /// the chosen calendar accounts' contacts, so only while one of them has contacts (a CalDAV
    /// calendar has no address book behind it, and with no account there is nothing to read).
    /// </summary>
    public static bool OffersBirthdays(IReadOnlyList<string> chosenIds, IReadOnlyList<AccountInfo> accounts)
    {
        ArgumentNullException.ThrowIfNull(chosenIds);
        ArgumentNullException.ThrowIfNull(accounts);

        var selected = CalendarRules.SelectAccounts(chosenIds, [.. accounts.Select(a => a.Id)]);
        return accounts.Any(a => selected.Contains(a.Id) && a.Features.Has(HostFeatures.Contacts));
    }

    /// <summary>
    /// [S-567] The Calendars submenu: the one chosen account's calendars straight in it, or a submenu
    /// per chosen account. Each list is read through the source's cache ([S-3746]); until it has come
    /// the account says it is looking, and a list that could not be read says why (the default
    /// calendar is shown meanwhile).
    /// </summary>
    private static ItemMenuEntry? CalendarsLine(WidgetMenuLines menu, CalendarConfig config, IOfficeSource source, IReadOnlyList<AccountInfo> accounts, bool colours)
    {
        var selected = CalendarRules.SelectAccounts(config.AccountIds, [.. accounts.Select(a => a.Id)]);
        if (selected.Count == 0)
        {
            return null;
        }

        var targets = CalendarRules.Targets(selected, config.Calendars);
        var loads = Listings.GetValue(source, _ => new MenuLoads<CalendarListing>(CalendarListing.Same));
        var label = Str.WidgetsA_Calendar_Settings_Calendars.Text();

        List<ItemMenuEntry> Of(AccountInfo account)
        {
            var listing = loads.Read(account.Id, menu, () => ListAsync(source, account.Id));
            if (listing.Value is not { Calendars: { } calendars })
            {
                var note = listing.Value?.Problem ?? (listing.Failed ? Str.WidgetsA_Calendar_Settings_ListFailed.Text() : Str.WidgetsA_Calendar_Settings_Listing.Text());
                return [WidgetMenuLines.Note(note)];
            }

            return CalendarLines(menu, config, account.Id, calendars, targets, colours);
        }

        // An account with nothing to choose (one calendar and no colours) has no line of its own.
        var chosen = accounts.Where(a => selected.Contains(a.Id)).ToList();
        var lines = chosen.Count == 1
            ? Of(chosen[0])
            : [.. chosen.Select(a => (Account: a, Lines: Of(a))).Where(a => a.Lines.Count > 0).Select(a => WidgetMenuLines.Submenu(null, a.Account.Label, a.Lines))];
        return lines.Count == 0 ? null : WidgetMenuLines.Submenu(Glyphs.Calendar, label, lines);
    }

    /// <summary>
    /// [S-567] One account's calendars: a switch each when there are several, then their colours
    /// where the widget draws them (<paramref name="withColours"/>); nothing when there is nothing to choose.
    /// </summary>
    private static List<ItemMenuEntry> CalendarLines(WidgetMenuLines menu, CalendarConfig config, string accountId, IReadOnlyList<CalendarInfo> calendars, IReadOnlyList<CalendarTarget> targets, bool withColours)
    {
        if (calendars.Count == 0)
        {
            return withColours ? [WidgetMenuLines.Note(Str.WidgetsA_Calendar_Settings_DefaultCalendar.Text())] : [];
        }

        var colours = new List<ItemMenuEntry>();
        IReadOnlyList<CalendarInfo> coloured = withColours ? calendars : [];
        foreach (var calendar in coloured)
        {
            // The palette index is the calendar's place among everything the widget fetches,
            // exactly as the widget resolves it, so the swatch here is the chip there.
            var index = -1;
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i].AccountId == accountId
                    && (targets[i].CalendarId == calendar.Id || (targets[i].CalendarId is null && calendar.IsDefault)))
                {
                    index = i;
                    break;
                }
            }

            if (index >= 0)
            {
                var text = calendars.Count > 1 ? calendar.Name : Str.WidgetsA_Calendar_Settings_ColorOf.Format(("name", calendar.Name));
                colours.Add(Colour(menu, config, text, CalendarRules.ColorKey(accountId, targets[index].CalendarId), index));
            }
        }

        if (calendars.Count == 1)
        {
            return colours;
        }

        var lines = new List<ItemMenuEntry>(calendars.Count + 2);
        foreach (var calendar in calendars)
        {
            var calendarId = calendar.Id;
            lines.Add(menu.Toggle(null, calendar.Name, CalendarRules.IsCalendarShown(config.Calendars, accountId, calendar),
                _ => config.Calendars = CalendarRules.ToggleCalendar(config.Calendars, accountId, calendarId, calendars))
                with { Value = calendar.IsDefault ? Str.WidgetsA_Calendar_Settings_DefaultCalendar.Text() : null });
        }

        if (colours.Count > 0)
        {
            lines.Add(ItemMenuEntry.Separator);
            lines.Add(WidgetMenuLines.Submenu(null, Str.WidgetsA_Calendar_Settings_Colors.Text(), colours));
        }

        return lines;
    }

    /// <summary>
    /// [S-566] A calendar's colour: Automatic (the palette colour at its index), one of the six, or any
    /// colour from the shared picker ([S-1549]), never typed. Stored lower-case; Automatic removes it.
    /// </summary>
    private static ItemMenuEntry Colour(WidgetMenuLines menu, CalendarConfig config, string text, string key, int index)
    {
        var automatic = CalendarRules.PaletteColor(index);
        var presets = new List<ColorPreset>();
        for (var i = 0; i < CalendarRules.Palette.Count; i++)
        {
            presets.Add(new ColorPreset(CalendarRules.Palette[i], CalendarRules.PaletteNames[i].Local()));
        }

        var chosen = config.CalendarColors.TryGetValue(key, out var hex) && CalendarRules.IsHexColor(hex) ? hex : string.Empty;
        return menu.Colour(text, chosen, value => config.CalendarColors = SetColour(config.CalendarColors, key, value), new ColorChoiceOptions
        {
            Presets = presets,
            DefaultLabel = Str.WidgetsA_Calendar_Settings_ColorAuto.Text(),
            DefaultColor = () => HexColor.TryParse(automatic, out var r, out var g, out var b)
                ? Windows.UI.Color.FromArgb(255, r, g, b)
                : null,
        });
    }

    /// <summary>[S-566] The colours after one is picked: a colour stored lower-case, Automatic (empty) taken out.</summary>
    public static Dictionary<string, string> SetColour(IReadOnlyDictionary<string, string> colours, string key, string value)
    {
        var next = new Dictionary<string, string>(colours, StringComparer.Ordinal);
        if (value.Length == 0)
        {
            next.Remove(key);
        }
        else
        {
            next[key] = value.ToLowerInvariant();
        }

        return next;
    }

    /// <summary>[S-567] An account's calendars through the source's own cache, or why they could not be listed.</summary>
    private static async Task<CalendarListing> ListAsync(IOfficeSource source, string accountId)
    {
        var result = await source.CalendarsAsync(accountId, force: false, CancellationToken.None).ConfigureAwait(true);
        return result.Value is { } calendars
            ? new CalendarListing(calendars, null)
            : new CalendarListing(null, Str.WidgetsA_Calendar_Settings_ListFailedWhy.Format(("problem", SyncProblems.Message(result.Problem, SyncSubject.Calendar, Strings.Current))));
    }
}
