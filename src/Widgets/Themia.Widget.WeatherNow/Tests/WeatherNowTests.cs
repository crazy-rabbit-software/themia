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
    public void S11101_each_style_is_one_height_header_content_and_inset_and_140_to_320_wide()
    {
        Assert.Equal(36 + 36 + 16 + 12, Of(WeatherNowStyle.Standard).Natural.Height);
        Assert.Equal(36 + 52 + 12, Of(WeatherNowStyle.LargeIcon).Natural.Height);
        Assert.Equal(36 + 36 + 12, Of(WeatherNowStyle.Minimal).Natural.Height);
        foreach (var style in Enum.GetValues<WeatherNowStyle>())
        {
            var sizing = Of(style);
            Assert.True(sizing.FixedHeight && !sizing.FixedWidth, $"{style}");
            Assert.Equal((140.0, 320.0), (sizing.Min.Width, sizing.Max.Width));
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
