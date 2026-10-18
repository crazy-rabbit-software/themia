using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.ScreenTimeDonut.Tests;

/// <summary>[S-3342] The donut's ladder. Moved with the widget from Core's ScreenTimeLayoutTests, unchanged in what they assert (the ladder is ScreenTimeDonutLayout's now).</summary>
public sealed class ScreenTimeDonutTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = 44;

    private static WidgetArea Content(WidgetTier tier) => new(tier.Width, tier.Height - Header);

    public static TheoryData<string> Tiers()
    {
        var data = new TheoryData<string>();
        foreach (var tier in WidgetTiers.All)
        {
            data.Add(tier.Name);
        }

        return data;
    }

    private static WidgetTier Tier(string name) => WidgetTiers.All.First(t => t.Name == name);

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S3342_the_ring_fits_the_tile(string name)
    {
        var area = Content(Tier(name));
        var plan = ScreenTimeDonutLayout.Donut(area);

        Assert.True(plan.Ring <= area.Height, $"{name}: ring {plan.Ring} taller than {area.Height}");
        Assert.True(plan.Ring <= area.Width - (2 * ScreenTimeLayout.Inset) || plan.Ring <= ScreenTimeDonutLayout.MinRing);
        Assert.Equal(ScreenTimeDonutLayout.Thickness(plan.Ring), plan.Thickness);
    }

    [Fact]
    public void S3342_a_larger_tile_draws_a_larger_ring_and_names_more_in_the_hole()
    {
        var s = ScreenTimeDonutLayout.Donut(Content(Tier("S")));
        var m = ScreenTimeDonutLayout.Donut(Content(Tier("M")));
        var xl = ScreenTimeDonutLayout.Donut(Content(Tier("XL")));

        Assert.True(m.Ring > s.Ring);
        Assert.True(xl.Ring > m.Ring);
        Assert.False(s.Detail);
        Assert.True(m.Detail);
    }
}
