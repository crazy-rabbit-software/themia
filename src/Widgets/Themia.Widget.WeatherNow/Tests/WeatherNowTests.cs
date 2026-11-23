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
            // [S-12833] One height at its natural size; it scales down as one, the condition giving way.
            Assert.True(sizing.Min.Height < sizing.Natural.Height && !sizing.FixedWidth, $"{style}");

            // Relaxed, [S-12411]: twice its natural on each axis, never under the content's 320; its least the lesser of
            // 140 and its least box in the inset ([S-16456]: its unit kept, the mark's room beside it).
            var least = WeatherNowStyles.Least(new WeatherNowConfig { Style = style });
            var scaled = Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(least.Width + 24));
            Assert.Equal((Math.Min(140.0, scaled), Math.Max(320.0, SizeStretch.Ceiling)), (sizing.Min.Width, sizing.Max.Width));
            Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(least.Height + 24)), sizing.Min.Height);
            Assert.Equal(SizeStretch.Ceiling, sizing.Max.Height);
            Assert.Empty(sizing.Problems());
        }
    }

    [Fact]
    public void S16447_with_no_reading_each_style_keeps_its_face()
    {
        Assert.Equal(new UnreadFace(true, false), WeatherNowStyles.Unread(WeatherNowStyle.Standard));
        Assert.Equal(new UnreadFace(false, true), WeatherNowStyles.Unread(WeatherNowStyle.LargeIcon));
        Assert.Equal(new UnreadFace(false, false), WeatherNowStyles.Unread(WeatherNowStyle.Minimal));
    }

    [Fact]
    public void S16455_no_reading_says_why_under_the_face_where_the_face_keeps_its_own_size()
    {
        // "--" held to two figures: a reading's height, about the dashes' width (so a centred face stays centred).
        Assert.Equal(2, WeatherNowStyles.UnreadHold.Length);
        Assert.All(WeatherNowStyles.UnreadHold, c => Assert.True(char.IsDigit(c)));

        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            var config = new WeatherNowConfig { Style = style };
            var natural = Of(style).Natural;

            // The line stands under the face only where the face keeps its own size over it: never made small by its why.
            var under = WeatherNowStyles.WhyUnder(config, natural.Width - 24, natural.Height - 24);
            Assert.Equal(natural.Height - 24 >= WeatherNowStyles.UnreadInk(style) + WhyLine.Band, under);
            Assert.True(WeatherNowStyles.WhyUnder(config, 876, 276), $"{style}");
            Assert.False(WeatherNowStyles.WhyUnder(config, double.NaN, 276), $"{style}");
        }

        // Standard's natural card holds the line (the face 28 at its own size); Minimal's and Large icon's say it by the mark.
        Assert.True(WeatherNowStyles.WhyUnder(new WeatherNowConfig(), 156, 52));
        Assert.False(WeatherNowStyles.WhyUnder(new WeatherNowConfig { Style = WeatherNowStyle.Minimal }, 116, 36));
        Assert.False(WeatherNowStyles.WhyUnder(new WeatherNowConfig { Style = WeatherNowStyle.LargeIcon }, 176, 52));
    }

    [Fact]
    public void S16455_the_face_and_its_why_stand_together_centred_in_the_card()
    {
        // A face the width stops leaves height: shared alike over the face and under the why's line (WhyLine.Spare), never
        // between them (900 x 300 stood the why 139 under the face, at the foot).
        var spare = WhyLine.Spare(876, 276 - WhyLine.Band, 100, 30, most: 5);
        Assert.Equal((276 - WhyLine.Band - 150) / 2, spare, 6);

        // A face the height stops leaves none: it stands on the line, the line on the inset.
        Assert.Equal(0, WhyLine.Spare(876, 40, 100, 30), 6);
    }

    [Fact]
    public void S16456_the_least_card_keeps_the_mark_beside_the_face()
    {
        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            var config = new WeatherNowConfig { Style = style };
            var sizing = Of(style);
            var face = WeatherNowStyles.Face(config);

            // The least card holds the main reading at the floor ([S-12833]) and the mark's room beside it, at its start too.
            Assert.True(sizing.Min.Width - 24 >= face.LeastBox().Width + (2 * WeatherNowStyles.MarkRoom) - 0.01, $"{style}");
            Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(face.LeastBox().Height + 24)), sizing.Min.Height);

            // There the mark never gives way: a stale reading or no reading says so beside the face (it was dropped at 39 x 32).
            var block = new SizeDip(sizing.Natural.Width - 24, sizing.Natural.Height - 24);
            var place = WeatherNowStyles.Mark(sizing.Min.Width - 24, sizing.Min.Height - 24, block, face.LeastBox(), WeatherNowStyles.MarkRoom, 4 + 16);
            Assert.NotEqual(MarkPlace.Yields, place);

            // And the why is that mark there, not a line: the least card cannot hold the line under the face.
            Assert.False(WeatherNowStyles.WhyUnder(config, sizing.Min.Width - 24, sizing.Min.Height - 24), $"{style}");
        }
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

        // A room under the least card's (15 × 8): what is left beside the mark cannot hold the reading at its floor, so the
        // mark gives way (the kind's least keeps it, [S-16456]).
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
