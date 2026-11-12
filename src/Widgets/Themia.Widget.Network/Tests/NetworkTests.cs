using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Network.Tests;

/// <summary>[S-1191] The network reading: download over upload, one height, no style.</summary>
public sealed class NetworkTests
{
    [Fact]
    public void S1191_download_over_upload_one_height_and_no_style()
    {
        var kind = ModuleCatalog.Of(new WidgetId("network"));
        var sizing = kind.SizingOf();
        Assert.Equal(36 + 36 + 8 + 36 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.True(sizing.Max.Width <= 320);
        Assert.False(WidgetStyles.Offers(new WidgetId("network")));
        Assert.Equal(SystemInterests.Chart(SystemSource.Network).Series, SystemInterests.For(new NetworkConfig(), @"C:\").Series);
    }
}
