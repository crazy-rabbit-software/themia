using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ScreenTimeApps.Tests;

/// <summary>
/// [S-3356] The app list's options on its properties menu, with the shared limits and hidden apps
/// ([S-3323]) over a stand-in tracker: the sample month, its preferences kept here.
/// </summary>
public sealed class ScreenTimeAppsMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    /// <summary>The sample history with preferences of its own, counting what is handed to it.</summary>
    internal sealed class Tracker(ScreenTimePreferences preferences) : IScreenTimeSource
    {
        private readonly SampleScreenTime _sample = new();

        public int Sets { get; private set; }

        public LocalDay Today => _sample.Today;

        public ScreenTimePreferences Preferences { get; private set; } = preferences;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public ScreenTimeDay Day(string? date) => _sample.Day(date);

        public IReadOnlyList<ScreenTimeDay> Range(int days) => _sample.Range(days);

        public void SetPreferences(ScreenTimePreferences preferences)
        {
            Preferences = preferences;
            Sets++;
        }
    }

    private WidgetMenuLines Build(ScreenTimeAppsConfig config, IScreenTimeSource? source)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        ScreenTimeAppsMenu.Lines(lines, config, source);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S3356_the_first_level_is_appearance_the_list_its_figures_then_the_shared_lines()
    {
        var lines = Build(new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Week }, new Tracker(new ScreenTimePreferences()));

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(
            [
                Str.Screentime_Cfg_Period.Text(),
                "|",
                ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Label.Text(),
                ScreenTimeAppsStr.Screentime_Metric_Average.Text(),
                ScreenTimeAppsStr.Screentime_Metric_Percent.Text(),
                "|",
                Str.WidgetsC_ScreenTime_Settings_Limits.Text(),
                Str.Screentime_Cfg_Hidden.Text(),
                "|",
                Str.WidgetsC_ScreenTime_Settings_Privacy.Text(),
            ],
            lines.Lines.Select(l => l.IsSeparator ? "|" : l.Text));
    }

    [Fact]
    public void S3356_a_day_has_no_average_and_no_tracker_says_so()
    {
        var lines = Build(new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Day }, null);

        Assert.DoesNotContain(lines.Lines, l => l.Text == ScreenTimeAppsStr.Screentime_Metric_Average.Text());
        Assert.DoesNotContain(lines.Lines, l => l.Text == Str.WidgetsC_ScreenTime_Settings_Limits.Text());
        Assert.True(Line(lines.Lines, Str.WidgetsC_ScreenTime_NotRecording.Text()).Note);
    }

    [Fact]
    public void S3356_the_period_sets_once_and_the_current_one_saves_nothing()
    {
        var config = new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Day };
        var period = Line(Build(config, null).Lines, Str.Screentime_Cfg_Period.Text());

        Line(period.Children!, Str.Screentime_Cfg_PeriodDay.Text()).Action!();
        Assert.Equal(0, _saves);

        Line(period.Children!, ScreenTimeRules.PeriodLabel(ScreenTimePeriod.Month)).Action!();
        Assert.Equal(ScreenTimePeriod.Month, config.Period);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3356_most_apps_reads_as_many_as_fit_at_zero_and_is_typed_in_range()
    {
        var config = new ScreenTimeAppsConfig();
        var most = Line(Build(config, null).Lines, ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Label.Text());
        Assert.Equal(ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Fit.Text(), most.Value);

        most.Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.NotNull(ask.Refuse("51"));
        Assert.NotNull(ask.Refuse("-1"));
        ask.Done("12");

        Assert.Equal(12, config.MaxApps);
        Assert.Equal(1, _saves);
        Assert.Equal("12", Line(Build(config, null).Lines, ScreenTimeAppsStr.WidgetsC_ScreenTime_Settings_Most_Label.Text()).Value);
    }

    [Fact]
    public void S3356_the_figures_switch_and_save_once()
    {
        var config = new ScreenTimeAppsConfig { Period = ScreenTimePeriod.Week, ShowAverage = true, ShowPercent = false };
        var lines = Build(config, null).Lines;

        Line(lines, ScreenTimeAppsStr.Screentime_Metric_Average.Text()).Action!();
        Line(lines, ScreenTimeAppsStr.Screentime_Metric_Percent.Text()).Action!();

        Assert.False(config.ShowAverage);
        Assert.True(config.ShowPercent);
        Assert.Equal(2, _saves);
    }

    [Fact]
    public void S3356_the_list_limits_each_app_never_the_day()
    {
        var tracker = new Tracker(ScreenTimePreferences.From(null, 0, [new("chrome", 5400)]));
        var limits = Line(Build(new ScreenTimeAppsConfig(), tracker).Lines, Str.WidgetsC_ScreenTime_Settings_Limits.Text()).Children!;

        Assert.DoesNotContain(limits, l => l.Text == Str.WidgetsC_ScreenTime_DailyLimit.Text());
        var chrome = Line(limits, "Google Chrome");
        Assert.Equal(ScreenTimeRules.FormatLimit(5400), chrome.Value);
        Assert.NotNull(Line(limits, Str.WidgetsC_ScreenTime_Settings_LimitApp.Text()).Search);
    }

    [Fact]
    public void S3356_an_app_limit_is_changed_and_removed_through_the_tracker()
    {
        var tracker = new Tracker(ScreenTimePreferences.From(null, 0, [new("chrome", 3600)]));
        var config = new ScreenTimeAppsConfig();
        var chrome = Line(Line(Build(config, tracker).Lines, Str.WidgetsC_ScreenTime_Settings_Limits.Text()).Children!, "Google Chrome").Children!;

        Line(chrome, Str.WidgetsC_ScreenTime_DailyLimit.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.NotNull(ask.Refuse("10"));
        Assert.NotNull(ask.Refuse("721"));
        ask.Done("90");
        Assert.Equal(5400, tracker.Preferences.AppLimits["chrome"]);

        Line(chrome, Str.Common_Remove.Text()).Action!();
        Assert.Empty(tracker.Preferences.AppLimits);
        Assert.Equal(2, tracker.Sets);
    }

    [Fact]
    public async Task S3356_limit_an_app_offers_the_known_apps_without_a_limit_and_adds_an_hour()
    {
        var tracker = new Tracker(ScreenTimePreferences.From(null, 0, [new("chrome", 3600)]));
        var limits = Line(Build(new ScreenTimeAppsConfig(), tracker).Lines, Str.WidgetsC_ScreenTime_Settings_Limits.Text()).Children!;
        var search = Line(limits, Str.WidgetsC_ScreenTime_Settings_LimitApp.Text()).Search!;

        var all = await search.Find(string.Empty, CancellationToken.None);
        Assert.DoesNotContain(all.Lines, l => l.Text == "Google Chrome");
        Assert.Contains(all.Lines, l => l.Text == "Visual Studio Code");

        var found = Assert.Single((await search.Find("spot", CancellationToken.None)).Lines);
        found.Action!();
        Assert.Equal(ScreenTimeMenuLines.NewAppLimitSeconds, tracker.Preferences.AppLimits["spotify"]);
        Assert.Equal(1, tracker.Sets);
    }

    [Fact]
    public void S3356_a_hidden_app_is_shown_again_through_the_tracker()
    {
        var tracker = new Tracker(ScreenTimePreferences.From(["spotify", "zz-gone"], 0, null));
        var hidden = Line(Build(new ScreenTimeAppsConfig(), tracker).Lines, Str.Screentime_Cfg_Hidden.Text()).Children!;

        Assert.Contains(hidden, l => l.Text == AppNames.Prettify("zz-gone"));
        Line(Line(hidden, "Spotify").Children!, Str.WidgetsC_ScreenTime_Settings_ShowAgain.Text()).Action!();

        Assert.Equal(["zz-gone"], tracker.Preferences.Hidden);
    }

    [Fact]
    public void S3356_without_hidden_apps_the_submenu_says_how_to_hide_one()
    {
        var hidden = Line(Build(new ScreenTimeAppsConfig(), new Tracker(new ScreenTimePreferences())).Lines, Str.Screentime_Cfg_Hidden.Text()).Children!;

        Assert.True(hidden[0].Note);
        Assert.Equal(Str.WidgetsC_ScreenTime_Settings_NoHidden.Text(), hidden[0].Text);
    }
}
