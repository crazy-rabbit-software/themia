using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// Settings rows for the three screen-time widgets (the GPU meter's are its module's,
/// the mixer's in <c>WidgetSettings.Mixer.cs</c>).
/// <para>
/// Same shape as the rest of <see cref="WidgetSettings"/>: each control writes straight to the live
/// config and saves, because the widget is on the desktop behind the window and watching it change
/// is the point. Each page has its own widget's rows, then the limits and hidden apps every
/// screen-time widget shares ([S-3322]), which write through the tracker rather than the config.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    /// <summary>[S-3330] The chart: a week or a month, as bars or a calendar; then the shared rows.</summary>
    public static void ScreenTimeChart(List<FrameworkElement> rows, ScreenTimeChartConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Chart_Label.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Screentime_Cfg_Period.Text(), Str.WidgetsC_ScreenTime_Settings_ChartPeriod_Desc.Text(),
            FluentLayout.Choice(
                [(ScreenTimePeriod.Week, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Week)), (ScreenTimePeriod.Month, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month))],
                ScreenTimeRules.ChartPeriod(config.Period),
                value => { config.Period = value; save(); })));

        rows.Add(FluentLayout.Row(Glyphs.Palette, Str.Screentime_Cfg_Style.Text(), Str.WidgetsC_ScreenTime_Settings_ChartStyle_Desc.Text(),
            FluentLayout.Choice(
                [(ScreenTimeChartStyle.Bars, Str.Screentime_Cfg_StyleBars.Text()), (ScreenTimeChartStyle.Heatmap, Str.WidgetsC_ScreenTime_Settings_StyleCalendar.Text())],
                ScreenTimeRules.ClampChartStyle(config.Style),
                value => { config.Style = value; save(); })));

        AddShared(rows, config);
    }

    /// <summary>[S-3340] The donut: the period; then the shared rows.</summary>
    public static void ScreenTimeDonut(List<FrameworkElement> rows, ScreenTimeDonutConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Donut_Label.Text()));
        rows.Add(PeriodRow(config.Period, value => { config.Period = value; save(); }));
        AddShared(rows, config);
    }

    /// <summary>[S-3350] The app list: the period, how many apps, the figures beside each; then the shared rows.</summary>
    public static void ScreenTimeApps(List<FrameworkElement> rows, ScreenTimeAppsConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Apps_Label.Text()));
        rows.Add(PeriodRow(config.Period, value => { config.Period = value; save(); }));

        // [S-1211] A ceiling over what the tile has room for.
        rows.Add(FluentLayout.Row(Glyphs.Count, Str.WidgetsC_ScreenTime_Settings_Most_Label.Text(),
            Str.WidgetsC_ScreenTime_Settings_Most_Desc.Format(("zero", 0)),
            FluentLayout.Slider(ScreenTimeRules.EffectiveMaxApps(config.MaxApps), 0, ScreenTimeRules.MaxAppsLimit, 1, string.Empty,
                value => { config.MaxApps = (int)Math.Round(value); save(); }),
            "apps listed", "max apps"));

        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Settings_Figures.Text()));

        // [S-6074] An average per day needs more than one day.
        if (config.Offers(ScreenTimeSetting.Average))
        {
            rows.Add(FluentLayout.Row(Glyphs.Hours, Str.Screentime_Metric_Average.Text(), Str.WidgetsC_ScreenTime_Settings_Average_Desc.Text(),
                FluentLayout.Toggle(config.ShowAverage, value => { config.ShowAverage = value; save(); })));
        }

        rows.Add(FluentLayout.Row(Glyphs.Count, Str.Screentime_Metric_Percent.Text(), Str.WidgetsC_ScreenTime_Settings_Share_Desc.Text(),
            FluentLayout.Toggle(config.ShowPercent, value => { config.ShowPercent = value; save(); })));

        AddShared(rows, config);
    }

    /// <summary>[S-3321] One day, or a rolling window that ends today.</summary>
    private static Border PeriodRow(ScreenTimePeriod period, Action<ScreenTimePeriod> changed)
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
    private static void AddShared(List<FrameworkElement> rows, IShownSettings<ScreenTimeSetting> widget)
    {
        rows.Add(FluentLayout.Header(Str.WidgetsC_ScreenTime_Settings_Limits.Text()));

        if (AppServices.ScreenTime is not { } source)
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
        if (AppServices.ScreenTime is not { } source)
        {
            return [];
        }

        return [.. ScreenTimeRules.Aggregate(source.Range(30), new HashSet<string>()).Select(a => (a.App, a.Name))];
    }
}
