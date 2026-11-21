using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5126] [S-11140] [S-14940]..[S-14943] The rain family: the graph's one layout, the sentence and the total.</summary>
public sealed class RainNextHourTests
{
    private static readonly IWidgetModule[] Modules = [new RainOutlookModule(), new RainNextHourModule(), new RainTotalModule()];

    private static readonly DateTimeOffset Now = new(2026, 11, 21, 11, 5, 0, TimeSpan.Zero);

    /// <summary>A nowcast from the quarter under way: <paramref name="amounts"/> a quarter each.</summary>
    private static RainOutlook Outlook(params double[] amounts)
    {
        var start = new DateTimeOffset(2026, 11, 21, 11, 0, 0, TimeSpan.Zero);
        var steps = amounts.Select((mm, i) => new RainStep(start.AddMinutes(15 * (i + 1)), mm)).ToList();
        return RainRules.Outlook(new RainNowcast(steps, []), Now);
    }

    [Fact]
    public void S11140_S14941_the_graph_is_the_columns_under_the_inset_and_the_ends_line_one_height_220_to_420_wide()
    {
        var sizing = RainLayout.Sizing();

        Assert.Equal(12 + (48 + 4 + 16) + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((220.0, 300.0, 420.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S5126_only_rain_to_tell_of_draws_the_bars()
    {
        Assert.True(RainLayout.Warns(RainHeadline.StartsIn));
        Assert.True(RainLayout.Warns(RainHeadline.StopsIn));
        Assert.True(RainLayout.Warns(RainHeadline.Continues));
        Assert.False(RainLayout.Warns(RainHeadline.Dry));
        Assert.False(RainLayout.Warns(RainHeadline.Outdated));
    }

    [Fact]
    public void S14940_three_widgets_in_the_rain_family_the_graph_keeping_the_old_id()
    {
        Assert.Equal(["rainOutlook", "rainNextHour", "rainTotal"], Modules.Select(m => m.Descriptor.Id.Value));
        Assert.All(Modules, m => Assert.Equal(AddFolderId.Rain, m.Descriptor.Add.Shelf));
        Assert.Equal([0, 1, 2], Modules.Select(m => m.Descriptor.Add.Rank));
        Assert.Equal(typeof(RainNextHourConfig), Modules[1].Descriptor.ConfigType);
    }

    [Fact]
    public void S14940_every_part_asks_for_the_same_nowcast_of_its_place_and_a_copy_keeps_its_kind()
    {
        RainConfig[] configs = [new RainOutlookConfig(), new RainNextHourConfig(), new RainTotalConfig()];
        foreach (var config in configs)
        {
            Assert.Null(config.Interest());
            config.Latitude = 47.37;
            config.Longitude = 8.54;
            Assert.Equal(new RainInterest(47.37, 8.54, TimeSpan.FromMinutes(15)), config.Interest(TimeSpan.FromMinutes(15)));
            var copy = (RainConfig)config.Clone();
            Assert.IsType(config.GetType(), copy);
            Assert.Equal(47.37, copy.Latitude);
        }
    }

    [Fact]
    public void S14942_the_sentence_is_the_headline_wrapping_to_two_lines_with_no_lead()
    {
        var kind = RainKinds.Outlook;
        Assert.Equal((2, false), (kind.Lines, kind.Lead));
        Assert.Same(TextOpens.Weather, kind.Opens);

        var starts = RainOutlookView.Shown(Outlook(0, 0.4, 0.6, 0, 0, 0, 0, 0), "Set a place", ["stale"]);
        Assert.Equal(("Rain in 15 min", true), (starts.Text, starts.Available));
        Assert.Equal(["stale"], starts.Problems);
    }

    [Fact]
    public void S14942_no_answer_says_why_and_a_window_too_short_says_so_both_unavailable()
    {
        var none = RainOutlookView.Shown(null, "Set a place", []);
        Assert.Equal(("Set a place", false), (none.Text, none.Available));

        var outdated = RainOutlookView.Shown(Outlook(0.2), "Set a place", []);
        Assert.Equal(RainHeadline.Outdated, Outlook(0.2).Headline);
        Assert.False(outdated.Available);
    }

    [Fact]
    public void S14943_the_total_is_the_windows_millimetres_a_tenth_under_ten_and_none_dry()
    {
        Assert.Equal(new MetricValue("1.1 mm", 1.1), RainText.Total(Locale.En, Outlook(0, 0.4, 0.7, 0, 0, 0, 0, 0)));
        Assert.Equal("1,2 mm", RainText.Total(Locale.De, Outlook(0, 0.4, 0.8, 0, 0, 0, 0, 0)).Text);
        Assert.Equal("12 mm", RainText.Total(Locale.En, Outlook(6, 6, 0, 0, 0, 0, 0, 0)).Text);
        Assert.Equal(new MetricValue("0 mm", 0.0), RainText.Total(Locale.En, Outlook(0, 0, 0, 0, 0, 0, 0, 0)));
        Assert.Equal(MetricValue.Unread, RainText.Total(Locale.En, Outlook(0.2)));
    }

    [Fact]
    public void S14943_the_total_is_a_single_metric_with_no_trend_opening_the_forecast()
    {
        Assert.Equal(MetricHistory.None, RainKinds.Total.History);
        Assert.Same(MetricOpens.Weather, RainKinds.Total.Opens);
        Assert.Equal(MetricRules.Sizing.Natural, Modules[2].Descriptor.Sizing(new RainTotalConfig()).Natural);
    }
}
