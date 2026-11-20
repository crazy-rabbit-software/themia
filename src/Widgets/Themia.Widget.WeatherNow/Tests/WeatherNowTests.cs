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
    public void S11101_the_widest_reading_fits_its_narrowest_card_in_every_style()
    {
        var room = WeatherNowStyles.Narrowest - (2 * FamilySpace.Inset);
        Assert.True(ReadoutRules.Width(FamilyType.Big28, WeatherNowStyles.Worst) <= room);
        Assert.True(WeatherNowStyles.LargeGlyph + FamilySpace.GlyphGap + ReadoutRules.Width(FamilyType.Big28, WeatherNowStyles.Beside) <= room + 0.01);
    }
}
