using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3321] [S-3323] The lines every screen-time widget's properties menu ([S-3719]) has: the period,
/// then the limits and the hidden apps they share, one set for the machine written through the
/// tracker (<see cref="IScreenTimeSource.SetPreferences"/>) rather than the config.
/// </summary>
public static class ScreenTimeMenuLines
{
    /// <summary>[S-457] A new app limit starts at an hour, as the page's did.</summary>
    public const int NewAppLimitSeconds = 3600;

    /// <summary>[S-3321] One day, or a rolling window that ends today.</summary>
    public static ItemMenuEntry Period(WidgetMenuLines menu, ScreenTimePeriod period, Action<ScreenTimePeriod> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return menu.Choice(Glyphs.Calendar, Str.Screentime_Cfg_Period.Text(),
            [
                (ScreenTimePeriod.Day, Str.Screentime_Cfg_PeriodDay.Text()),
                (ScreenTimePeriod.Week, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week)),
                (ScreenTimePeriod.Month, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month)),
            ],
            ScreenTimeRules.ClampPeriod(period), set);
    }

    /// <summary>
    /// [S-3323] The limits and the hidden apps, on every screen-time widget's menu: one set for the
    /// machine, changed from whichever widget's menu is open, saved with the history by
    /// <paramref name="source"/> (none: the history is not recorded, and the lines say so). Of the
    /// limits only the kind the widget draws with ([S-6074]): the day's or each app's.
    /// </summary>
    public static void AddShared(WidgetMenuLines menu, IShownSettings<ScreenTimeSetting> widget, IScreenTimeSource? source)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(widget);

        if (source is null)
        {
            menu.Lines.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_NotRecording.Text()));
            menu.Lines.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_Privacy.Text()));
            return;
        }

        var limits = new List<ItemMenuEntry>();
        if (widget.Offers(ScreenTimeSetting.DailyLimit))
        {
            limits.Add(DailyLimit(menu, source));
        }

        if (widget.Offers(ScreenTimeSetting.AppLimits))
        {
            if (limits.Count > 0)
            {
                limits.Add(ItemMenuEntry.Separator);
            }

            limits.AddRange(AppLimits(menu, source));
        }

        limits.Add(ItemMenuEntry.Separator);
        limits.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_Shared.Text()));
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Timer, Str.WidgetsC_ScreenTime_Settings_Limits.Text(), limits));

        var hidden = HiddenApps(menu, source);
        hidden.Add(ItemMenuEntry.Separator);
        hidden.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_Shared.Text()));
        menu.Lines.Add(WidgetMenuLines.Submenu(Glyphs.Hidden, Str.Screentime_Cfg_Hidden.Text(), hidden));

        menu.Lines.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_Privacy.Text()));
    }

    /// <summary>
    /// [S-457] The daily limit on the whole, in minutes (quarter hours up to a day, typed: more steps
    /// than dots), stored in seconds. Zero is no limit at all, never one everything instantly exceeds,
    /// and the line says Off.
    /// </summary>
    private static ItemMenuEntry DailyLimit(WidgetMenuLines menu, IScreenTimeSource source)
    {
        var minutes = (int)Math.Clamp(source.Preferences.GoalSeconds / 60, 0, 1440);
        var line = menu.Number(Glyphs.Timer, Str.WidgetsC_ScreenTime_DailyLimit.Text(), minutes, 0, 1440, 15, Minutes,
            value => Change(source, next => next.GoalSeconds = value * 60L));
        return minutes == 0 ? line with { Value = Str.Toggle_Off.Text() } : line;
    }

    /// <summary>
    /// [S-457] Each app's limit, an app's line with its limit at the end (its submenu: the limit in
    /// quarter hours up to 12 hours, Remove), then Limit an app: a search over the apps the history
    /// knows, so a limit is picked rather than typed and mistyped.
    /// </summary>
    private static List<ItemMenuEntry> AppLimits(WidgetMenuLines menu, IScreenTimeSource source)
    {
        var known = KnownApps(source);
        var names = known.ToDictionary(a => a.App, a => a.Name, StringComparer.Ordinal);
        var limits = source.Preferences.AppLimits.OrderBy(limit => limit.Key, StringComparer.Ordinal).ToList();

        var lines = new List<ItemMenuEntry>();
        if (limits.Count == 0)
        {
            lines.Add(WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_NoAppLimits.Text()));
        }

        foreach (var (app, seconds) in limits)
        {
            var minutes = (int)Math.Clamp(seconds / 60, 15, 720);
            lines.Add(WidgetMenuLines.Submenu(null, names.GetValueOrDefault(app) ?? AppNames.Prettify(app),
                [
                    menu.Number(Glyphs.Timer, Str.WidgetsC_ScreenTime_DailyLimit.Text(), minutes, 15, 720, 15, Minutes,
                        value => Change(source, next => next.AppLimits[app] = value * 60L)),
                    ItemMenuEntry.Separator,
                    menu.Command(Glyphs.Delete, Str.Common_Remove.Text(), () => Change(source, next => next.AppLimits.Remove(app))),
                ]) with { Value = ScreenTimeRules.FormatLimit(seconds) });
        }

        Task<MenuSearchAnswer> Find(string typed, CancellationToken cancel)
        {
            var query = typed.Trim();
            var limited = source.Preferences.AppLimits;
            return Task.FromResult(new MenuSearchAnswer([.. known
                .Where(a => !limited.ContainsKey(a.App) && (query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .Select(a => menu.Command(null, a.Name, () => Change(source, next => next.AppLimits[a.App] = NewAppLimitSeconds)))]));
        }

        lines.Add(WidgetMenuLines.Search(Glyphs.Add, Str.WidgetsC_ScreenTime_Settings_LimitApp.Text(), null,
            new MenuSearch(Str.WidgetsC_ScreenTime_Settings_LimitApp.Text(), Find, [])));
        return lines;
    }

    /// <summary>
    /// [S-455] [S-3322] The apps hidden from every screen-time widget, each with Show again. Keys only:
    /// no durations, no dates.
    /// </summary>
    private static List<ItemMenuEntry> HiddenApps(WidgetMenuLines menu, IScreenTimeSource source)
    {
        var hidden = source.Preferences.Hidden.Order(StringComparer.Ordinal).ToList();
        if (hidden.Count == 0)
        {
            return [WidgetMenuLines.Note(Str.WidgetsC_ScreenTime_Settings_NoHidden.Text())];
        }

        var names = KnownApps(source).ToDictionary(a => a.App, a => a.Name, StringComparer.Ordinal);
        return [.. hidden.Select(key => WidgetMenuLines.Submenu(Glyphs.Hidden, names.GetValueOrDefault(key) ?? AppNames.Prettify(key),
            [menu.Command(Glyphs.View, Str.WidgetsC_ScreenTime_Settings_ShowAgain.Text(), () => Change(source, next => next.Hidden.Remove(key)))]))];
    }

    /// <summary>
    /// [S-3322] One change to the shared preferences: made on a copy, cleaned as a file is read, and
    /// handed to the tracker whole. The menu's save after it stores nothing new in the config; it is
    /// what builds the menu again.
    /// </summary>
    private static void Change(IScreenTimeSource source, Action<ScreenTimePreferences> change)
    {
        var next = source.Preferences.Copy();
        change(next);
        source.SetPreferences(ScreenTimePreferences.From(next.Hidden, next.GoalSeconds, next.AppLimits));
    }

    private static string Minutes(int minutes) => LocaleFormat.Integer(minutes) + Str.WidgetsC_Pomodoro_MinutesUnit.Text();

    /// <summary>Apps in the last month of history, busiest first, as (key, name).</summary>
    private static List<(string App, string Name)> KnownApps(IScreenTimeSource source)
        => [.. ScreenTimeRules.Aggregate(source.Range(30), new HashSet<string>()).Select(a => (a.App, a.Name))];
}
