using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.ScreenTimeDonut.Tests;

/// <summary>[S-3342] The donut's ladder: one ring in the family's track inside the card's insets.</summary>
public sealed class ScreenTimeDonutTests
{
    /// <summary>The card's header, which the content area is the card less.</summary>
    private const double Header = FamilySpace.ContentTop;

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
    public void S3342_the_ring_fits_the_tile_inside_the_cards_insets(string name)
    {
        var area = Content(Tier(name));
        var plan = ScreenTimeDonutLayout.Donut(area);

        Assert.True(plan.Ring <= area.Height - FamilySpace.Inset, $"{name}: ring {plan.Ring} taller than {area.Height}");
        Assert.True(plan.Ring <= area.Width - (2 * FamilySpace.Inset), name);
        Assert.Equal(FamilyShape.Track, plan.Thickness);
        Assert.Equal(plan.Ring - (2 * FamilyShape.Track), plan.Hole);
    }

    [Fact]
    public void S3342_a_larger_tile_draws_a_larger_ring()
    {
        var s = ScreenTimeDonutLayout.Donut(Content(Tier("S")));
        var m = ScreenTimeDonutLayout.Donut(Content(Tier("M")));
        var xl = ScreenTimeDonutLayout.Donut(Content(Tier("XL")));

        Assert.True(m.Ring > s.Ring);
        Assert.True(xl.Ring > m.Ring);
    }
}
