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
        var lines = new WidgetMenuLines(() => _saves++);
        ClockMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S1478_the_digital_clock_offers_seconds_under_style_and_no_date_and_no_size()
        => Assert.Equal(
            [ClockStr.Clock_SecondsLabel.Text()],
            Build(new ClockConfig { Style = ClockStyle.Digital }).StyleLines.Select(l => l.Text));

    [Fact]
    public void S1478_appearance_then_the_zone_and_the_offset_after_it_and_no_link_to_the_time_format()
    {
        var lines = Build(new ClockConfig { Style = ClockStyle.Digital });

        Assert.Collection(lines.Lines,
            divider => Assert.True(divider.IsSeparator),
            zone => Assert.Empty(zone.Search!.Fixed),
            // [S-3590] The 12- or 24-hour clock is ⚙'s: the clock just follows it, no link row ([S-3749]).
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
    public void S6741_the_clock_has_no_text_size_of_its_own_its_line_is_its_steps()
    {
        // [S-1476] retired: the line under the time follows the readout's step ([S-6701]); the text size is ⚙'s.
        Assert.Equal(
            [ClockStr.Clock_SecondsLabel.Text()],
            Build(new ClockConfig { Style = ClockStyle.Digital, TimeZoneId = "Asia/Tokyo" }).StyleLines.Select(l => l.Text));
    }

    [Fact]
    public void S1478_the_seconds_switch_turns_them_the_other_way()
    {
        var config = new ClockConfig { Style = ClockStyle.Digital, ShowSeconds = false };
        Build(config).StyleLines.Single(l => l.Text == ClockStr.Clock_SecondsLabel.Text()).Action!();

        Assert.True(config.ShowSeconds);
    }
}
