using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11170] The fact atoms' shared rules: the pick's repair, its resize and its sizing.</summary>
public sealed class SkyFactsTests
{
    private enum Fact
    {
        A,
        B,
        C,
        D,
    }

    private static readonly Fact[] All = [Fact.A, Fact.B, Fact.C, Fact.D];

    [Fact]
    public void S11170_the_height_is_the_content_top_a_28_row_a_fact_and_the_inset_following_the_count()
    {
        var sizing = SkyFacts.Sizing(3, 4);

        Assert.Equal(36 + (3 * 28) + 12, sizing.Natural.Height);
        Assert.Equal((36 + 28 + 12.0, 36 + (4 * 28) + 12.0), (sizing.Min.Height, sizing.Max.Height));
        Assert.Equal((200.0, 240.0, 420.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.True(sizing.HeightSteps!.FollowsConfig);
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S11170_a_resize_keeps_the_first_facts_or_adds_the_next_not_picked_in_order()
    {
        Assert.Equal([Fact.C, Fact.A], SkyFacts.Resize([Fact.C, Fact.A, Fact.D], 2, All));
        Assert.Equal([Fact.C, Fact.A, Fact.B, Fact.D], SkyFacts.Resize([Fact.C, Fact.A], 9, All));
        Assert.Equal([Fact.C], SkyFacts.Resize([Fact.C, Fact.A], 0, All));
    }

    [Fact]
    public void S11170_a_pick_is_repaired_to_each_known_fact_once_or_to_the_defaults_when_nothing_is_left()
    {
        Check([Fact.B, Fact.A], true, SkyFacts.Repair<Fact>([Fact.B, Fact.B, (Fact)9, Fact.A], All, [Fact.A]));
        Check([Fact.A, Fact.C], true, SkyFacts.Repair<Fact>([], All, [Fact.A, Fact.C]));
        Check([Fact.A, Fact.C], true, SkyFacts.Repair<Fact>(null, All, [Fact.A, Fact.C]));
        Check([Fact.D], false, SkyFacts.Repair<Fact>([Fact.D], All, [Fact.A]));
    }

    private static void Check(Fact[] facts, bool changed, (List<Fact> Facts, bool Changed) result)
    {
        Assert.Equal(facts, result.Facts);
        Assert.Equal(changed, result.Changed);
    }
}
