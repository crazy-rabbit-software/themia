using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-1122] What every meter's properties menu shares (<see cref="MeterMenuLines"/>): the chart span
/// under Style in the look that draws a history, nothing in a look without one. Over every meter of
/// the build, since the meters' configs are their modules'.
/// </summary>
public sealed class MeterMenuHostTests
{
    private int _saves;

    public static TheoryData<string> Meters() =>
        [.. HostCatalog.ModuleWidgets.Where(d => d.CreateDefault() is GaugeConfig).Select(d => d.Id.Value)];

    private static GaugeConfig Meter(string id)
        => (GaugeConfig)HostCatalog.ModuleWidgets.Single(d => d.Id.Value == id).CreateDefault();

    private WidgetMenuLines Build(GaugeConfig gauge)
    {
        var menu = new WidgetMenuLines(() => _saves++, _ => { });
        MeterMenuLines.Lines(menu, gauge);
        return menu;
    }

    [Fact]
    public void S1122_the_build_has_meters()
        => Assert.Contains(HostCatalog.ModuleWidgets, d => d.Id.Value == "cpu" && d.CreateDefault() is GaugeConfig);

    [Theory]
    [MemberData(nameof(Meters))]
    public void S1122_a_meter_in_its_graph_style_offers_the_span_under_style_and_nothing_on_the_first_level(string id)
    {
        var gauge = Meter(id);
        Assert.True(WidgetStyles.Offers(gauge.Kind), $"{id}: every meter has styles; an unstyled one puts its history lines on the first level");

        var menu = Build(gauge);

        Assert.Empty(menu.Lines);
        var span = Assert.Single(menu.StyleLines);
        Assert.Equal(Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(), span.Text);
        Assert.Equal(MeterTiming.SpanChoices.Select(MeterTiming.SpanLabel), span.Children!.Select(c => c.Text));
        Assert.Equal(MeterTiming.SpanLabel(MeterTiming.NearestSpan(gauge.GraphSpanSeconds)), Assert.Single(span.Children!, c => c.Checked == true).Text);
    }

    [Fact]
    public void S1122_a_look_without_a_history_offers_no_span()
    {
        Assert.Empty(Build(new CpuConfig { Style = CpuStyle.Grid }).StyleLines);
        Assert.Empty(Build(new MemoryConfig { Style = MemoryStyle.Gauge }).StyleLines);
        Assert.Single(Build(new CpuConfig { Style = CpuStyle.CoreGraphs }).StyleLines);
    }

    [Fact]
    public void S1122_a_span_picked_is_stored_once_and_the_current_one_stores_nothing()
    {
        var cpu = new CpuConfig { GraphSpanSeconds = 60 };
        var span = Assert.Single(Build(cpu).StyleLines);

        span.Children!.Single(c => c.Checked == true).Action!();
        Assert.Equal(0, _saves);

        span.Children![MeterTiming.SpanChoices.ToList().IndexOf(900)].Action!();
        Assert.Equal(900, cpu.GraphSpanSeconds);
        Assert.Equal(1, _saves);
    }
}
