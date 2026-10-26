using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Gpu.Tests;

/// <summary>[S-1200] [S-433] The GPU meter's options on its properties menu: Appearance, the adapter with two or more, the span under Style.</summary>
public sealed class GpuMenuTests
{
    private static readonly GpuAdapter[] Two = [new(0, "NVIDIA GeForce RTX 4070", "a", 0, 0), new(1, "Intel UHD Graphics", "b", 0, 0)];

    private int _saves;

    private WidgetMenuLines Build(GpuConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        GpuMenu.Lines(menu, config, adapters);
        return menu;
    }

    [Fact]
    public void S433_one_adapter_offers_no_choice()
    {
        var menu = Build(new GpuConfig(), [Two[0]]);

        Assert.Empty(menu.Lines);
        Assert.Equal(Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(), Assert.Single(menu.StyleLines).Text);
        Assert.Empty(Build(new GpuConfig { Style = GpuStyle.Gauge }, [Two[0]]).StyleLines);
    }

    [Fact]
    public void S433_two_adapters_are_a_choice_by_name_after_appearance()
    {
        var menu = Build(new GpuConfig(), Two);

        var adapter = Assert.Single(menu.Lines);
        Assert.Equal(Str.Details_Settings_Gpu_Adapter_Label.Text(), adapter.Text);
        Assert.Equal(Two.Select(a => a.Name), adapter.Children!.Select(c => c.Text));
        Assert.True(adapter.Children![0].Checked);
    }

    [Fact]
    public void S433_an_adapter_that_is_gone_shows_as_the_first_and_a_pick_is_stored_once()
    {
        var config = new GpuConfig { Adapter = 5 };
        var adapter = Assert.Single(Build(config, Two).Lines);
        Assert.True(adapter.Children![0].Checked);

        adapter.Children![1].Action!();
        Assert.Equal(1, config.Adapter);
        Assert.Equal(1, _saves);

        Assert.Single(Build(config, Two).Lines).Children![1].Action!();
        Assert.Equal(1, _saves);
    }
}
