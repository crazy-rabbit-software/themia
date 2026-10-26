using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.ScreenTimeDonut.Tests;

/// <summary>[S-3345] The donut's options on its properties menu: the period, then the shared limits ([S-3323]): the day's, never an app's.</summary>
public sealed class ScreenTimeDonutMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    /// <summary>The sample history with preferences of its own.</summary>
    private sealed class Tracker(ScreenTimePreferences preferences) : IScreenTimeSource
    {
        private readonly SampleScreenTime _sample = new();

        public LocalDay Today => _sample.Today;

        public ScreenTimePreferences Preferences { get; private set; } = preferences;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public ScreenTimeDay Day(string? date) => _sample.Day(date);

        public IReadOnlyList<ScreenTimeDay> Range(int days) => _sample.Range(days);

        public void SetPreferences(ScreenTimePreferences preferences) => Preferences = preferences;
    }

    private WidgetMenuLines Build(ScreenTimeDonutConfig config, IScreenTimeSource? source)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        ScreenTimeDonutMenu.Lines(lines, config, source);
        return lines;
    }

    private static ItemMenuEntry Line(IEnumerable<ItemMenuEntry> lines, string text) => Assert.Single(lines, l => l.Text == text);

    [Fact]
    public void S3345_the_first_level_is_appearance_the_period_then_the_shared_lines()
    {
        var lines = Build(new ScreenTimeDonutConfig(), new Tracker(new ScreenTimePreferences()));

        Assert.Equal(
            [Str.Screentime_Cfg_Period.Text(), "|", Str.WidgetsC_ScreenTime_Settings_Limits.Text(), Str.Screentime_Cfg_Hidden.Text(), "|", Str.WidgetsC_ScreenTime_Settings_Privacy.Text()],
            lines.Lines.Select(l => l.IsSeparator ? "|" : l.Text));
        Assert.Equal(3, Line(lines.Lines, Str.Screentime_Cfg_Period.Text()).Children!.Count);
    }

    [Fact]
    public void S3345_the_donut_limits_the_day_never_an_app_and_off_reads_as_off()
    {
        var limits = Line(Build(new ScreenTimeDonutConfig(), new Tracker(new ScreenTimePreferences())).Lines, Str.WidgetsC_ScreenTime_Settings_Limits.Text()).Children!;

        Assert.Equal(Str.Toggle_Off.Text(), Line(limits, Str.WidgetsC_ScreenTime_DailyLimit.Text()).Value);
        Assert.DoesNotContain(limits, l => l.Text == Str.WidgetsC_ScreenTime_Settings_LimitApp.Text());
    }

    [Fact]
    public void S3345_the_daily_limit_is_typed_in_quarter_hours_and_stored_in_seconds()
    {
        var tracker = new Tracker(ScreenTimePreferences.From(null, 4 * 3600, null));
        var limits = Line(Build(new ScreenTimeDonutConfig(), tracker).Lines, Str.WidgetsC_ScreenTime_Settings_Limits.Text()).Children!;

        Line(limits, Str.WidgetsC_ScreenTime_DailyLimit.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.Equal("240", ask.Initial);
        Assert.NotNull(ask.Refuse("1441"));
        ask.Done("100");

        Assert.Equal(105 * 60, tracker.Preferences.GoalSeconds);
        Assert.Equal(1, _saves);

        ask.Done("0");
        Assert.Equal(0, tracker.Preferences.GoalSeconds);
    }
}
