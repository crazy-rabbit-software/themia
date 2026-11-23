using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherNow.Tests;

/// <summary>[S-11100] [S-11101] The weather's reading: one thing in three looks, each one height, 140..320 wide.</summary>
public sealed class WeatherNowTests
{
    private static readonly WidgetDescriptor Kind = new WeatherNowModule().Descriptor;

    private static WidgetSizing Of(WeatherNowStyle style) => Kind.Sizing(new WeatherNowConfig { Style = style });

    [Fact]
    public void S13904_it_is_called_weather_summary_in_every_language()
    {
        Assert.Equal(("Weather summary", "Wetterübersicht"), (Kind.Label.Text(Locale.En), Kind.Label.Text(Locale.De)));
        Assert.All(Enum.GetValues<Locale>(), locale => Assert.False(string.IsNullOrWhiteSpace(Kind.Label.Text(locale))));
    }

    [Fact]
    public void S11101_each_style_is_one_height_its_content_in_the_inset_and_140_to_320_wide()
    {
        Assert.Equal(12 + 36 + 16 + 12, Of(WeatherNowStyle.Standard).Natural.Height);
        Assert.Equal(12 + 52 + 12, Of(WeatherNowStyle.LargeIcon).Natural.Height);
        Assert.Equal(12 + 36 + 12, Of(WeatherNowStyle.Minimal).Natural.Height);
        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            var sizing = Of(style);
            // [S-12833] One height at its natural size; it scales down as one to its temperature at 9, the rest giving way.
            Assert.True(sizing.Min.Height < sizing.Natural.Height && !sizing.FixedWidth, $"{style}");

            // Relaxed, [S-12411]: twice its natural on each axis, never under the content's 320; its least the lesser of
            // 140 and its parts' least box in the inset ([S-12833]).
            var least = WeatherNowStyles.Face(new WeatherNowConfig { Style = style }).LeastBox();
            var scaled = Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(least.Width + 24));
            Assert.Equal((Math.Min(140.0, scaled), Math.Max(320.0, SizeStretch.Ceiling)), (sizing.Min.Width, sizing.Max.Width));
            Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(least.Height + 24)), sizing.Min.Height);
            Assert.Equal(SizeStretch.Ceiling, sizing.Max.Height);
            Assert.Empty(sizing.Problems());
        }
    }

    [Fact]
    public void S16447_with_no_reading_each_style_keeps_its_face_held_to_its_widest_reading()
    {
        Assert.Equal(new UnreadFace(true, false, WeatherNowStyles.Worst), WeatherNowStyles.Unread(WeatherNowStyle.Standard));
        Assert.Equal(new UnreadFace(false, true, WeatherNowStyles.Beside), WeatherNowStyles.Unread(WeatherNowStyle.LargeIcon));
        Assert.Equal(new UnreadFace(false, false, WeatherNowStyles.Bare), WeatherNowStyles.Unread(WeatherNowStyle.Minimal));

        // The held form is what each style's face is planned on (its sizing and its least box), so "--" fills as a reading.
        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            Assert.False(string.IsNullOrEmpty(WeatherNowStyles.Unread(style).Held.Value));
        }
    }

    [Fact]
    public void S16453_why_stands_in_the_empty_line_under_at_its_own_size_or_is_the_tooltip()
    {
        // The line under is kept, empty: a figure space draws no ink, so the block is a reading's two lines.
        Assert.Equal(" ", WeatherNowStyles.UnderSlot);
        Assert.True(string.IsNullOrWhiteSpace(WeatherNowStyles.UnderSlot));

        // Why stands there where the line drawn holds a Caption line and the start of a sentence; else the tooltip.
        Assert.True(WeatherNowStyles.WhyFits(FamilyType.Caption.LineHeight, WhyLine.MinBeside));
        Assert.True(WeatherNowStyles.WhyFits(99, 800));
        Assert.False(WeatherNowStyles.WhyFits(FamilyType.Caption.LineHeight - 2, 800));
        Assert.False(WeatherNowStyles.WhyFits(40, WhyLine.MinBeside - 1));
        Assert.False(WeatherNowStyles.WhyFits(double.NaN, 800));
    }

    [Fact]
    public void S16454_the_summary_centres_until_an_alignment_is_chosen()
    {
        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            Assert.Equal(ContentAlign.Center, ((IContentAligned)new WeatherNowConfig { Style = style }).DefaultAlign);
        }
    }

    [Fact]
    public void S16433_the_status_mark_stands_clear_beside_or_gives_way_but_never_over_the_reading()
    {
        var block = new SizeDip(110, 52);
        var least = new SizeDip(20, 14);
        const double mark = 4 + 16;

        // Nothing shown, or a block the width stops standing under the mark's band: the mark has the corner to itself.
        Assert.Equal(MarkPlace.Clear, WeatherNowStyles.Mark(156, 52, block, least, 0, mark));
        Assert.Equal(MarkPlace.Clear, WeatherNowStyles.Mark(156, 200, block, least, 24, mark));

        // A block that fills the height reaches into the band: it ends the mark's room sooner.
        Assert.Equal(MarkPlace.Beside, WeatherNowStyles.Mark(156, 52, block, least, 24, mark));

        // The least card (39 × 32): the room left beside the mark cannot hold the reading at its floor, so the mark gives way.
        Assert.Equal(MarkPlace.Yields, WeatherNowStyles.Mark(15, 8, block, least, 24, mark));
    }

    [Fact]
    public void S11101_the_widest_reading_fits_its_narrowest_card_in_every_style()
    {
        var room = WeatherNowStyles.Narrowest - (2 * FamilySpace.Inset);
        Assert.True(ReadoutRules.Width(FamilyType.Big28, WeatherNowStyles.Worst) <= room);
        Assert.True(WeatherNowStyles.LargeGlyph + FamilySpace.GlyphGap + ReadoutRules.Width(FamilyType.Big28, WeatherNowStyles.Beside) <= room + 0.01);
    }
}
