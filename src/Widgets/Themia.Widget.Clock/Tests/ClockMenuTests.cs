using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>[S-1478] The Clock's options on its properties menu: what the style draws under Style, the zone as a search.</summary>
public sealed class ClockMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(ClockConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        ClockMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S1478_the_digital_clock_offers_seconds_date_and_text_size_under_style()
        => Assert.Equal(
            [ClockStr.Clock_SecondsLabel.Text(), ClockStr.Clock_DateLabel.Text(), ClockStr.ClockText_SizeLabel.Text()],
            Build(new ClockConfig { Style = ClockStyle.Digital }).StyleLines.Select(l => l.Text));

    [Fact]
    public void S1478_appearance_then_the_zone_and_the_offset_after_it_on_the_first_level()
    {
        var lines = Build(new ClockConfig { Style = ClockStyle.Digital });

        Assert.Equal(0, lines.CardAt);
        Assert.Collection(lines.Lines,
            divider => Assert.True(divider.IsSeparator),
            zone => Assert.Empty(zone.Search!.Fixed),
            offset => Assert.Equal(ClockStr.Clock_OffsetLabel.Text(), offset.Text));
    }

    [Fact]
    public async Task S1478_the_search_puts_the_machine_zone_first_and_a_pick_stores_the_city()
    {
        var config = new ClockConfig();
        var search = Build(config).Lines.First(l => l.Search is not null).Search!;

        var answer = await search.Find("tokyo", CancellationToken.None);
        Assert.True(answer.Lines[0].Checked);
        var tokyo = answer.Lines.First(l => l.Text.StartsWith("Tokyo", StringComparison.Ordinal));
        Assert.False(tokyo.Checked);

        tokyo.Action!();
        Assert.Equal("Asia/Tokyo", config.TimeZoneId);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S1476_the_text_size_is_offered_only_while_a_line_under_the_time_is_drawn()
    {
        static bool Offered(ClockConfig c) => c.Offers(ClockSetting.TextSize);

        Assert.True(Offered(new ClockConfig { ShowDate = true }));
        Assert.False(Offered(new ClockConfig { ShowDate = false }));
        Assert.True(Offered(new ClockConfig { ShowDate = false, OffsetMode = ClockOffsetMode.Utc }));
        Assert.True(Offered(new ClockConfig { ShowDate = false, TimeZoneId = "Asia/Tokyo" }));
        Assert.False(Offered(new ClockConfig { Style = ClockStyle.Minimal, ShowDate = true }));
    }

    [Fact]
    public void S1476_with_nothing_under_the_time_the_style_lines_lose_the_text_size()
        => Assert.Equal(
            [ClockStr.Clock_SecondsLabel.Text(), ClockStr.Clock_DateLabel.Text()],
            Build(new ClockConfig { Style = ClockStyle.Digital, ShowDate = false }).StyleLines.Select(l => l.Text));

    [Fact]
    public void S1478_the_seconds_switch_turns_them_the_other_way()
    {
        var config = new ClockConfig { Style = ClockStyle.Digital, ShowSeconds = false };
        Build(config).StyleLines.Single(l => l.Text == ClockStr.Clock_SecondsLabel.Text()).Action!();

        Assert.True(config.ShowSeconds);
    }
}
