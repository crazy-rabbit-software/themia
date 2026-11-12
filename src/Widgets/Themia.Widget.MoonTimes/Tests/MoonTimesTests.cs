using Themia.Core.Astronomy;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.MoonTimes.Tests;

/// <summary>[S-11125]..[S-11127] The moon times: its rows, its pick, its sizing and its menu.</summary>
public sealed class MoonTimesTests
{
    private static readonly WidgetDescriptor Kind = new MoonTimesModule().Descriptor;

    private static readonly DateTimeOffset Evening = new(2026, 10, 12, 21, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void S11125_a_new_card_shows_moonrise_moonset_and_the_next_full_moon()
        => Assert.Equal([MoonTimeKind.Rise, MoonTimeKind.Set, MoonTimeKind.NextFull], new MoonTimesConfig().Times);

    [Fact]
    public void S11125_the_next_full_and_new_moon_are_each_the_first_after_now_on_the_reading_s_lunation()
    {
        var full = MoonPhase.NextFull(Evening);
        var fresh = MoonPhase.NextNew(Evening);

        Assert.True(full.At > Evening && fresh.At > Evening);
        Assert.True(full.At - Evening < TimeSpan.FromDays(29.6) && fresh.At - Evening < TimeSpan.FromDays(29.6));
        Assert.Equal(0.5, MoonPhase.At(full.At).Phase, 3);
        Assert.Equal(MoonPhase.NextMajor(Evening), full.At < fresh.At ? full : fresh);
    }

    [Fact]
    public void S11125_each_picked_row_is_one_row_in_order_and_one_of_rise_and_set_is_next()
    {
        var rows = MoonTimeRules.For([MoonTimeKind.NextNew, MoonTimeKind.Rise, MoonTimeKind.Set], Evening, 47.37, 8.54);

        Assert.Equal([MoonTimeKind.NextNew, MoonTimeKind.Rise, MoonTimeKind.Set], rows.Select(r => r.Kind));
        Assert.Equal(1, rows.Count(r => r.Next));
        Assert.False(rows[0].Next);
        Assert.NotNull(rows[0].ShortValue);
    }

    [Fact]
    public void S11126_its_height_is_a_28_row_a_time_under_the_header_and_dragging_sets_the_rows()
    {
        var config = new MoonTimesConfig();
        Assert.Equal(36 + (3 * 28) + 12, Kind.Sizing(config).Natural.Height);

        var all = (MoonTimesConfig)Kind.SizeSets!(config, new SizeDip(240, 36 + (4 * 28) + 12));
        Assert.Equal([MoonTimeKind.Rise, MoonTimeKind.Set, MoonTimeKind.NextFull, MoonTimeKind.NextNew], all.Times);
    }

    [Fact]
    public void S11127_the_menu_is_the_rows_then_the_place()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        new MoonTimesModule().MenuLines(menu, new MoonTimesConfig());

        Assert.Equal(MoonTimesStr.MoonTimes_Menu_Times.Text(), menu.Lines[0].Text);
        Assert.Contains(menu.Lines, l => l.Search is not null);
    }
}
