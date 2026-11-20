using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.MoonTimes.Tests;

/// <summary>[S-11125] [S-14695] The moon's times, a single metric each.</summary>
public sealed class MoonMetricTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 21, 0, 0, TimeSpan.FromHours(1));

    [Fact]
    public void S11125_each_time_is_one_value_of_the_sky_and_a_date_for_the_next_full_and_new_moon()
    {
        var rows = MoonTimeRules.For([MoonTimeKind.NextFull, MoonTimeKind.NextNew, MoonTimeKind.Rise], Now, 47.37, 8.54);

        Assert.Equal([MoonTimeKind.NextFull, MoonTimeKind.NextNew, MoonTimeKind.Rise], rows.Select(r => r.Kind));
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Value)));
    }

    [Fact]
    public void S14695_a_time_card_is_its_kind_and_its_menu_the_place()
    {
        Assert.Equal(MoonTimeKind.Set, new MoonsetConfig().Time);
        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new MoonriseModule()).MenuLines(menu, new MoonriseConfig());
        Assert.NotEmpty(menu.Lines);
    }
}
