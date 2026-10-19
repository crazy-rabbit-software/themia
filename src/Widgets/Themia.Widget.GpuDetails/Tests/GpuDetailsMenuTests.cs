using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.GpuDetails.Tests;

/// <summary>[S-2431] The GPU details' options on its properties menu: Appearance, then the adapter with two or more.</summary>
public sealed class GpuDetailsMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(GpuDetailsConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        GpuDetailsMenu.Lines(menu, config, adapters);
        return menu;
    }

    [Fact]
    public void S2431_the_adapter_is_offered_with_two_or_more_only()
    {
        GpuAdapter[] two = [new(0, "Radeon RX 7800", "a", 0, 0), new(1, "Intel Arc", "b", 0, 0)];

        Assert.Empty(Build(new GpuDetailsConfig(), [two[0]]).Lines);
        Assert.Empty(Build(new GpuDetailsConfig(), []).Lines);

        var config = new GpuDetailsConfig();
        var menu = Build(config, two);
        Assert.Equal(0, menu.CardAt);
        Assert.Empty(menu.StyleLines);

        Assert.Single(menu.Lines).Children![1].Action!();
        Assert.Equal(1, config.Adapter);
        Assert.Equal(1, _saves);
    }
}
