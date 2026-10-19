using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.AirQuality.Tests;

/// <summary>[S-5108] The air quality widget's options on its properties menu.</summary>
public sealed class AirQualityMenuTests
{
    private static WidgetMenuLines Build(AirQualityConfig config)
    {
        var lines = new WidgetMenuLines(() => { }, _ => { });
        AirQualityMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S5108_appearance_then_the_location_and_its_name_then_the_index_each_behind_a_divider()
    {
        var lines = Build(new AirQualityConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Equal(["-", Str.Shell_Place_Location.Text(), Str.Weather_LocationNameLabel.Text(), "-", AirQualityStr.Online_Air_Settings_Index_Label.Text()], lines.Lines.Select(l => l.IsSeparator ? "-" : l.Text));
    }

    [Fact]
    public void S5108_the_index_is_a_choice_and_ends_with_the_source_greyed()
    {
        var config = new AirQualityConfig { Scale = AirQualityScale.Auto };
        var index = Build(config).Lines.Single(l => l.Text == AirQualityStr.Online_Air_Settings_Index_Label.Text()).Children!;

        Assert.Equal([true, false, false], index.Take(3).Select(o => o.Checked == true));
        Assert.True(index[^1].Disabled);
        Assert.Contains("CAMS", index[^1].Text, StringComparison.Ordinal);

        index[2].Action!();
        Assert.Equal(AirQualityScale.Us, config.Scale);
    }
}
