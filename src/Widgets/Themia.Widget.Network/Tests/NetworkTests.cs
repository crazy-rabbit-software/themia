using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Network.Tests;

/// <summary>[S-1191] The network reading: download and upload on one line, one height, no style.</summary>
public sealed class NetworkTests
{
    [Fact]
    public void S1191_download_and_upload_on_one_line_one_height_and_no_style()
    {
        var kind = ModuleCatalog.Of(new WidgetId("network"));
        var sizing = kind.SizingOf();
        Assert.Equal(12 + 36 + 12, sizing.Natural.Height);
        Assert.Equal(sizing.Min.Height, sizing.Natural.Height);
        Assert.Equal(Themia.Sdk.SizeStretch.Ceiling, sizing.Max.Height); // relaxed, [S-12411]: no practical Max
        Assert.True(sizing.Min.Width >= MeterSizing.NumbersWidth(ReadoutRules.Split(Formatting.Rate(1023 * 1024))) + 24);
        Assert.Equal(SizeStretch.Ceiling, sizing.Max.Width); // relaxed, [S-12411]
        Assert.False(WidgetStyles.Offers(new WidgetId("network")));
        Assert.Equal(SystemInterests.Chart(SystemSource.Network).Series, SystemInterests.For(new NetworkConfig(), @"C:\").Series);
    }
}
