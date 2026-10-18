using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3321] [S-3322] The rows every screen-time widget's page has: the period, then the limits and the
/// hidden apps they share, one set for the machine written through the tracker
/// (<see cref="SettingsHost.ScreenTime"/>) rather than the config.
/// </summary>
public static class ScreenTimeSettingsRows
{
    /// <summary>[S-3321] One day, or a rolling window that ends today.</summary>
    public static Border PeriodRow(ScreenTimePeriod period, Action<ScreenTimePeriod> changed)
        => FluentLayout.Row(Glyphs.Calendar, Str.Screentime_Cfg_Period.Text(), Str.WidgetsC_ScreenTime_Settings_Period_Desc.Text(),
            FluentLayout.Choice(
                [(ScreenTimePeriod.Day, Str.Screentime_Cfg_PeriodDay.Text()), (ScreenTimePeriod.Week, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week)), (ScreenTimePeriod.Month, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month))],
                ScreenTimeRules.ClampPeriod(period),
                changed));

    /// <summary>
    /// [S-3322] The limits and the hidden apps, on every screen-time widget's page: one set for the
    /// machine, edited from whichever page is open, saved with the history by the tracker.
    /// </summary>
    /// <remarks>[S-6074] Of the limits, only the kind the widget draws with: the day's or each app's.</remarks>
    public static void AddShared(List<FrameworkElement> rows, IShownSettings<ScreenTimeSetting> widget)
    {
        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Settings_Limits.Text()));

        if (SettingsHost.ScreenTime is not { } source)
        {
            rows.Add(FluentLayout.Note(Str.WidgetsC_ScreenTime_NotRecording.Text()));
            rows.Add(FluentLayout.Note(Str.WidgetsC_ScreenTime_Settings_Privacy.Text()));
            return;
        }

        rows.Add(FluentLayout.Note(Str.WidgetsC_ScreenTime_Settings_Shared.Text()));
        AddLimits(rows, source, widget);
        AddHiddenApps(rows, source);
        rows.Add(FluentLayout.Note(Str.WidgetsC_ScreenTime_Settings_Privacy.Text()));
    }

    /// <summary>
    /// [S-457] Daily limits, edited in quarter hours and stored in seconds. A limit of zero is no
    /// limit at all, never one that everything instantly exceeds.
    /// </summary>
    private static void AddLimits(List<FrameworkElement> rows, IScreenTimeSource source, IShownSettings<ScreenTimeSetting> widget)
    {
        if (widget.Offers(ScreenTimeSetting.DailyLimit))
        {
            rows.Add(FluentLayout.Row(Glyphs.Timer, Str.WidgetsC_ScreenTime_DailyLimit.Text(), Str.WidgetsC_ScreenTime_Settings_DailyLimit_Desc.Format(("zero", 0)),
                FluentLayout.Slider(Math.Clamp(source.Preferences.GoalSeconds / 60, 0, 1440), 0, 1440, 15, Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
                    value =>
                    {
                        var next = source.Preferences.Copy();
                        next.GoalSeconds = (long)Math.Round(value) * 60;
                        source.SetPreferences(next);
                    })));
        }

        if (!widget.Offers(ScreenTimeSetting.AppLimits))
        {
            return;
        }

        // The apps the history knows, so a limit is picked rather than typed and mistyped.
        var known = KnownApps();

        // The editor works on rows; every change writes the whole set back through the tracker.
        var limits = source.Preferences.AppLimits
            .OrderBy(limit => limit.Key, StringComparer.Ordinal)
            .Select(limit => new ScreenTimeAppLimit { App = limit.Key, Seconds = (int)Math.Min(int.MaxValue, limit.Value) })
            .ToList();

        void Save()
        {
            var current = source.Preferences;
            source.SetPreferences(ScreenTimePreferences.From(
                current.Hidden,
                current.GoalSeconds,
                limits.Select(limit => new KeyValuePair<string, long>(limit.App, limit.Seconds))));
        }

        FluentLayout.ListEditor(rows, new ListEditorOptions<ScreenTimeAppLimit>
        {
            Items = limits,
            Save = Save,
            EmptyText = Str.WidgetsC_ScreenTime_Settings_NoAppLimits.Text(),
            AddTitle = Str.WidgetsC_ScreenTime_Settings_LimitApp.Text(),
            AddAction = Str.Common_Add.Text(),
            Create = () => new ScreenTimeAppLimit { App = known.Count > 0 ? known[0].App : string.Empty, Seconds = 3600 },
            Body = limit => LimitEditor(limit, known, Save),
        });
    }

    private static FrameworkElement LimitEditor(ScreenTimeAppLimit limit, IReadOnlyList<(string App, string Name)> known, Action save)
    {
        var app = new ComboBox { MinWidth = 180 };
        var options = known.ToList();

        // An app no longer in the history keeps its row rather than silently becoming another.
        if (limit.App.Length > 0 && options.All(o => o.App != limit.App))
        {
            options.Insert(0, (limit.App, AppNames.Prettify(limit.App)));
        }

        foreach (var (key, name) in options)
        {
            app.Items.Add(new ComboBoxItem { Content = name, Tag = key });
        }

        app.SelectedIndex = options.FindIndex(o => o.App == limit.App);
        app.SelectionChanged += (_, _) =>
        {
            if (app.SelectedItem is ComboBoxItem { Tag: string key })
            {
                limit.App = key;
                save();
            }
        };

        var minutes = FluentLayout.Slider(Math.Clamp(limit.Seconds / 60, 15, 720), 15, 720, 15, Str.WidgetsC_Pomodoro_MinutesUnit.Text(),
            value => { limit.Seconds = (int)Math.Round(value) * 60; save(); });

        return new StackPanel { Spacing = Fluent.Space1, Children = { app, minutes } };
    }

    /// <summary>
    /// [S-455] [S-3322] The apps hidden from every screen-time widget, each with a way back. Keys
    /// only: no durations, no dates.
    /// </summary>
    private static void AddHiddenApps(List<FrameworkElement> rows, IScreenTimeSource source)
    {
        rows.Add(FluentLayout.Header(Str.Screentime_Cfg_Hidden.Text()));

        var host = new StackPanel { Spacing = Fluent.Space1 };
        rows.Add(host);

        var names = KnownApps().ToDictionary(a => a.App, a => a.Name, StringComparer.Ordinal);

        void Rebuild()
        {
            host.Children.Clear();

            if (source.Preferences.Hidden.Count == 0)
            {
                host.Children.Add(FluentLayout.Note(Str.WidgetsC_ScreenTime_Settings_NoHidden.Text()));
                return;
            }

            foreach (var key in source.Preferences.Hidden.Order(StringComparer.Ordinal).ToList())
            {
                var show = FluentLayout.IconCommand(Glyphs.View, Str.WidgetsC_ScreenTime_Settings_ShowAgain.Text());
                show.Click += (_, _) =>
                {
                    var next = source.Preferences.Copy();
                    next.Hidden.Remove(key);
                    source.SetPreferences(next);
                    Rebuild();
                };

                host.Children.Add(FluentLayout.Row(Glyphs.Hidden, names.GetValueOrDefault(key) ?? AppNames.Prettify(key), key, show));
            }
        }

        Rebuild();
    }

    /// <summary>Apps in the last month of history, busiest first, as (key, name).</summary>
    private static List<(string App, string Name)> KnownApps()
    {
        if (SettingsHost.ScreenTime is not { } source)
        {
            return [];
        }

        return [.. ScreenTimeRules.Aggregate(source.Range(30), new HashSet<string>()).Select(a => (a.App, a.Name))];
    }
}
