using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The sizing rules from the widget layout pass, [S-640..S-659].</summary>
public sealed class WidgetFitTests
{
    // ---- [S-655] a header's trailing label ------------------------------------------------------

    [Fact]
    public void S655_a_narrow_header_drops_the_label_so_the_title_can_be_read()
    {
        // The XS weather tile: 88px of header, 24 of it the icon and its gap.
        Assert.Equal(0, HeaderFit.TrailingLabelWidth(88, 24));
    }

    [Fact]
    public void S655_a_wider_header_gives_the_label_what_the_title_does_not_need()
    {
        Assert.Equal(168 - 24 - HeaderFit.MinimumTitle, HeaderFit.TrailingLabelWidth(168, 24));
    }

    [Fact]
    public void S655_an_unmeasured_header_does_not_limit_the_label()
    {
        Assert.True(double.IsPositiveInfinity(HeaderFit.TrailingLabelWidth(0, 24)));
        Assert.True(double.IsPositiveInfinity(HeaderFit.TrailingLabelWidth(double.NaN, 24)));
    }
}
