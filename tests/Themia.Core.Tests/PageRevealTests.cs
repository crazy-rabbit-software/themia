using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2347] An opened item scrolled into sight as Karpo's settings panel does it (§671, <c>EditDeck.Reveal</c>).</summary>
public sealed class PageRevealTests
{
    private const double Gap = 16;

    [Fact]
    public void S2347_an_item_in_sight_does_not_move_the_page()
        => Assert.Equal(100, PageReveal.ScrollIntoView(100, 500, 200, 300, 2000, Gap));

    [Fact]
    public void S2347_an_item_whose_content_still_fits_keeps_its_head_where_it_is()
    {
        // The owner, 2026-11-16: "sometimes everything is shifted although the content of the expansion section would still fit".
        var to = PageReveal.ScrollIntoView(0, 600, 300, 284, 2000, Gap);

        Assert.Equal(0, to);
        Assert.False(PageReveal.Moves(0, to));
    }

    [Fact]
    public void S2347_an_item_past_the_foot_scrolls_only_as_far_as_its_foot_needs()
    {
        // Its foot at 900 with the gap: the viewport's foot there, its head still well below the top.
        var to = PageReveal.ScrollIntoView(0, 600, 500, 400, 2000, Gap);

        Assert.Equal(900 + Gap - 600, to);
        Assert.True(500 - to > Gap, "the head is not pushed to the top");
    }

    [Fact]
    public void S2347_an_item_taller_than_the_viewport_puts_its_head_at_the_top()
        => Assert.Equal(500 - Gap, PageReveal.ScrollIntoView(0, 600, 500, 900, 3000, Gap));

    [Fact]
    public void S2347_an_item_above_the_top_comes_down_to_the_top()
        => Assert.Equal(200 - Gap, PageReveal.ScrollIntoView(400, 600, 200, 100, 2000, Gap));

    [Fact]
    public void S2347_the_scroll_stays_within_the_page()
    {
        // Its foot at the page's end: no further than the page scrolls.
        Assert.Equal(1400, PageReveal.ScrollIntoView(0, 600, 1800, 200, 2000, Gap));

        // Its head at the page's top: never above it.
        Assert.Equal(0, PageReveal.ScrollIntoView(300, 600, 4, 50, 2000, Gap));

        // A page shorter than its viewport never scrolls.
        Assert.Equal(0, PageReveal.ScrollIntoView(0, 600, 400, 300, 500, Gap));
    }

    [Fact]
    public void S2347_less_than_a_dip_is_no_move()
    {
        Assert.False(PageReveal.Moves(100, 100.5));
        Assert.True(PageReveal.Moves(100, 101));
    }

    [Fact]
    public void S2347_the_glide_eases_out_over_its_time_and_ends_where_it_goes()
    {
        Assert.Equal(100, PageReveal.Glide(100, 300, 0));
        Assert.Equal(300, PageReveal.Glide(100, 300, PageReveal.Ms));
        Assert.Equal(300, PageReveal.Glide(100, 300, PageReveal.Ms * 3));

        // A cubic ease-out: past half the way at half the time, and never back.
        var half = PageReveal.Glide(100, 300, PageReveal.Ms / 2);
        Assert.True(half > 200, $"{half}");
        Assert.True(PageReveal.Glide(100, 300, PageReveal.Ms * 0.75) > half);
        Assert.False(PageReveal.Done(PageReveal.Ms - 1));
        Assert.True(PageReveal.Done(PageReveal.Ms));
    }
}
