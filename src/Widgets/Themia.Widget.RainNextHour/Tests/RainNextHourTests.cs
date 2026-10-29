using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5126] The rain widget's ladder, on the family's tokens.</summary>
public sealed class RainNextHourTests
{
    private const string Headline = "Rain in 30 min";

    /// <summary>The seven tiers less the card's header.</summary>
    private static readonly WidgetArea Xs = new(120, 76), S = new(200, 106), M = new(320, 196), Wide = new(480, 116), Xl = new(800, 516);

    [Fact]
    public void S5126_the_sentence_is_title_where_its_line_holds_it_and_body_strong_on_two_lines_where_not()
    {
        var tiny = RainLayout.Rain(Xs, Headline, hasCaption: true);
        Assert.Equal(FamilyType.BodyStrong, tiny.Headline);
        Assert.True(tiny.Wraps);

        Assert.All(new[] { S, M, Wide, Xl }, area =>
        {
            var plan = RainLayout.Rain(area, Headline, hasCaption: true);
            Assert.Equal(FamilyType.Title, plan.Headline);
            Assert.False(plan.Wraps);
        });
    }

    [Fact]
    public void S5126_the_ladder_adds_the_caption_then_the_bars_which_take_the_rest_to_the_foot()
    {
        Assert.Equal(0, RainLayout.Rain(S, Headline, true).ChartHeight);
        Assert.True(RainLayout.Rain(S, Headline, true).Caption);
        Assert.Equal(0, RainLayout.Rain(Wide, Headline, true).ChartHeight);

        // M: the content's 180 less the sentence (28), the caption (20) and the 8 over the bars.
        var medium = RainLayout.Rain(M, Headline, true);
        Assert.Equal(180 - 28 - 20 - 8, medium.ChartHeight, 6);
        Assert.Equal(medium.ChartHeight - 4 - 16, medium.BarsHeight, 6);
        Assert.True(medium.BarsHeight >= RainLayout.ChartMin);

        // The sentence never grows with the tile: the room goes to the bars.
        Assert.Equal(FamilyType.Title, RainLayout.Rain(Xl, Headline, true).Headline);
        Assert.True(RainLayout.Rain(Xl, Headline, true).ChartHeight > medium.ChartHeight);
    }

    [Fact]
    public void S5126_only_rain_to_tell_of_draws_the_sentence_and_the_bars()
    {
        Assert.True(RainLayout.Warns(RainHeadline.StartsIn));
        Assert.True(RainLayout.Warns(RainHeadline.StopsIn));
        Assert.True(RainLayout.Warns(RainHeadline.Continues));
        Assert.False(RainLayout.Warns(RainHeadline.Dry));
        Assert.False(RainLayout.Warns(RainHeadline.Outdated));
    }
}
