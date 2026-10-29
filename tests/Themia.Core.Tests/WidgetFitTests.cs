using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The sizing rules from the widget layout pass, [S-640..S-659].</summary>
public sealed class WidgetFitTests
{
    // ---- [S-642] the empty state ----------------------------------------------------------------

    [Fact]
    public void S642_a_short_box_holds_the_sentence_to_the_lines_it_has()
    {
        Assert.Equal(3, EmptyStateFit.LineBudget(64, 20));
        Assert.Equal(1, EmptyStateFit.LineBudget(30, 20));
    }

    [Fact]
    public void S642_a_box_shorter_than_one_line_still_shows_one()
    {
        Assert.Equal(1, EmptyStateFit.LineBudget(8, 20));
    }

    [Fact]
    public void S642_an_unbounded_box_has_no_line_limit()
    {
        Assert.Equal(0, EmptyStateFit.LineBudget(double.PositiveInfinity, 20));
        Assert.Equal(0, EmptyStateFit.LineBudget(double.NaN, 20));
        Assert.Equal(0, EmptyStateFit.LineBudget(100, 0));
    }

    [Fact]
    public void S642_a_box_a_fraction_of_a_pixel_short_still_fits_its_lines()
    {
        Assert.Equal(3, EmptyStateFit.LineBudget(59.8, 20));
    }

    [Fact]
    public void S642_the_glyph_goes_before_the_sentence_does()
    {
        // 64px: a 32px glyph block and a two-line sentence fit; a three-line one does not.
        Assert.True(EmptyStateFit.ShowsIcon(72, 32, 40));
        Assert.False(EmptyStateFit.ShowsIcon(72, 32, 60));
    }

    [Fact]
    public void S642_an_unbounded_box_always_keeps_the_glyph()
    {
        Assert.True(EmptyStateFit.ShowsIcon(double.PositiveInfinity, 32, 400));
    }

    [Fact]
    public void S642_a_glyph_the_view_already_collapsed_costs_nothing()
    {
        Assert.True(EmptyStateFit.ShowsIcon(40, 0, 40));
    }

    // ---- [S-646] a reading's caption ------------------------------------------------------------

    [Fact]
    public void S646_a_wide_tile_keeps_the_caption_beside_the_reading()
    {
        Assert.True(ReadoutLayout.CaptionFitsBeside(288, FluentType.Title, 4, FluentType.Caption, 14, 8));
    }

    [Fact]
    public void S646_a_narrow_tile_moves_the_caption_under_the_reading()
    {
        // The XS memory tile: "46%" and "30 GB of 64 GB" in 88px.
        Assert.False(ReadoutLayout.CaptionFitsBeside(88, FluentType.Subtitle, 4, FluentType.Caption, 14, 8));
    }

    [Fact]
    public void S646_no_caption_or_no_measurement_keeps_the_one_line_layout()
    {
        Assert.True(ReadoutLayout.CaptionFitsBeside(88, FluentType.Display, 4, FluentType.Caption, 0, 8));
        Assert.True(ReadoutLayout.CaptionFitsBeside(double.NaN, FluentType.Display, 4, FluentType.Caption, 40, 8));
        Assert.True(ReadoutLayout.CaptionFitsBeside(0, FluentType.Display, 4, FluentType.Caption, 40, 8));
    }

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
