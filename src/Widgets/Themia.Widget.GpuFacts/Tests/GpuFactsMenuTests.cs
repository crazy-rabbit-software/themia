using Themia.Core.Localization;
using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.GpuFacts.Tests;

/// <summary>[S-2431] The GPU details' options on its properties menu: Appearance, then the adapter with two or more.</summary>
public sealed class GpuFactsMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(GpuFactsConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        GpuFactsMenu.Lines(menu, config, adapters);
        return menu;
    }

    [Fact]
    public void S2431_the_adapter_is_offered_with_two_or_more_only()
    {
        GpuAdapter[] two = [new(0, "Radeon RX 7800", "a", 0, 0), new(1, "Intel Arc", "b", 0, 0)];

        // [S-11016] One adapter, or none: the facts line alone.
        Assert.Equal(Str.Details_Facts.Text(), Assert.Single(Build(new GpuFactsConfig(), [two[0]]).Lines).Text);
        Assert.Single(Build(new GpuFactsConfig(), []).Lines);

        var config = new GpuFactsConfig();
        var menu = Build(config, two);
        Assert.Empty(menu.StyleLines);

        Assert.Equal(2, menu.Lines.Count);
        menu.Lines[0].Children![1].Action!();
        Assert.Equal(1, config.Adapter);
        Assert.Equal(1, _saves);
    }
}
