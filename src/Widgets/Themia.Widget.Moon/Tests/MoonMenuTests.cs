using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Moon.Tests;

/// <summary>[S-3108] The Moon's options on its properties menu: Appearance, then the sky's shared location search ([S-3101]).</summary>
public sealed class MoonMenuTests
{
    private static WidgetMenuLines Build(MoonConfig config)
    {
        var lines = new WidgetMenuLines(() => { }, _ => { });
        MoonMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S3108_the_first_level_is_appearance_then_use_my_location_and_the_location_search_and_nothing_under_style()
    {
        var lines = Build(new MoonConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Empty(lines.StyleLines);
        Assert.Equal(["-", Str.Shell_Place_UseDevice.Text(), Str.Shell_Place_Location.Text()], lines.Lines.Select(l => l.IsSeparator ? "-" : l.Text));
    }

    [Fact]
    public void S3108_the_month_of_phases_offers_no_longitude()
    {
        var disc = Assert.Single(Build(new MoonConfig { Style = MoonStyle.Disc }).Lines, l => l.Search is not null).Search!;
        var month = Assert.Single(Build(new MoonConfig { Style = MoonStyle.PhaseMonth }).Lines, l => l.Search is not null).Search!;

        Assert.Contains(disc.Fixed, f => f.Text == Str.SunMoon_Longitude_Label.Text());
        Assert.DoesNotContain(month.Fixed, f => f.Text == Str.SunMoon_Longitude_Label.Text());
        Assert.Contains(month.Fixed, f => f.Text == Str.SunMoon_Latitude_Label.Text());
    }
}
