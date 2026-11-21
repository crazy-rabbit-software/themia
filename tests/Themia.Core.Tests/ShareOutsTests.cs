using Themia.Core.Geometry;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-15026] A card shared out among several kinds, laid so each part keeps at least its minimum.</summary>
public sealed class ShareOutsTests
{
    private static readonly MergeShare[] LeadBesideStack = [new(0, 0, 0.25, 1), new(0.25, 0, 0.75, 0.5), new(0.25, 0.5, 0.75, 0.5)];

    [Fact]
    public void S15026_a_length_under_its_minimum_is_held_at_it_and_the_rest_shared_again()
    {
        Assert.Equal([10, 20, 30], ShareOuts.Split(60, [1, 2, 3], [0, 0, 0]));
        Assert.Equal([25, 14, 21], ShareOuts.Split(60, [1, 2, 3], [25, 0, 0]));
    }

    [Fact]
    public void S15026_parts_that_fit_their_shares_stay_in_them()
    {
        var card = new FracRect(0, 0, 0.4, 0.2);
        var rects = ShareOuts.Lay(card, LeadBesideStack, [(0.05, 0.05), (0.2, 0.05), (0.2, 0.05)])!;

        Assert.Equal([.. LeadBesideStack.Select(s => ShareOuts.Placed(card, s))], rects);
    }

    [Fact]
    public void S15026_the_line_beside_the_lead_moves_before_the_card_is_stacked_or_grown()
    {
        var card = new FracRect(0.1, 0.1, 0.4, 0.2);
        var rects = ShareOuts.Lay(card, LeadBesideStack, [(0.05, 0.05), (0.33, 0.05), (0.2, 0.05)])!;

        Assert.Equal(0.07, rects[0].W, 9);
        Assert.Equal(0.33, rects[1].W, 9);
        Assert.Equal(card.Right, rects[1].Right, 9);
    }

    [Fact]
    public void S15026_a_card_too_narrow_for_the_lead_beside_stacks_it_on_top()
    {
        var card = new FracRect(0, 0, 0.4, 0.3);
        var rects = ShareOuts.Lay(card, LeadBesideStack, [(0.1, 0.05), (0.35, 0.05), (0.2, 0.05)])!;

        Assert.All(rects, r => Assert.Equal(0.4, r.W, 9));
        Assert.Equal([0.0, 0.1, 0.2], rects.Select(r => Math.Round(r.Y, 9)));
    }

    [Fact]
    public void S15026_shares_that_are_not_a_lead_beside_a_stack_are_laid_as_shared()
        => Assert.Null(ShareOuts.Lay(new FracRect(0, 0, 1, 1), [new(0, 0, 0.5, 0.5), new(0.5, 0, 0.5, 0.5)], [(0.1, 0.1), (0.1, 0.1)]));
}
